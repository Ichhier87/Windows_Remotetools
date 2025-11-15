using System;
using System.IO;
using Newtonsoft.Json;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Configuration manager for the application
    /// </summary>
    public class ConfigManager
    {
        private const string ConfigFileName = "config.json";

        public string WsHost { get; set; } = "localhost";
        public int WsPort { get; set; } = 8765;
        public int MaxVolume { get; set; } = 100;
        public double VolumeCheckInterval { get; set; } = 1.0;
        public bool EnforceMaxVolume { get; set; } = true;

        public string WebSocketUrl => $"ws://{WsHost}:{WsPort}";

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
    }
}
