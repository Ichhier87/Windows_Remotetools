using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace WindowsRemoteTools.ReleaseManager
{
    /// <summary>
    /// Single-window release dashboard:
    ///   - shows current state across three sources (VERSION file, installed
    ///     version on this box, currently-published version on the server)
    ///   - lets the operator bump the version
    ///   - one-button build (Build-Installer.ps1) and upload+publish
    ///   - bootstraps the signing keypair on the server when needed
    /// All long-running work is async so the UI stays responsive; log output
    /// streams into the bottom panel via Invoke().
    /// </summary>
    public class MainForm : Form
    {
        // ── Status row widgets ─────────────────────────────────────────────
        private readonly Label _versionFileVal = NewValue();
        private readonly Label _installedVal = NewValue();
        private readonly Label _publishedVal = NewValue();
        private readonly Label _serverStateVal = NewValue();
        private readonly Button _refreshBtn = new() { Text = "Refresh", AutoSize = true };

        // ── Settings ───────────────────────────────────────────────────────
        private readonly TextBox _serverUrlBox = NewTextBox(width: 260);
        private readonly TextBox _repoRootBox = NewTextBox(width: 360);
        private readonly TextBox _usernameBox = NewTextBox(width: 160);
        private readonly TextBox _passwordBox = NewTextBox(width: 160);
        private readonly Button _saveSettingsBtn = new() { Text = "Save settings", AutoSize = true };

        // ── Release inputs ─────────────────────────────────────────────────
        private readonly TextBox _newVersionBox = NewTextBox(width: 120);
        private readonly Button _bumpPatchBtn = new() { Text = "+ Patch", AutoSize = true };
        private readonly Button _bumpMinorBtn = new() { Text = "+ Minor", AutoSize = true };
        private readonly Button _bumpMajorBtn = new() { Text = "+ Major", AutoSize = true };
        private readonly TextBox _notesBox = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 60,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
        };

        // ── Action buttons ─────────────────────────────────────────────────
        private readonly Button _buildBtn = new() { Text = "1) Build MSI", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        private readonly Button _publishBtn = new() { Text = "2) Upload + Sign + Publish", AutoSize = true, Padding = new Padding(8, 4, 8, 4) };
        private readonly Button _doAllBtn = new()
        {
            Text = "▶  DO ALL  (write VERSION → build → publish)",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            BackColor = Color.FromArgb(0x2E, 0x7D, 0x32),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        private readonly Button _genKeysBtn = new() { Text = "Generate signing keys (one-time)", AutoSize = true };
        private readonly Button _copyPubKeyBtn = new() { Text = "Copy public key", AutoSize = true, Enabled = false };
        private readonly Button _openMsiFolderBtn = new() { Text = "Open MSI folder", AutoSize = true };

        // ── Log ────────────────────────────────────────────────────────────
        private readonly TextBox _log = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            WordWrap = false,
        };

        private ReleaseSettings _settings = new();
        private string? _serverPublicKeyB64;

        public MainForm()
        {
            Text = "Windows Remote Tools — Release Manager";
            Size = new Size(960, 820);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(820, 600);

            _passwordBox.PasswordChar = '•';

            BuildLayout();
            WireEvents();

            _settings = ReleaseSettings.Load();
            ApplySettingsToUI();
            _ = RefreshStateAsync();
        }

        // ── Layout ─────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(12),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(BuildStatusBox(), 0, 0);
            root.Controls.Add(BuildSettingsBox(), 0, 1);
            root.Controls.Add(BuildReleaseBox(), 0, 2);
            root.Controls.Add(BuildActionsBox(), 0, 3);
            var logBox = BuildLogBox();
            logBox.Dock = DockStyle.Fill;
            root.Controls.Add(logBox, 0, 4);
            for (int i = 0; i < 4; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            Controls.Add(root);
        }

        private GroupBox BuildStatusBox()
        {
            var box = new GroupBox
            {
                Text = "Status",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(8),
            };
            var grid = new TableLayoutPanel
            {
                ColumnCount = 3,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(new Label { Text = "VERSION file (next build):", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 0);
            grid.Controls.Add(_versionFileVal, 1, 0);
            grid.Controls.Add(new Label { Text = "Installed on this PC:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 1);
            grid.Controls.Add(_installedVal, 1, 1);
            grid.Controls.Add(new Label { Text = "Published on server:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 2);
            grid.Controls.Add(_publishedVal, 1, 2);
            grid.Controls.Add(new Label { Text = "Server signing keys:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 3);
            grid.Controls.Add(_serverStateVal, 1, 3);
            grid.Controls.Add(_refreshBtn, 2, 0);
            box.Controls.Add(grid);
            return box;
        }

        private GroupBox BuildSettingsBox()
        {
            var box = new GroupBox
            {
                Text = "Server & Repo",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(8),
            };
            var grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(new Label { Text = "Server URL:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 0);
            grid.Controls.Add(_serverUrlBox, 1, 0);
            grid.Controls.Add(new Label { Text = "Repo root:", AutoSize = true, Padding = new Padding(16, 4, 8, 4) }, 2, 0);
            grid.Controls.Add(_repoRootBox, 3, 0);
            grid.Controls.Add(new Label { Text = "Username:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 1);
            grid.Controls.Add(_usernameBox, 1, 1);
            grid.Controls.Add(new Label { Text = "Password:", AutoSize = true, Padding = new Padding(16, 4, 8, 4) }, 2, 1);
            grid.Controls.Add(_passwordBox, 3, 1);
            grid.Controls.Add(_saveSettingsBtn, 3, 2);
            box.Controls.Add(grid);
            return box;
        }

        private GroupBox BuildReleaseBox()
        {
            var box = new GroupBox
            {
                Text = "Next release",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(8),
            };
            var grid = new TableLayoutPanel
            {
                ColumnCount = 6,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            for (int i = 0; i < 5; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(new Label { Text = "Version:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 0);
            grid.Controls.Add(_newVersionBox, 1, 0);
            grid.Controls.Add(_bumpPatchBtn, 2, 0);
            grid.Controls.Add(_bumpMinorBtn, 3, 0);
            grid.Controls.Add(_bumpMajorBtn, 4, 0);
            grid.Controls.Add(new Label
            {
                Text = "(writes VERSION-file when build/publish is triggered)",
                AutoSize = true,
                ForeColor = Color.Gray,
                Padding = new Padding(8, 6, 0, 0)
            }, 5, 0);
            grid.Controls.Add(new Label { Text = "Notes:", AutoSize = true, Padding = new Padding(0, 4, 8, 4) }, 0, 1);
            grid.SetColumnSpan(_notesBox, 5);
            _notesBox.Dock = DockStyle.Top;
            _notesBox.Width = 600;
            grid.Controls.Add(_notesBox, 1, 1);
            box.Controls.Add(grid);
            return box;
        }

        private GroupBox BuildActionsBox()
        {
            var box = new GroupBox
            {
                Text = "Actions",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Padding = new Padding(8),
            };
            var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
            row1.Controls.Add(_doAllBtn);
            row1.Controls.Add(new Label { Text = "  or step-by-step:  ", AutoSize = true, Padding = new Padding(12, 8, 4, 0), ForeColor = Color.Gray });
            row1.Controls.Add(_buildBtn);
            row1.Controls.Add(_publishBtn);

            var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false };
            row2.Controls.Add(_genKeysBtn);
            row2.Controls.Add(_copyPubKeyBtn);
            row2.Controls.Add(_openMsiFolderBtn);

            var outer = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            outer.Controls.Add(row1, 0, 0);
            outer.Controls.Add(row2, 0, 1);
            box.Controls.Add(outer);
            return box;
        }

        private GroupBox BuildLogBox()
        {
            var box = new GroupBox { Text = "Log", Padding = new Padding(8) };
            box.Controls.Add(_log);
            return box;
        }

        private void WireEvents()
        {
            _refreshBtn.Click += async (_, _) => await RefreshStateAsync();
            _saveSettingsBtn.Click += (_, _) => SaveSettingsFromUI();
            _bumpPatchBtn.Click += (_, _) => BumpAndShow(2);
            _bumpMinorBtn.Click += (_, _) => BumpAndShow(1);
            _bumpMajorBtn.Click += (_, _) => BumpAndShow(0);
            _buildBtn.Click += async (_, _) => await SafeRun(BuildOnlyAsync);
            _publishBtn.Click += async (_, _) => await SafeRun(PublishOnlyAsync);
            _doAllBtn.Click += async (_, _) => await SafeRun(DoAllAsync);
            _genKeysBtn.Click += async (_, _) => await SafeRun(GenerateKeysAsync);
            _copyPubKeyBtn.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(_serverPublicKeyB64))
                {
                    Clipboard.SetText(_serverPublicKeyB64!);
                    WriteLog("Public key copied to clipboard.");
                }
            };
            _openMsiFolderBtn.Click += (_, _) =>
            {
                var dir = _settings.MsiOutputDirectory;
                if (dir != null && Directory.Exists(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
                else
                    WriteLog($"MSI output dir not found: {dir}");
            };
        }

        // ── Settings <-> UI ────────────────────────────────────────────────

        private void ApplySettingsToUI()
        {
            _serverUrlBox.Text = _settings.ServerBaseUrl;
            _repoRootBox.Text = _settings.RepoRoot;
            _usernameBox.Text = _settings.Username;
            _passwordBox.Text = _settings.GetPassword();
            _notesBox.Text = _settings.DefaultNotes;
        }

        private void SaveSettingsFromUI()
        {
            _settings.ServerBaseUrl = _serverUrlBox.Text.Trim();
            _settings.RepoRoot = _repoRootBox.Text.Trim();
            _settings.Username = _usernameBox.Text.Trim();
            _settings.SetPassword(_passwordBox.Text);
            _settings.DefaultNotes = _notesBox.Text;
            // Auto-derive manifest URL from the server URL — the GUI never asks
            // the operator to type two URLs that need to stay in sync.
            _settings.ManifestUrl = _settings.ServerBaseUrl.TrimEnd('/') + "/updates/manifest.json";
            _settings.Save();
            WriteLog("Settings saved.");
        }

        // ── State refresh ──────────────────────────────────────────────────

        private async Task RefreshStateAsync()
        {
            var vf = VersionInspector.ReadVersionFile(_settings.VersionFile);
            _versionFileVal.Text = vf ?? "(missing)";
            if (string.IsNullOrWhiteSpace(_newVersionBox.Text) && vf != null)
                _newVersionBox.Text = vf;

            _installedVal.Text = VersionInspector.ReadInstalledVersion() ?? "(not installed)";

            // Public manifest fetch — works without login.
            using var client = new CentralServerClient(_settings.ServerBaseUrl);
            var manifest = await client.GetPublicManifestAsync(_settings.ManifestUrl);
            _publishedVal.Text = manifest?["version"]?.ToString() ?? "(no manifest yet)";

            // Server signing-key state — also accessible without login via
            // /updates/public-key.pem. We just check the public URL.
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var resp = await http.GetAsync(_settings.ServerBaseUrl.TrimEnd('/') + "/updates/public-key.pem");
                _serverStateVal.Text = resp.IsSuccessStatusCode ? "present" : "missing (run Generate keys)";
            }
            catch (Exception ex)
            {
                _serverStateVal.Text = $"unreachable ({ex.Message})";
            }
        }

        // ── Actions ────────────────────────────────────────────────────────

        private void BumpAndShow(int position)
        {
            var basis = _newVersionBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(basis))
                basis = VersionInspector.ReadVersionFile(_settings.VersionFile) ?? "1.0.0";
            var next = VersionInspector.Bump(basis, position);
            if (next == null)
            {
                WriteLog($"Cannot parse version '{basis}'");
                return;
            }
            _newVersionBox.Text = next;
        }

        private async Task BuildOnlyAsync()
        {
            var v = await PrepareVersionAsync();
            if (v == null) return;
            await RunBuildAsync(v);
        }

        private async Task PublishOnlyAsync()
        {
            var v = _newVersionBox.Text.Trim();
            if (!IsValidVersion(v))
            {
                WriteLog($"Invalid version: '{v}'");
                return;
            }
            if (!File.Exists(_settings.MsiOutputPath))
            {
                WriteLog($"MSI not found: {_settings.MsiOutputPath}");
                WriteLog("Run step 1 (Build MSI) first.");
                return;
            }
            await PublishAsync(v);
            await RefreshStateAsync();
        }

        private async Task DoAllAsync()
        {
            var v = await PrepareVersionAsync();
            if (v == null) return;
            var built = await RunBuildAsync(v);
            if (!built) return;
            await PublishAsync(v);
            await RefreshStateAsync();
        }

        /// <summary>
        /// Validates the version-input field, writes it into the VERSION file
        /// so the build step picks it up, and returns the version to use. Null
        /// on input error.
        /// </summary>
        private Task<string?> PrepareVersionAsync()
        {
            var v = _newVersionBox.Text.Trim();
            if (!IsValidVersion(v))
            {
                WriteLog($"Invalid version: '{v}' (expected X.Y.Z)");
                return Task.FromResult<string?>(null);
            }
            try
            {
                VersionInspector.WriteVersionFile(_settings.VersionFile, v);
                WriteLog($"VERSION -> {v}");
                _versionFileVal.Text = v;
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to write VERSION: {ex.Message}");
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(v);
        }

        private async Task<bool> RunBuildAsync(string version)
        {
            if (!File.Exists(_settings.BuildScript))
            {
                WriteLog($"Build-Installer.ps1 not found at {_settings.BuildScript}");
                return false;
            }
            WriteLog("─── Building MSI ───");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{_settings.BuildScript}\" -Version \"{version}\" -NoOpenPrompt",
                WorkingDirectory = _settings.RepoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var exit = await RunProcessStreamingAsync(psi);
            if (exit != 0)
            {
                WriteLog($"Build failed (exit {exit}).");
                return false;
            }
            if (!File.Exists(_settings.MsiOutputPath))
            {
                WriteLog($"Build returned 0 but MSI missing at {_settings.MsiOutputPath}");
                return false;
            }
            WriteLog($"Built: {_settings.MsiOutputPath}");
            return true;
        }

        private async Task PublishAsync(string version)
        {
            WriteLog("─── Publishing to server ───");
            using var client = new CentralServerClient(_settings.ServerBaseUrl);

            try
            {
                await client.LoginAsync(_settings.Username, _passwordBox.Text);
                WriteLog($"Login OK as {_settings.Username}");
            }
            catch (Exception ex)
            {
                WriteLog($"Login failed: {ex.Message}");
                return;
            }

            try
            {
                var result = await client.PublishAsync(_settings.MsiOutputPath, version, _notesBox.Text, null);
                WriteLog("Publish OK:");
                WriteLog(result.ToString());
            }
            catch (Exception ex)
            {
                WriteLog($"Publish failed: {ex.Message}");
            }
        }

        private async Task GenerateKeysAsync()
        {
            using var client = new CentralServerClient(_settings.ServerBaseUrl);
            try
            {
                await client.LoginAsync(_settings.Username, _passwordBox.Text);
            }
            catch (Exception ex)
            {
                WriteLog($"Login failed: {ex.Message}");
                return;
            }
            try
            {
                var result = await client.GenerateKeysAsync();
                _serverPublicKeyB64 = (string?)result["publicKeyRawBase64"];
                if (!string.IsNullOrEmpty(_serverPublicKeyB64))
                {
                    WriteLog("Signing keypair generated on server.");
                    WriteLog($"Public key (base64): {_serverPublicKeyB64}");
                    WriteLog("Paste this into WindowsRemoteTools/UpdateChecker.cs → UpdateSigningPublicKeyBase64");
                    _copyPubKeyBtn.Enabled = true;
                }
                else
                {
                    WriteLog($"Unexpected response: {result}");
                }
                await RefreshStateAsync();
            }
            catch (Exception ex)
            {
                WriteLog($"Generate keys failed: {ex.Message}");
            }
        }

        // ── Process plumbing ───────────────────────────────────────────────

        private async Task<int> RunProcessStreamingAsync(ProcessStartInfo psi)
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data != null) WriteLog(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data != null) WriteLog(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync();
            return p.ExitCode;
        }

        private async Task SafeRun(Func<Task> action)
        {
            try
            {
                SetActionsEnabled(false);
                await action();
            }
            catch (Exception ex)
            {
                WriteLog($"Unhandled error: {ex.Message}");
            }
            finally
            {
                SetActionsEnabled(true);
            }
        }

        private void SetActionsEnabled(bool enabled)
        {
            foreach (var b in new[] { _buildBtn, _publishBtn, _doAllBtn, _genKeysBtn, _refreshBtn, _saveSettingsBtn })
                b.Enabled = enabled;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static bool IsValidVersion(string v) =>
            System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+\.\d+\.\d+(\.\d+)?$");

        private void WriteLog(string line)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(WriteLog), line); return; }
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
        }

        private static Label NewValue() => new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Padding = new Padding(0, 4, 16, 4) };
        private static TextBox NewTextBox(int width) => new() { Width = width };
    }
}
