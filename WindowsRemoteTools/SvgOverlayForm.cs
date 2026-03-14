using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Positioned floating overlay window that renders SVG or styled text via WebBrowser.
    /// </summary>
    public class SvgOverlayForm : Form
    {
        private readonly bool _clickThrough;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public SvgOverlayForm(SvgOverlayData data)
        {
            _clickThrough = data.ClickThrough;

            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Opacity = Math.Clamp(data.Opacity, 0.0, 1.0);
            Width = Math.Max(data.Width, 10);
            Height = Math.Max(data.Height, 10);

            try { BackColor = ColorTranslator.FromHtml(data.BackgroundColor); }
            catch { BackColor = Color.Black; }

            var screen = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            var pos = CalculatePosition(data, screen);
            Left = pos.X;
            Top = pos.Y;

            var browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScrollBarsEnabled = false,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = false,
                AllowNavigation = false,
                AllowWebBrowserDrop = false,
            };
            Controls.Add(browser);
            browser.DocumentText = BuildHtml(data);

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (_clickThrough)
                    cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                return cp;
            }
        }

        private static Point CalculatePosition(SvgOverlayData data, Rectangle screen)
        {
            int x, y;
            switch ((data.Gravity ?? "TOP_LEFT").ToUpperInvariant())
            {
                case "TOP_RIGHT":
                    x = screen.Width - data.Width - data.X;
                    y = data.Y;
                    break;
                case "BOTTOM_LEFT":
                    x = data.X;
                    y = screen.Height - data.Height - data.Y;
                    break;
                case "BOTTOM_RIGHT":
                    x = screen.Width - data.Width - data.X;
                    y = screen.Height - data.Height - data.Y;
                    break;
                case "CENTER":
                    x = (screen.Width - data.Width) / 2 + data.X;
                    y = (screen.Height - data.Height) / 2 + data.Y;
                    break;
                case "TOP":
                    x = (screen.Width - data.Width) / 2 + data.X;
                    y = data.Y;
                    break;
                case "BOTTOM":
                    x = (screen.Width - data.Width) / 2 + data.X;
                    y = screen.Height - data.Height - data.Y;
                    break;
                default: // TOP_LEFT
                    x = data.X;
                    y = data.Y;
                    break;
            }
            return new Point(screen.Left + x, screen.Top + y);
        }

        private static string BuildHtml(SvgOverlayData data)
        {
            string bg = data.BackgroundColor ?? "#000000";
            string borderCss = data.BorderWidth > 0
                ? $"border:{data.BorderWidth}px solid {data.BorderColor ?? "#000000"};"
                : "border:none;";

            string bodyContent;
            if (!string.IsNullOrEmpty(data.Svg))
            {
                bodyContent = data.Svg;
            }
            else
            {
                string textColor = data.TextColor ?? "#FFFFFF";
                string text = HtmlEncode(data.Text ?? "");
                bodyContent = $"<p style=\"color:{textColor};font-family:Arial,sans-serif;font-size:{data.FontSize}px;margin:0;padding:8px;word-wrap:break-word;\">{text}</p>";
            }

            return $@"<!DOCTYPE html><html>
<head>
<meta http-equiv=""X-UA-Compatible"" content=""IE=Edge"">
<style>
*{{margin:0;padding:0;box-sizing:border-box;}}
html,body{{
  width:{data.Width}px;height:{data.Height}px;
  overflow:hidden;background:{bg};
  {borderCss}
  display:flex;align-items:center;justify-content:center;
}}
svg{{max-width:100%;max-height:100%;}}
</style>
</head>
<body>{bodyContent}</body>
</html>";
        }

        private static string HtmlEncode(string text) =>
            text.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
    }
}
