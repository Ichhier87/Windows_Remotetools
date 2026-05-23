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
            public const string ShowPicture = "show_picture";
            public const string ShowLockedScreen = "show_locked_screen";
            public const string ShowSvgOverlay = "show_svg_overlay";
            public const string HideSvgOverlay = "hide_svg_overlay";
            public const string Shutdown = "shutdown";

            // UI → Service Commands
            public const string Heartbeat = "heartbeat";
            public const string Ready = "ready";
            public const string Closed = "closed";
            public const string ScreenUnlocked = "screen_unlocked";
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
    /// Data for locked overlay display command
    /// </summary>
    public class LockedOverlayData
    {
        [JsonProperty("message")]
        public string Message { get; set; } = "Screen Locked";

        [JsonProperty("password_hash")]
        public string PasswordHash { get; set; } = "";
    }

    /// <summary>
    /// Data for SVG/positioned overlay display commands
    /// </summary>
    public class SvgOverlayData
    {
        [JsonProperty("svg")]
        public string Svg { get; set; } = "";

        [JsonProperty("text")]
        public string Text { get; set; } = "";

        [JsonProperty("width")]
        public int Width { get; set; } = 300;

        [JsonProperty("height")]
        public int Height { get; set; } = 100;

        [JsonProperty("x")]
        public int X { get; set; } = 0;

        [JsonProperty("y")]
        public int Y { get; set; } = 0;

        [JsonProperty("gravity")]
        public string Gravity { get; set; } = "TOP_LEFT";

        [JsonProperty("clickThrough")]
        public bool ClickThrough { get; set; } = false;

        [JsonProperty("backgroundColor")]
        public string BackgroundColor { get; set; } = "#000000";

        [JsonProperty("textColor")]
        public string TextColor { get; set; } = "#FFFFFF";

        [JsonProperty("borderColor")]
        public string BorderColor { get; set; } = "#000000";

        [JsonProperty("borderWidth")]
        public int BorderWidth { get; set; } = 0;

        [JsonProperty("fontSize")]
        public int FontSize { get; set; } = 24;

        [JsonProperty("opacity")]
        public double Opacity { get; set; } = 0.9;
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
