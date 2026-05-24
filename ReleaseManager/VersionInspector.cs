using System;
using System.IO;
using Microsoft.Win32;

namespace WindowsRemoteTools.ReleaseManager
{
    /// <summary>
    /// Reads version info from the three sources the release manager cares
    /// about: the VERSION file in the repo (what would be built next), the
    /// Uninstall-registry (what's installed on this dev box), and a remote
    /// manifest URL (what's published — done by CentralServerClient instead).
    /// </summary>
    internal static class VersionInspector
    {
        public static string? ReadVersionFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var s = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            catch { return null; }
        }

        public static void WriteVersionFile(string path, string version)
        {
            File.WriteAllText(path, version + Environment.NewLine);
        }

        /// <summary>
        /// Returns the DisplayVersion of the first registry Uninstall entry
        /// whose DisplayName starts with "Windows Remote Tools", or null.
        /// Scans both 64-bit and 32-bit Uninstall hives.
        /// </summary>
        public static string? ReadInstalledVersion()
        {
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            };
            foreach (var root in roots)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(root);
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using var entry = key.OpenSubKey(sub);
                        if (entry == null) continue;
                        var name = entry.GetValue("DisplayName") as string;
                        if (name != null && name.StartsWith("Windows Remote Tools", StringComparison.OrdinalIgnoreCase))
                        {
                            return entry.GetValue("DisplayVersion") as string;
                        }
                    }
                }
                catch { /* try next hive */ }
            }
            return null;
        }

        /// <summary>
        /// Bumps a "X.Y.Z" version string. Position 0 = major (resets minor+patch),
        /// 1 = minor (resets patch), 2 = patch. Returns null on parse failure.
        /// </summary>
        public static string? Bump(string version, int position)
        {
            if (string.IsNullOrWhiteSpace(version)) return null;
            var parts = version.Split('.');
            if (parts.Length < 3) return null;
            if (!int.TryParse(parts[0], out var a) ||
                !int.TryParse(parts[1], out var b) ||
                !int.TryParse(parts[2], out var c)) return null;
            switch (position)
            {
                case 0: a++; b = 0; c = 0; break;
                case 1: b++; c = 0; break;
                case 2: c++; break;
                default: return null;
            }
            return $"{a}.{b}.{c}";
        }
    }
}
