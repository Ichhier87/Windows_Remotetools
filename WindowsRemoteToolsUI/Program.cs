using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsRemoteToolsUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            var app = new UIApplication();
            Application.Run();
        }
    }

    public class UIApplication
    {
        private readonly UIPipeClient _pipeClient;
        private readonly OverlayWindow _overlayWindow;
        private readonly NotifyIcon _trayIcon;
        private bool _running = true;

        public UIApplication()
        {
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

                    _overlayWindow.Show(overlayData.Message, bgColor, txtColor, overlayData.Opacity);
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

        private void OnExit(object? sender, EventArgs e)
        {
            Shutdown();
        }

        private void Shutdown()
        {
            _running = false;
            _overlayWindow.Hide();
            _trayIcon.Visible = false;
            _pipeClient.Dispose();
            Application.Exit();
        }
    }
}
