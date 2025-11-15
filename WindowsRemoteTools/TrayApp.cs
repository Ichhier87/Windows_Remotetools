using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsRemoteTools
{
    /// <summary>
    /// System tray application
    /// </summary>
    public class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly ConfigManager _config;
        private readonly VolumeController _volumeController;
        private readonly OverlayWindow _overlayWindow;
        private readonly WebSocketClient _webSocketClient;

        public TrayApp()
        {
            // Initialize components
            _config = ConfigManager.Load();
            _volumeController = new VolumeController(_config);
            _overlayWindow = new OverlayWindow();
            _webSocketClient = new WebSocketClient(_config, _volumeController, _overlayWindow);

            // Create tray icon
            _notifyIcon = new NotifyIcon
            {
                Icon = CreateIcon(),
                Visible = true,
                Text = "Windows Remote Tools"
            };

            _notifyIcon.ContextMenuStrip = CreateContextMenu();

            // Start components
            _webSocketClient.Start();

            if (_config.EnforceMaxVolume)
            {
                _volumeController.StartMonitoring();
            }

            Console.WriteLine("Tray application started");
        }

        private Icon CreateIcon()
        {
            // Create a simple blue icon
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Blue);
                g.FillRectangle(Brushes.LightBlue, 8, 8, 16, 16);
                g.FillRectangle(Brushes.Blue, 12, 12, 8, 8);
            }

            return Icon.FromHandle(bitmap.GetHicon());
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var menu = new ContextMenuStrip();

            // Status
            menu.Items.Add($"Status: {_webSocketClient.GetStatus()}", null, null).Enabled = false;
            menu.Items.Add(new ToolStripSeparator());

            // WebSocket submenu
            var wsMenu = new ToolStripMenuItem("WebSocket");
            wsMenu.DropDownItems.Add(_webSocketClient.IsConnected ? "Disconnect" : "Connect", null, ToggleWebSocket);
            wsMenu.DropDownItems.Add($"Server: {_config.WebSocketUrl}", null, null).Enabled = false;
            menu.Items.Add(wsMenu);

            // Volume submenu
            var volumeMenu = new ToolStripMenuItem("Volume");
            volumeMenu.DropDownItems.Add($"Current: {_volumeController.GetVolumePercent()}%", null, null).Enabled = false;
            volumeMenu.DropDownItems.Add($"Max Limit: {_config.MaxVolume}%", null, null).Enabled = false;
            volumeMenu.DropDownItems.Add(new ToolStripSeparator());

            var muteItem = new ToolStripMenuItem("Mute", null, ToggleMute)
            {
                Checked = _volumeController.GetMute()
            };
            volumeMenu.DropDownItems.Add(muteItem);

            var enforceItem = new ToolStripMenuItem("Enforce Max Volume", null, ToggleVolumeEnforcement)
            {
                Checked = _config.EnforceMaxVolume
            };
            volumeMenu.DropDownItems.Add(enforceItem);

            volumeMenu.DropDownItems.Add(new ToolStripSeparator());
            volumeMenu.DropDownItems.Add("Set Max to 50%", null, (s, e) => SetMaxVolume(50));
            volumeMenu.DropDownItems.Add("Set Max to 75%", null, (s, e) => SetMaxVolume(75));
            volumeMenu.DropDownItems.Add("Set Max to 100%", null, (s, e) => SetMaxVolume(100));
            menu.Items.Add(volumeMenu);

            // Overlay submenu
            var overlayMenu = new ToolStripMenuItem("Overlay");
            overlayMenu.DropDownItems.Add("Show Test Overlay", null, TestOverlay);
            overlayMenu.DropDownItems.Add("Show Blocking Screen", null, (s, e) => _overlayWindow.ShowBlockingScreen("Test Lock"));
            overlayMenu.DropDownItems.Add("Hide Overlay", null, (s, e) => _overlayWindow.Hide());
            menu.Items.Add(overlayMenu);

            menu.Items.Add(new ToolStripSeparator());

            // Monitoring
            var monitorItem = new ToolStripMenuItem("Volume Monitoring", null, ToggleMonitoring)
            {
                Checked = _volumeController._monitoring
            };
            menu.Items.Add(monitorItem);

            menu.Items.Add(new ToolStripSeparator());

            // Exit
            menu.Items.Add("Exit", null, Exit);

            return menu;
        }

        private void RefreshMenu()
        {
            _notifyIcon.ContextMenuStrip = CreateContextMenu();
        }

        private void ToggleWebSocket(object? sender, EventArgs e)
        {
            if (_webSocketClient.IsConnected)
            {
                _webSocketClient.Stop();
            }
            else
            {
                _webSocketClient.Start();
            }
            RefreshMenu();
        }

        private void ToggleMute(object? sender, EventArgs e)
        {
            _volumeController.ToggleMute();
            RefreshMenu();
        }

        private void ToggleVolumeEnforcement(object? sender, EventArgs e)
        {
            _volumeController.EnableEnforcement(!_config.EnforceMaxVolume);
            RefreshMenu();
        }

        private void SetMaxVolume(int maxVolume)
        {
            _volumeController.SetMaxVolumeLimit(maxVolume);
            RefreshMenu();
        }

        private void ToggleMonitoring(object? sender, EventArgs e)
        {
            if (_volumeController._monitoring)
            {
                _volumeController.StopMonitoring();
            }
            else
            {
                _volumeController.StartMonitoring();
            }
            RefreshMenu();
        }

        private void TestOverlay(object? sender, EventArgs e)
        {
            _overlayWindow.ShowAsync(
                "Test Overlay\n\nThis is a test message",
                Color.DarkBlue,
                Color.White,
                0.9
            );
        }

        private void Exit(object? sender, EventArgs e)
        {
            Console.WriteLine("Shutting down...");

            _webSocketClient?.Dispose();
            _volumeController?.Dispose();
            _overlayWindow?.Hide();

            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();

            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _notifyIcon?.Dispose();
                _webSocketClient?.Dispose();
                _volumeController?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
