using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace WindowsRemoteTools.ReleaseManager
{
    /// <summary>
    /// User-level persisted settings for the release manager — server URL,
    /// repo location, last-used username, and the password encrypted with
    /// Windows DPAPI for the current user.
    /// </summary>
    public class ReleaseSettings
    {
        public string ServerBaseUrl { get; set; } = "https://dennislewin.de";
        public string ManifestUrl { get; set; } = "https://dennislewin.de/updates/manifest.json";
        public string RepoRoot { get; set; } = @"C:\Programming\Windows_Remotetools";
        public string Username { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
        public string DefaultNotes { get; set; } = "";

        [JsonIgnore]
        public string BuildScript => Path.Combine(RepoRoot, "Build-Installer.ps1");

        [JsonIgnore]
        public string VersionFile => Path.Combine(RepoRoot, "VERSION");

        [JsonIgnore]
        public string MsiOutputPath => ResolveMsiOutputPath();

        [JsonIgnore]
        public string MsiOutputDirectory => Path.GetDirectoryName(MsiOutputPath)
            ?? Path.Combine(RepoRoot, "Installer", "bin", "Release");

        private string[] MsiOutputCandidates => new[]
        {
            Path.Combine(RepoRoot, "Installer", "bin", "Release", "WindowsRemoteToolsSetup.msi"),
            Path.Combine(RepoRoot, "Installer", "bin", "x64", "Release", "WindowsRemoteToolsSetup.msi")
        };

        private static string SettingsPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WindowsRemoteToolsRelease");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
        }

        public static ReleaseSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var s = JsonConvert.DeserializeObject<ReleaseSettings>(File.ReadAllText(SettingsPath));
                    if (s != null) return s;
                }
            }
            catch { /* fall through to defaults */ }
            return new ReleaseSettings();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { /* best-effort */ }
        }

        public string GetPassword()
        {
            if (string.IsNullOrWhiteSpace(EncryptedPassword))
                return "";

            try
            {
                var protectedBytes = Convert.FromBase64String(EncryptedPassword);
                var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        public void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                EncryptedPassword = "";
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(password);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            EncryptedPassword = Convert.ToBase64String(protectedBytes);
        }

        private string ResolveMsiOutputPath()
        {
            var existing = MsiOutputCandidates
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            return existing?.FullName ?? MsiOutputCandidates[0];
        }
    }
}
