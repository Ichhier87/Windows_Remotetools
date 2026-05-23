using System;
using System.IO;
using System.Text;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Redirects Console.Out and Console.Error to a log file while keeping the original output.
    /// Rotates the log file when it exceeds MaxFileSizeBytes.
    /// </summary>
    public static class FileLogger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsRemoteTools");

        public static string LogPath => Path.Combine(LogDir, "service.log");
        private static string OldLogPath => Path.Combine(LogDir, "service.old.log");

        private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(LogDir);

                RotateIfNeeded();

                var fileStream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                var fileWriter = new StreamWriter(fileStream, Encoding.UTF8) { AutoFlush = true };

                var teeOut = new TeeWriter(Console.Out, fileWriter);
                var teeErr = new TeeWriter(Console.Error, fileWriter);

                Console.SetOut(teeOut);
                Console.SetError(teeErr);

                Console.WriteLine($"[Logger] Log started — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"[Logger] Log file: {LogPath}");
            }
            catch (Exception ex)
            {
                // Don't crash the app if logging fails
                Console.Error.WriteLine($"[Logger] Failed to initialize file logger: {ex.Message}");
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= MaxFileSizeBytes)
                {
                    if (File.Exists(OldLogPath))
                        File.Delete(OldLogPath);
                    File.Move(LogPath, OldLogPath);
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes to two TextWriters simultaneously.
        /// </summary>
        private class TeeWriter : TextWriter
        {
            private readonly TextWriter _primary;
            private readonly TextWriter _secondary;

            public TeeWriter(TextWriter primary, TextWriter secondary)
            {
                _primary = primary;
                _secondary = secondary;
            }

            public override Encoding Encoding => _primary.Encoding;

            public override void Write(char value)
            {
                _primary.Write(value);
                _secondary.Write(value);
            }

            public override void Write(string? value)
            {
                var stamped = Stamp(value);
                _primary.Write(stamped);
                _secondary.Write(stamped);
            }

            public override void WriteLine(string? value)
            {
                var stamped = Stamp(value);
                _primary.WriteLine(stamped);
                _secondary.WriteLine(stamped);
            }

            public override void WriteLine()
            {
                _primary.WriteLine();
                _secondary.WriteLine();
            }

            private static string? Stamp(string? value) =>
                value == null ? null : $"[{DateTime.Now:HH:mm:ss}] {value}";

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _secondary.Dispose();
                }
                base.Dispose(disposing);
            }
        }
    }
}
