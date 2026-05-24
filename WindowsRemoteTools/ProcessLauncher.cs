using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Launches processes in active user sessions (from a Windows Service)
    /// </summary>
    public static class ProcessLauncher
    {
        #region Windows API Imports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr phToken);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern int WTSEnumerateSessions(
            IntPtr hServer,
            int Reserved,
            int Version,
            ref IntPtr ppSessionInfo,
            ref int pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            IntPtr hToken,
            bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
            TOKEN_TYPE TokenType,
            out IntPtr phNewToken);

        #endregion

        #region Structures and Enums

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public uint cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public uint SessionID;
            public IntPtr pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private enum SECURITY_IMPERSONATION_LEVEL
        {
            SecurityAnonymous,
            SecurityIdentification,
            SecurityImpersonation,
            SecurityDelegation
        }

        private enum TOKEN_TYPE
        {
            TokenPrimary = 1,
            TokenImpersonation
        }

        private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const uint CREATE_NEW_CONSOLE = 0x00000010;
        private const uint NORMAL_PRIORITY_CLASS = 0x00000020;

        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;

        #endregion

        /// <summary>
        /// Starts a process in the active user session
        /// </summary>
        public static Process? StartProcessAsActiveUser(string applicationPath, string workingDirectory)
        {
            return StartProcessAsActiveUser(applicationPath, workingDirectory, null);
        }

        /// <summary>
        /// Starts a process in the active user session with command-line arguments.
        /// </summary>
        public static Process? StartProcessAsActiveUser(string applicationPath, string workingDirectory, string? arguments)
        {
            var sessionId = GetActiveUserSessionId();
            if (sessionId == uint.MaxValue)
            {
                Console.WriteLine("ProcessLauncher: No active user session found");
                return null;
            }

            Console.WriteLine($"ProcessLauncher: Starting process in session {sessionId}");
            return StartProcessInSession(applicationPath, workingDirectory, sessionId, arguments);
        }

        /// <summary>
        /// Gets the session ID of the active user
        /// </summary>
        private static uint GetActiveUserSessionId()
        {
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            try
            {
                if (WTSEnumerateSessions(IntPtr.Zero, 0, 1, ref ppSessionInfo, ref count) == 0)
                {
                    return uint.MaxValue;
                }

                var sessionInfoSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                var current = ppSessionInfo;

                for (int i = 0; i < count; i++)
                {
                    var sessionInfo = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO))!;

                    // Look for active session
                    if (sessionInfo.State == WTS_CONNECTSTATE_CLASS.WTSActive)
                    {
                        return sessionInfo.SessionID;
                    }

                    current = IntPtr.Add(current, sessionInfoSize);
                }

                return uint.MaxValue;
            }
            finally
            {
                if (ppSessionInfo != IntPtr.Zero)
                {
                    WTSFreeMemory(ppSessionInfo);
                }
            }
        }

        /// <summary>
        /// Starts a process in a specific session
        /// </summary>
        private static Process? StartProcessInSession(string applicationPath, string workingDirectory, uint sessionId, string? arguments)
        {
            IntPtr userToken = IntPtr.Zero;
            IntPtr duplicatedToken = IntPtr.Zero;
            IntPtr environmentBlock = IntPtr.Zero;

            try
            {
                // Get user token for the session
                if (!WTSQueryUserToken(sessionId, out userToken))
                {
                    Console.WriteLine($"ProcessLauncher: WTSQueryUserToken failed: {Marshal.GetLastWin32Error()}");
                    return null;
                }

                // Duplicate the token
                if (!DuplicateTokenEx(
                    userToken,
                    TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY,
                    IntPtr.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary,
                    out duplicatedToken))
                {
                    Console.WriteLine($"ProcessLauncher: DuplicateTokenEx failed: {Marshal.GetLastWin32Error()}");
                    return null;
                }

                // Create environment block for the user
                if (!CreateEnvironmentBlock(out environmentBlock, duplicatedToken, false))
                {
                    Console.WriteLine($"ProcessLauncher: CreateEnvironmentBlock failed: {Marshal.GetLastWin32Error()}");
                    environmentBlock = IntPtr.Zero;
                }

                // Setup startup info
                var startupInfo = new STARTUPINFO
                {
                    cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO)),
                    lpDesktop = "winsta0\\default"
                };

                // Create the process
                var processInfo = new PROCESS_INFORMATION();
                var creationFlags = CREATE_UNICODE_ENVIRONMENT | NORMAL_PRIORITY_CLASS;
                var commandLine = string.IsNullOrWhiteSpace(arguments)
                    ? null
                    : $"\"{applicationPath}\" {arguments}";

                if (!CreateProcessAsUser(
                    duplicatedToken,
                    applicationPath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    environmentBlock,
                    workingDirectory,
                    ref startupInfo,
                    out processInfo))
                {
                    Console.WriteLine($"ProcessLauncher: CreateProcessAsUser failed: {Marshal.GetLastWin32Error()}");
                    return null;
                }

                // Get the Process object
                try
                {
                    var process = Process.GetProcessById((int)processInfo.dwProcessId);
                    Console.WriteLine($"ProcessLauncher: Successfully started process (PID: {processInfo.dwProcessId})");

                    // Close handles
                    CloseHandle(processInfo.hProcess);
                    CloseHandle(processInfo.hThread);

                    return process;
                }
                catch
                {
                    return null;
                }
            }
            finally
            {
                if (environmentBlock != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(environmentBlock);
                }

                if (duplicatedToken != IntPtr.Zero)
                {
                    CloseHandle(duplicatedToken);
                }

                if (userToken != IntPtr.Zero)
                {
                    CloseHandle(userToken);
                }
            }
        }
    }
}
