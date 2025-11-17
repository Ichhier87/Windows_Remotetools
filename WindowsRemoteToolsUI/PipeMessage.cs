using System;
using Newtonsoft.Json;

namespace WindowsRemoteToolsUI
{
    /// <summary>
    /// Message format for Named Pipe communication between Service and UI
    /// </summary>
    public class PipeMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("data")]
        public object? Data { get; set; }

        public static class MessageTypes
        {
            // Service → UI Commands
            public const string ShowOverlay = "show_overlay";
            public const string HideOverlay = "hide_overlay";
            public const string ShowNotification = "show_notification";
            public const string ShowWarning = "show_warning";
            public const string ShowError = "show_error";
            public const string ShowBlockingScreen = "show_blocking_screen";
            public const string Shutdown = "shutdown";

            // UI → Service Commands
            public const string Heartbeat = "heartbeat";
            public const string Ready = "ready";
            public const string Closed = "closed";
        }

        public static PipeMessage Create(string type, object? data = null)
        {
            return new PipeMessage { Type = type, Data = data };
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static PipeMessage? FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<PipeMessage>(json);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Data for overlay display commands
    /// </summary>
    public class OverlayData
    {
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        [JsonProperty("backgroundColor")]
        public string? BackgroundColor { get; set; }

        [JsonProperty("textColor")]
        public string? TextColor { get; set; }

        [JsonProperty("opacity")]
        public double Opacity { get; set; } = 0.9;
    }
}
