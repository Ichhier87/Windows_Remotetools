using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WindowsRemoteToolsUI
{
    /// <summary>
    /// Fullscreen overlay window for displaying messages
    /// </summary>
    public class OverlayWindow
    {
        private Form? _currentForm;
        private readonly object _lock = new object();
        private Form? _svgForm;
        private readonly object _svgLock = new object();

        public void Show(string message = "", Color? backgroundColor = null, Color? textColor = null, double opacity = 0.9)
        {
            // Must run on UI thread
            if (_currentForm?.InvokeRequired ?? false)
            {
                _currentForm.Invoke(new Action(() => Show(message, backgroundColor, textColor, opacity)));
                return;
            }

            Hide();

            var bgColor = backgroundColor ?? Color.Black;
            var txtColor = textColor ?? Color.White;

            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                BackColor = bgColor,
                Opacity = opacity,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds
            };

            // Main panel
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = bgColor
            };

            if (!string.IsNullOrEmpty(message))
            {
                var label = new Label
                {
                    Text = message,
                    Font = new Font("Arial", 48, FontStyle.Bold),
                    ForeColor = txtColor,
                    BackColor = bgColor,
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill
                };
                panel.Controls.Add(label);
            }

            // Close instruction
            var closeLabel = new Label
            {
                Text = "Press ESC or click to close",
                Font = new Font("Arial", 16),
                ForeColor = txtColor,
                BackColor = bgColor,
                AutoSize = true,
                TextAlign = ContentAlignment.BottomCenter,
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(0, 0, 0, 20)
            };
            panel.Controls.Add(closeLabel);

            form.Controls.Add(panel);

            // Event handlers
            form.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                    Hide();
            };

            form.Click += (s, e) => Hide();
            panel.Click += (s, e) => Hide();

            lock (_lock)
            {
                _currentForm = form;
            }

            form.Show();
        }

        public void ShowAsync(string message = "", Color? backgroundColor = null, Color? textColor = null, double opacity = 0.9)
        {
            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                Show(message, backgroundColor, textColor, opacity);
                Application.Run();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        public void Hide()
        {
            lock (_lock)
            {
                if (_currentForm != null)
                {
                    try
                    {
                        if (_currentForm.InvokeRequired)
                        {
                            _currentForm.Invoke(new Action(() =>
                            {
                                _currentForm.Close();
                                _currentForm.Dispose();
                            }));
                        }
                        else
                        {
                            _currentForm.Close();
                            _currentForm.Dispose();
                        }
                    }
                    catch { }
                    finally
                    {
                        _currentForm = null;
                    }
                }
            }
        }

        public void ShowBlockingScreen(string message = "Screen Locked")
        {
            ShowAsync(message, Color.Black, Color.Red, 0.95);
        }

        public void ShowLockedScreen(string message, string passwordHash, Action? onUnlocked = null)
        {
            Hide();
            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                var form = new LockedOverlayForm(message, passwordHash, onUnlocked);
                Application.Run(form);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        public void ShowNotification(string message)
        {
            ShowAsync(message, Color.Blue, Color.White, 0.7);
        }

        public void ShowWarning(string message)
        {
            ShowAsync(message, Color.Orange, Color.Black, 0.85);
        }

        public void ShowError(string message)
        {
            ShowAsync(message, Color.Red, Color.White, 0.9);
        }

        public void ShowSvgOverlay(SvgOverlayData data)
        {
            HideSvgOverlay();

            var thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                var form = new SvgOverlayForm(data);
                lock (_svgLock)
                {
                    _svgForm = form;
                }
                form.FormClosed += (s, e) =>
                {
                    lock (_svgLock)
                    {
                        if (_svgForm == form)
                            _svgForm = null;
                    }
                };
                Application.Run(form);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        public void HideSvgOverlay()
        {
            Form? f;
            lock (_svgLock)
            {
                f = _svgForm;
                _svgForm = null;
            }
            if (f == null) return;
            try
            {
                if (f.InvokeRequired)
                    f.Invoke(new Action(() => { f.Close(); f.Dispose(); }));
                else
                { f.Close(); f.Dispose(); }
            }
            catch { }
        }

        public void ShowPicture(string imagePath)
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();

                    if (!File.Exists(imagePath))
                    {
                        ShowError("Picture not found");
                        return;
                    }

                    Hide();

                    var form = new Form
                    {
                        FormBorderStyle = FormBorderStyle.None,
                        WindowState = FormWindowState.Maximized,
                        TopMost = true,
                        BackColor = Color.Black,
                        ShowInTaskbar = false,
                        StartPosition = FormStartPosition.Manual,
                        Bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds
                    };

                    var pictureBox = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        BackColor = Color.Black,
                        SizeMode = PictureBoxSizeMode.Zoom
                    };

                    try { pictureBox.Image = Image.FromFile(imagePath); }
                    catch { ShowError("Error loading image"); return; }

                    form.Controls.Add(pictureBox);
                    form.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
                    form.Click += (s, e) => Hide();
                    pictureBox.Click += (s, e) => Hide();

                    lock (_lock) { _currentForm = form; }

                    form.Show();
                    Application.Run(form);
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
