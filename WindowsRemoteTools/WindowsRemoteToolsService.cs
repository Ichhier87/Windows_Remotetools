using System;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Windows Service implementation for Windows Remote Tools
    /// </summary>
    public class WindowsRemoteToolsService : ServiceBase
    {
        private RemoteToolsCore? _core;
        private ProcessWatchdog? _watchdog;

        public WindowsRemoteToolsService()
        {
            ServiceName = "WindowsRemoteToolsService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        /// <summary>
        /// Called when the service starts
        /// </summary>
        protected override void OnStart(string[] args)
        {
            try
            {
                EventLog.WriteEntry("Windows Remote Tools Service is starting...", System.Diagnostics.EventLogEntryType.Information);
                Console.WriteLine("Service starting...");

                // Start core with service mode (uses pipe server instead of direct overlay)
                _core = new RemoteToolsCore(useOverlayDirectly: false);
                _core.Start();

                // Start UI process watchdog
                var uiExePath = GetUIExecutablePath();
                if (File.Exists(uiExePath))
                {
                    _watchdog = new ProcessWatchdog(uiExePath);
                    _watchdog.ProcessStarted += (s, e) => EventLog.WriteEntry("UI process started", System.Diagnostics.EventLogEntryType.Information);
                    _watchdog.ProcessStopped += (s, e) => EventLog.WriteEntry("UI process stopped", System.Diagnostics.EventLogEntryType.Warning);
                    _watchdog.Start();
                    EventLog.WriteEntry("UI process watchdog started", System.Diagnostics.EventLogEntryType.Information);
                }
                else
                {
                    EventLog.WriteEntry($"UI executable not found at: {uiExePath}", System.Diagnostics.EventLogEntryType.Warning);
                }

                EventLog.WriteEntry("Windows Remote Tools Service started successfully", System.Diagnostics.EventLogEntryType.Information);
                Console.WriteLine("Service started successfully");
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"Failed to start service: {ex.Message}\n{ex.StackTrace}", System.Diagnostics.EventLogEntryType.Error);
                Console.WriteLine($"Service start failed: {ex.Message}");
                throw;
            }
        }

        private string GetUIExecutablePath()
        {
            // Get the directory where the service executable is located
            var serviceDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            return Path.Combine(serviceDir, "WindowsRemoteToolsUI.exe");
        }

        /// <summary>
        /// Called when the service stops
        /// </summary>
        protected override void OnStop()
        {
            try
            {
                EventLog.WriteEntry("Windows Remote Tools Service is stopping...", System.Diagnostics.EventLogEntryType.Information);
                Console.WriteLine("Service stopping...");

                _watchdog?.Stop();
                _watchdog?.Dispose();
                _watchdog = null;

                _core?.Dispose();
                _core = null;

                EventLog.WriteEntry("Windows Remote Tools Service stopped successfully", System.Diagnostics.EventLogEntryType.Information);
                Console.WriteLine("Service stopped successfully");
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"Error during service stop: {ex.Message}\n{ex.StackTrace}", System.Diagnostics.EventLogEntryType.Error);
                Console.WriteLine($"Service stop error: {ex.Message}");
            }
        }

        /// <summary>
        /// Called when the service receives a shutdown signal
        /// </summary>
        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _watchdog?.Dispose();
                _core?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
