using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Non-dismissable fullscreen lock screen. Only Ctrl+Alt+P + correct password closes it.
    /// </summary>
    public class LockedOverlayForm : Form
    {
        private const int HOTKEY_ID = 1;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_P = 0x50;
        private const int WM_HOTKEY = 0x0312;

        private readonly string _passwordHash;
        private readonly Action? _onUnlocked;
        private bool _allowClose = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public LockedOverlayForm(string message, string passwordHash, Action? onUnlocked = null)
        {
            _passwordHash = passwordHash;
            _onUnlocked = onUnlocked;

            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            BackColor = System.Drawing.Color.Black;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds
                ?? System.Windows.Forms.Screen.AllScreens[0].Bounds;

            var panel = new Panel { Dock = DockStyle.Fill, BackColor = System.Drawing.Color.Black };

            var hintLabel = new Label
            {
                Text = "Press Ctrl+Alt+P to unlock",
                Font = new System.Drawing.Font("Arial", 16),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Black,
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 50
            };

            var messageLabel = new Label
            {
                Text = message,
                Font = new System.Drawing.Font("Arial", 48, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.Red,
                BackColor = System.Drawing.Color.Black,
                AutoSize = false,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };

            panel.Controls.Add(messageLabel);
            panel.Controls.Add(hintLabel);
            Controls.Add(panel);

            FormClosing += (s, e) => { if (!_allowClose) e.Cancel = true; };
            FormClosed += (s, e) => UnregisterHotKey(Handle, HOTKEY_ID);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, VK_P);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                ShowPasswordDialog();
                return;
            }
            base.WndProc(ref m);
        }

        private void ShowPasswordDialog()
        {
            using var dialog = new Form
            {
                Text = "Unlock Screen",
                Size = new System.Drawing.Size(360, 160),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var passwordBox = new TextBox
            {
                PasswordChar = '*',
                Location = new System.Drawing.Point(20, 35),
                Width = 305,
                Font = new System.Drawing.Font("Arial", 14)
            };

            var okButton = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(130, 80),
                Width = 100,
                DialogResult = DialogResult.OK
            };

            dialog.Controls.Add(passwordBox);
            dialog.Controls.Add(okButton);
            dialog.AcceptButton = okButton;

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var hash = ComputeSha256(passwordBox.Text);
                if (string.Equals(hash, _passwordHash, StringComparison.OrdinalIgnoreCase))
                {
                    _onUnlocked?.Invoke();
                    _allowClose = true;
                    Close();
                }
            }
        }

        private static string ComputeSha256(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
