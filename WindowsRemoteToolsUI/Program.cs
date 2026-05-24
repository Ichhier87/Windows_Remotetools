using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace WindowsRemoteToolsUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            UIFileLogger.Init();
            Application.ThreadException += (_, e) => Console.WriteLine($"UI thread exception: {e.Exception}");
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.WriteLine($"UI unhandled exception: {e.ExceptionObject}");

            if (TryRunCommandMode(Environment.GetCommandLineArgs()))
                return;

            // Single-instance guard: if another instance is already running, exit immediately.
            // This prevents a second (manually started) instance from conflicting with the
            // watchdog-started instance.
            using var mutex = new System.Threading.Mutex(true, "WindowsRemoteToolsUI_SingleInstance", out bool isNewInstance);
            if (!isNewInstance)
            {
                Console.WriteLine("Another WindowsRemoteToolsUI instance is already running; exiting.");
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new UIApplication());
        }

        private static bool TryRunCommandMode(string[] args)
        {
            if (args.Length < 3 || !args[1].Equals("--show-web-overlay", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                ApplicationConfiguration.Initialize();
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(args[2]));
                var data = JsonConvert.DeserializeObject<WebOverlayData>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.Url))
                {
                    Console.WriteLine("Command mode web overlay ignored because payload is empty.");
                    return true;
                }

                Console.WriteLine($"Command mode showing web overlay: {data.Url}");
                Application.Run(new WebOverlayForm(data.Url, data.CanClose));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command mode failed: {ex}");
            }

            return true;
        }
    }

    public class UIApplication : ApplicationContext
    {
        private readonly UIPipeClient _pipeClient;
        private readonly OverlayWindow _overlayWindow;
        private readonly NotifyIcon _trayIcon;
        private readonly Control _uiInvoker;
        private WebOverlayForm? _webOverlay;
        private bool _running = true;

        public UIApplication()
        {
            Console.WriteLine("WindowsRemoteToolsUI starting application context.");

            _uiInvoker = new Control();
            _ = _uiInvoker.Handle;

            _overlayWindow = new OverlayWindow();
            _pipeClient = new UIPipeClient();

            // Setup system tray icon
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Windows Remote Tools UI"
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, OnExit);
            _trayIcon.ContextMenuStrip = contextMenu;

            // Setup pipe client events
            _pipeClient.MessageReceived += OnMessageReceived;
            _pipeClient.Connected += (s, e) => Console.WriteLine("Connected to service");
            _pipeClient.Disconnected += (s, e) => Console.WriteLine("Disconnected from service");

            // Connect to service
            Task.Run(async () => await ConnectToService());

            // Retry tray icon visibility: Windows shell may not be ready when started at boot
            // via the service watchdog. Re-show the icon every 3 s until it sticks (max 10 retries).
            int retryCount = 0;
            var iconRetryTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            iconRetryTimer.Tick += (s, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Visible = true;
                if (++retryCount >= 10)
                    iconRetryTimer.Stop();
            };
            iconRetryTimer.Start();
        }

        private async Task ConnectToService()
        {
            while (_running)
            {
                if (!_pipeClient.IsConnected)
                {
                    Console.WriteLine("Attempting to connect to service...");
                    await _pipeClient.ConnectAsync();
                }

                await Task.Delay(5000);
            }
        }

        private void OnMessageReceived(object? sender, PipeMessage message)
        {
            if (_uiInvoker.InvokeRequired)
            {
                _uiInvoker.BeginInvoke(new Action(() => OnMessageReceived(sender, message)));
                return;
            }

            Console.WriteLine($"Received message: {message.Type}");

            switch (message.Type)
            {
                case PipeMessage.MessageTypes.ShowOverlay:
                    HandleShowOverlay(message);
                    break;

                case PipeMessage.MessageTypes.HideOverlay:
                    _overlayWindow.Hide();
                    break;

                case PipeMessage.MessageTypes.ShowNotification:
                    HandleShowNotification(message);
                    break;

                case PipeMessage.MessageTypes.ShowWarning:
                    HandleShowWarning(message);
                    break;

                case PipeMessage.MessageTypes.ShowError:
                    HandleShowError(message);
                    break;

                case PipeMessage.MessageTypes.ShowBlockingScreen:
                    HandleShowBlockingScreen(message);
                    break;

                case PipeMessage.MessageTypes.ShowLockedScreen:
                    if (message.Data is Newtonsoft.Json.Linq.JObject jo)
                    {
                        var lockedData = jo.ToObject<LockedOverlayData>();
                        if (lockedData != null)
                            _overlayWindow.ShowLockedScreen(
                                lockedData.Message,
                                lockedData.PasswordHash,
                                () => _ = _pipeClient.SendMessageAsync(
                                        PipeMessage.Create(PipeMessage.MessageTypes.ScreenUnlocked)));
                    }
                    break;

                case PipeMessage.MessageTypes.ShowSvgOverlay:
                    if (message.Data is Newtonsoft.Json.Linq.JObject svgJo)
                    {
                        var svgData = svgJo.ToObject<SvgOverlayData>();
                        if (svgData != null)
                            _overlayWindow.ShowSvgOverlay(svgData);
                    }
                    break;

                case PipeMessage.MessageTypes.HideSvgOverlay:
                    _overlayWindow.HideSvgOverlay();
                    break;

                case PipeMessage.MessageTypes.ShowWebOverlay:
                    if (message.Data is Newtonsoft.Json.Linq.JObject webJo)
                    {
                        var webData = webJo.ToObject<WebOverlayData>();
                        if (webData != null)
                            ShowWebOverlay(webData);
                    }
                    break;

                case PipeMessage.MessageTypes.HideWebOverlay:
                    HideWebOverlay();
                    break;

                case PipeMessage.MessageTypes.ShowPicture:
                    var picPath = message.Data?.ToString();
                    if (!string.IsNullOrEmpty(picPath))
                        _overlayWindow.ShowPicture(picPath);
                    break;

                case PipeMessage.MessageTypes.Shutdown:
                    Shutdown();
                    break;
            }
        }

        private void HandleShowOverlay(PipeMessage message)
        {
            if (message.Data is Newtonsoft.Json.Linq.JObject jobj)
            {
                var overlayData = jobj.ToObject<OverlayData>();
                if (overlayData != null)
                {
                    Color? bgColor = null;
                    Color? txtColor = null;

                    if (!string.IsNullOrEmpty(overlayData.BackgroundColor))
                        bgColor = ColorTranslator.FromHtml(overlayData.BackgroundColor);

                    if (!string.IsNullOrEmpty(overlayData.TextColor))
                        txtColor = ColorTranslator.FromHtml(overlayData.TextColor);

                    _overlayWindow.ShowAsync(overlayData.Message, bgColor, txtColor, overlayData.Opacity);
                }
            }
        }

        private void HandleShowNotification(PipeMessage message)
        {
            var msg = message.Data?.ToString() ?? "Notification";
            _overlayWindow.ShowNotification(msg);
        }

        private void HandleShowWarning(PipeMessage message)
        {
            var msg = message.Data?.ToString() ?? "Warning";
            _overlayWindow.ShowWarning(msg);
        }

        private void HandleShowError(PipeMessage message)
        {
            var msg = message.Data?.ToString() ?? "Error";
            _overlayWindow.ShowError(msg);
        }

        private void HandleShowBlockingScreen(PipeMessage message)
        {
            var msg = message.Data?.ToString() ?? "Screen Locked";
            _overlayWindow.ShowBlockingScreen(msg);
        }

        private void ShowWebOverlay(WebOverlayData data)
        {
            void DoShow()
            {
                HideWebOverlay();
                if (string.IsNullOrWhiteSpace(data.Url))
                {
                    Console.WriteLine("ShowWebOverlay ignored because URL is empty.");
                    return;
                }

                Console.WriteLine($"Showing web overlay: {data.Url} (canClose={data.CanClose})");

                var form = new WebOverlayForm(data.Url, data.CanClose);
                _webOverlay = form;
                form.FormClosed += (_, _) =>
                {
                    if (ReferenceEquals(_webOverlay, form))
                        _webOverlay = null;
                };
                form.Show();
                form.BringToFront();
                form.Activate();
            }

            if (_uiInvoker.InvokeRequired)
                _uiInvoker.BeginInvoke(new Action(DoShow));
            else
                DoShow();
        }

        private void HideWebOverlay()
        {
            var form = _webOverlay;
            if (form == null) return;

            void DoClose()
            {
                try { form.Close(); form.Dispose(); } catch { }
            }

            if (form.InvokeRequired)
                form.BeginInvoke(new Action(DoClose));
            else
                DoClose();

            _webOverlay = null;
        }

        private void OnExit(object? sender, EventArgs e)
        {
            Shutdown();
        }

        private void Shutdown()
        {
            Console.WriteLine("WindowsRemoteToolsUI shutting down.");
            _running = false;
            HideWebOverlay();
            _overlayWindow.Hide();
            _trayIcon.Visible = false;
            _pipeClient.Dispose();
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                HideWebOverlay();
                _overlayWindow.Hide();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _pipeClient.Dispose();
                _uiInvoker.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal static class UIFileLogger
    {
        private static readonly object Sync = new();
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsRemoteTools");
        private static readonly string LogPath = Path.Combine(LogDir, "ui.log");

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                var writer = TextWriter.Synchronized(new StreamWriter(
                    new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    Encoding.UTF8)
                { AutoFlush = true });
                Console.SetOut(new TimestampWriter(writer));
                Console.SetError(new TimestampWriter(writer));
                Console.WriteLine($"UI log started. Log file: {LogPath}");
            }
            catch
            {
            }
        }

        private sealed class TimestampWriter : TextWriter
        {
            private readonly TextWriter _inner;
            public TimestampWriter(TextWriter inner) => _inner = inner;
            public override Encoding Encoding => _inner.Encoding;

            public override void WriteLine(string? value)
            {
                lock (Sync)
                {
                    _inner.WriteLine($"[{DateTime.Now:HH:mm:ss}] {value}");
                }
            }

            public override void Write(string? value)
            {
                lock (Sync)
                {
                    _inner.Write(value);
                }
            }
        }
    }
}
