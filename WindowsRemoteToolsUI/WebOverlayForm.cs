using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WindowsRemoteToolsUI
{
    /// <summary>
    /// Fullscreen, always-on-top WebView-based overlay used to render web pages
    /// pushed via the CentralServer DEVICE_MESSAGE command — most importantly the
    /// vocable trainer (https://server/overlay/vocable-trainer?token=...).
    ///
    /// Behaviour mirrors the Android WindowMagic LINK overlay:
    ///  * URLs starting with "remotetool://close" close the overlay
    ///  * URLs starting with "remotetool://minimize?duration=MS" hide it for that
    ///    duration and then re-show it
    ///  * ESC closes the overlay only if CanClose is true (assignments may forbid it)
    ///
    /// WebView2 needs the Edge WebView2 Runtime; if initialization fails the form
    /// falls back to launching the URL in the system default browser.
    /// </summary>
    public class WebOverlayForm : Form
    {
        private readonly string _url;
        private readonly bool _canClose;
        private WebView2? _webView;
        private System.Windows.Forms.Timer? _minimizeTimer;
        private bool _initFailed;

        public WebOverlayForm(string url, bool canClose)
        {
            _url = url;
            _canClose = canClose;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            BackColor = System.Drawing.Color.Black;
            KeyPreview = true;

            KeyDown += OnKeyDown;
            Load += async (_, _) => await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                _webView = new WebView2 { Dock = DockStyle.Fill };
                Controls.Add(_webView);

                // Use a per-user data folder so the runtime works without admin rights.
                var dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WindowsRemoteTools", "WebView2");
                Directory.CreateDirectory(dataDir);

                var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.NavigationStarting += OnNavigationStarting;
                _webView.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                _initFailed = true;
                Console.WriteLine($"WebView2 init failed, falling back to default browser: {ex.Message}");
                FallbackToBrowser();
                // Closing the form schedules disposal on the message loop.
                BeginInvoke(new Action(Close));
            }
        }

        private void FallbackToBrowser()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = _url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fallback browser launch failed: {ex.Message}");
            }
        }

        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var target = e.Uri ?? "";

            if (target.StartsWith("remotetool://close", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                BeginInvoke(new Action(() => Close()));
                return;
            }

            if (target.StartsWith("remotetool://minimize", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                long durationMs = 30000;
                try
                {
                    var uri = new Uri(target);
                    var qs = uri.Query.TrimStart('?');
                    foreach (var part in qs.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = part.Split('=', 2);
                        if (kv.Length == 2 && kv[0].Equals("duration", StringComparison.OrdinalIgnoreCase)
                            && long.TryParse(kv[1], out var ms))
                        {
                            durationMs = ms;
                            break;
                        }
                    }
                }
                catch { /* fall back to default duration */ }

                BeginInvoke(new Action(() => Minimize(durationMs)));
            }
        }

        private void Minimize(long durationMs)
        {
            Visible = false;
            _minimizeTimer?.Stop();
            _minimizeTimer = new System.Windows.Forms.Timer { Interval = (int)Math.Min(durationMs, int.MaxValue) };
            _minimizeTimer.Tick += (_, _) =>
            {
                _minimizeTimer?.Stop();
                _minimizeTimer?.Dispose();
                _minimizeTimer = null;
                Visible = true;
                BringToFront();
                Activate();
            };
            _minimizeTimer.Start();
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _canClose)
                Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _minimizeTimer?.Stop();
                _minimizeTimer?.Dispose();
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }

        public bool InitFailed => _initFailed;
    }
}
