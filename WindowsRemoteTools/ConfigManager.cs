using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using Newtonsoft.Json;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Configuration manager for the application
    /// </summary>
    public class ConfigManager
    {
        private const string ConfigFileName = "config.json";

        // WebSocket connection settings
        public string WsHost { get; set; } = "localhost";
        public int WsPort { get; set; } = 9090;
        public bool WsUseSSL { get; set; } = true;

        // Device identification
        public string DeviceName { get; set; } = Environment.MachineName;
        public string DeviceId { get; set; } = GetMachineId();

        // Authentication (optional)
        public string? Username { get; set; }
        public string? Password { get; set; }

        // Volume control settings
        public int MaxVolume { get; set; } = 100;
        public double VolumeCheckInterval { get; set; } = 1.0;
        public bool EnforceMaxVolume { get; set; } = true;

        // SSL/TLS settings
        public bool IgnoreSslErrors { get; set; } = true; // For self-signed certificates

        [JsonIgnore]
        public string WebSocketUrl => $"{(WsUseSSL ? "wss" : "ws")}://{WsHost}:{WsPort}";

        [JsonIgnore]
        public string LocalIpAddress => GetLocalIPAddress();

        [JsonIgnore]
        public string DeviceType => "windows";

        [JsonIgnore]
        public string Platform => Environment.OSVersion.Platform.ToString();

        [JsonIgnore]
        public string OSVersion => Environment.OSVersion.VersionString;

        public static ConfigManager Load()
        {
            try
            {
                if (File.Exists(ConfigFileName))
                {
                    var json = File.ReadAllText(ConfigFileName);
                    var config = JsonConvert.DeserializeObject<ConfigManager>(json);
                    return config ?? new ConfigManager();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }

            return new ConfigManager();
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(ConfigFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting IP address: {ex.Message}");
            }
            return "127.0.0.1";
        }

        private static string GetMachineId()
        {
            try
            {
                // Use MAC address as unique identifier
                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n => n.OperationalStatus == OperationalStatus.Up
                                      && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                if (networkInterface != null)
                {
                    return BitConverter.ToString(networkInterface.GetPhysicalAddress().GetAddressBytes())
                        .Replace("-", "").ToLower();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting machine ID: {ex.Message}");
            }

            // Fallback to machine name
            return Environment.MachineName.ToLower();
        }
    }
}
