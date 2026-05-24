using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Polls the CentralServer for a signed update manifest and applies new MSI
    /// releases via msiexec when one is available.
    ///
    /// Trust model: the manifest carries an Ed25519 signature that we verify
    /// against the public key embedded in this assembly. Compromising the
    /// update server alone is not enough to push malicious code — the attacker
    /// also needs the private signing key, which lives only on the operator's
    /// machine.
    ///
    /// The SIGNED_FIELDS list and the canonical-string format MUST match
    /// CentralServer/updates.js exactly.
    /// </summary>
    public class UpdateChecker : IDisposable
    {
        // Raw 32-byte Ed25519 public key, base64-encoded. Replace this with the
        // output of `node tools/update-cli.js generate-keys` on the server. An
        // empty / placeholder value disables verification (and therefore updates)
        // until a real key is provisioned.
        private const string UpdateSigningPublicKeyBase64 = "REPLACE_ME_WITH_BASE64_ED25519_PUBKEY";

        // Order matters — must match SIGNED_FIELDS in updates.js
        private static readonly string[] SignedFields =
        {
            "schema", "channel", "version", "released", "url", "size", "sha256", "minPriorVersion"
        };

        private readonly ConfigManager _config;
        private readonly HttpClient _http;
        private readonly CancellationTokenSource _cts = new();
        private Task? _loop;

        public UpdateChecker(ConfigManager config)
        {
            _config = config;
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"WindowsRemoteTools/{CurrentVersion()}");
        }

        public void Start()
        {
            if (!_config.UpdatesEnabled || string.IsNullOrWhiteSpace(_config.UpdateManifestUrl))
            {
                Console.WriteLine("[Updater] disabled via config");
                return;
            }
            if (UpdateSigningPublicKeyBase64.StartsWith("REPLACE_ME", StringComparison.Ordinal))
            {
                Console.WriteLine("[Updater] public key not provisioned — refusing to check for updates");
                return;
            }

            _loop = Task.Run(() => LoopAsync(_cts.Token));
            Console.WriteLine($"[Updater] started — polling {_config.UpdateManifestUrl} every {_config.UpdateCheckIntervalHours:F1}h");
        }

        public void Stop()
        {
            try { _cts.Cancel(); } catch { }
            try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
            _http.Dispose();
        }

        // ── Main loop ────────────────────────────────────────────────────────

        private async Task LoopAsync(CancellationToken ct)
        {
            // Light initial delay so we don't compete with normal startup work.
            try { await Task.Delay(TimeSpan.FromMinutes(2), ct); }
            catch (OperationCanceledException) { return; }

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await CheckOnceAsync(ct);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Updater] check failed: {ex.Message}");
                }

                var interval = TimeSpan.FromHours(Math.Max(0.1, _config.UpdateCheckIntervalHours));
                try { await Task.Delay(interval, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task CheckOnceAsync(CancellationToken ct)
        {
            var manifestJson = await _http.GetStringAsync(_config.UpdateManifestUrl, ct);
            var manifest = JObject.Parse(manifestJson);

            if (!VerifySignature(manifest))
            {
                Console.WriteLine("[Updater] manifest signature INVALID — ignoring");
                return;
            }

            var remoteVersion = (string?)manifest["version"] ?? "0.0.0";
            var minPrior = (string?)manifest["minPriorVersion"] ?? "0.0.0";
            var current = CurrentVersion();

            if (!TryParseVersion(remoteVersion, out var remote)
                || !TryParseVersion(current, out var local)
                || !TryParseVersion(minPrior, out var minPriorParsed))
            {
                Console.WriteLine($"[Updater] could not parse versions (remote={remoteVersion}, local={current}, min={minPrior})");
                return;
            }
            if (remote <= local)
            {
                Console.WriteLine($"[Updater] up to date ({current})");
                return;
            }
            if (local < minPriorParsed)
            {
                Console.WriteLine($"[Updater] current {current} below minPriorVersion {minPrior} — manual intervention required");
                return;
            }

            var url = (string?)manifest["url"] ?? "";
            var expectedSha = ((string?)manifest["sha256"] ?? "").ToLowerInvariant();
            var expectedSize = (long?)manifest["size"] ?? 0;
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(expectedSha))
            {
                Console.WriteLine("[Updater] manifest is missing url or sha256");
                return;
            }

            Console.WriteLine($"[Updater] update available: {current} -> {remoteVersion}");

            var msiPath = await DownloadMsiAsync(url, expectedSize, expectedSha, ct);
            if (msiPath == null) return;

            LaunchMsiexec(msiPath);
        }

        // ── Signature verification ───────────────────────────────────────────

        private static bool VerifySignature(JObject manifest)
        {
            try
            {
                var sigToken = manifest["signature"];
                if (sigToken == null) return false;
                var sigBytes = Convert.FromBase64String((string)sigToken!);

                var pubKey = Convert.FromBase64String(UpdateSigningPublicKeyBase64);

                var sb = new StringBuilder();
                for (int i = 0; i < SignedFields.Length; i++)
                {
                    if (i > 0) sb.Append('\n');
                    var tok = manifest[SignedFields[i]];
                    if (tok == null || tok.Type == JTokenType.Null)
                    {
                        Console.WriteLine($"[Updater] signed field missing: {SignedFields[i]}");
                        return false;
                    }
                    sb.Append(SignedFields[i]).Append('=').Append(tok.ToString());
                }
                var canonical = Encoding.UTF8.GetBytes(sb.ToString());

                return Ed25519Verify.Verify(pubKey, canonical, sigBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] signature check error: {ex.Message}");
                return false;
            }
        }

        // ── Download + hash ──────────────────────────────────────────────────

        private async Task<string?> DownloadMsiAsync(string url, long expectedSize, string expectedSha, CancellationToken ct)
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"WindowsRemoteToolsSetup-{Guid.NewGuid():N}.msi");
            try
            {
                Console.WriteLine($"[Updater] downloading {url} -> {tmp}");
                using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var fs = File.Create(tmp);
                    await resp.Content.CopyToAsync(fs, ct);
                }

                var info = new FileInfo(tmp);
                if (expectedSize > 0 && info.Length != expectedSize)
                {
                    Console.WriteLine($"[Updater] size mismatch (got {info.Length}, expected {expectedSize})");
                    SafeDelete(tmp);
                    return null;
                }
                var actualSha = Sha256Hex(tmp);
                if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Updater] sha256 mismatch (got {actualSha}, expected {expectedSha})");
                    SafeDelete(tmp);
                    return null;
                }
                return tmp;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] download failed: {ex.Message}");
                SafeDelete(tmp);
                return null;
            }
        }

        private static string Sha256Hex(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        // ── Apply via msiexec ────────────────────────────────────────────────

        /// <summary>
        /// Launches msiexec as a detached process. The MSI's MajorUpgrade
        /// element + util:CloseApplication stop our running service before
        /// replacing files; once the new files are in place msiexec starts the
        /// service again. We must NOT block on the msiexec process — when the
        /// SCM stops us our process exits and msiexec continues independently.
        /// </summary>
        private static void LaunchMsiexec(string msiPath)
        {
            // Log file lives next to the MSI so post-mortem debugging is easy.
            var logPath = Path.ChangeExtension(msiPath, ".install.log");
            var args = $"/i \"{msiPath}\" /quiet /qn /norestart /L*v \"{logPath}\"";

            try
            {
                Console.WriteLine($"[Updater] launching msiexec: {args}");
                var psi = new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using var p = Process.Start(psi);
                // Intentionally don't WaitForExit — msiexec will stop our service.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] msiexec launch failed: {ex.Message}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string CurrentVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var fileVersion = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (!string.IsNullOrEmpty(fileVersion) && fileVersion != "0.0.0.0") return fileVersion!;
            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }

        /// <summary>
        /// Lightweight semver-ish comparison. Accepts 1.2.3 or 1.2.3.4 and pads
        /// missing segments with zero.
        /// </summary>
        private static bool TryParseVersion(string s, out long packed)
        {
            packed = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var parts = s.Trim().Split('.');
            if (parts.Length < 1 || parts.Length > 4) return false;
            int a = 0, b = 0, c = 0, d = 0;
            try
            {
                if (parts.Length > 0) a = int.Parse(parts[0]);
                if (parts.Length > 1) b = int.Parse(parts[1]);
                if (parts.Length > 2) c = int.Parse(parts[2]);
                if (parts.Length > 3) d = int.Parse(parts[3]);
            }
            catch { return false; }
            if (a < 0 || b < 0 || c < 0 || d < 0) return false;
            packed = ((long)a << 48) | ((long)b << 32) | ((long)c << 16) | (long)d;
            return true;
        }
    }
}
