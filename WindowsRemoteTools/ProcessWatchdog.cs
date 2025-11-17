using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Watches and restarts the UI process if it terminates
    /// </summary>
    public class ProcessWatchdog : IDisposable
    {
        private readonly string _uiExecutablePath;
        private Process? _uiProcess;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _watchTask;
        private bool _running;

        public event EventHandler? ProcessStarted;
        public event EventHandler? ProcessStopped;

        public ProcessWatchdog(string uiExecutablePath)
        {
            _uiExecutablePath = uiExecutablePath ?? throw new ArgumentNullException(nameof(uiExecutablePath));
        }

        public void Start()
        {
            if (_running)
                return;

            _running = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _watchTask = Task.Run(() => WatchLoop(_cancellationTokenSource.Token));
        }

        private async Task WatchLoop(CancellationToken cancellationToken)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_uiProcess == null || _uiProcess.HasExited)
                    {
                        Console.WriteLine("ProcessWatchdog: UI process not running, starting...");
                        StartUIProcess();
                    }

                    await Task.Delay(5000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ProcessWatchdog error: {ex.Message}");
                }
            }
        }

        private void StartUIProcess()
        {
            try
            {
                // Kill old process if it exists
                if (_uiProcess != null && !_uiProcess.HasExited)
                {
                    try
                    {
                        _uiProcess.Kill();
                    }
                    catch { }
                }

                // Get working directory
                var workingDir = System.IO.Path.GetDirectoryName(_uiExecutablePath) ?? "";

                // Start process in active user session (from Service context)
                _uiProcess = ProcessLauncher.StartProcessAsActiveUser(_uiExecutablePath, workingDir);

                if (_uiProcess != null)
                {
                    Console.WriteLine($"ProcessWatchdog: UI process started in user session (PID: {_uiProcess.Id})");
                    ProcessStarted?.Invoke(this, EventArgs.Empty);

                    _uiProcess.EnableRaisingEvents = true;
                    _uiProcess.Exited += (s, e) =>
                    {
                        Console.WriteLine("ProcessWatchdog: UI process exited");
                        ProcessStopped?.Invoke(this, EventArgs.Empty);
                    };
                }
                else
                {
                    Console.WriteLine("ProcessWatchdog: Failed to start UI process in user session");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ProcessWatchdog StartUIProcess error: {ex.Message}");
            }
        }

        public void Stop()
        {
            _running = false;
            _cancellationTokenSource?.Cancel();

            if (_uiProcess != null && !_uiProcess.HasExited)
            {
                try
                {
                    _uiProcess.Kill();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            Stop();
            _watchTask?.Wait(TimeSpan.FromSeconds(2));
            _uiProcess?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
