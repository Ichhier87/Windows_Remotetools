using System;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace WindowsRemoteTools
{
    /// <summary>
    /// WebSocket client for receiving remote commands
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        private readonly ConfigManager _config;
        private readonly VolumeController _volumeController;
        private readonly OverlayWindow _overlayWindow;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _connectionCts;
        private Task? _connectionTask;
        private bool _running;

        public bool IsConnected { get; private set; }

        public WebSocketClient(ConfigManager config, VolumeController volumeController, OverlayWindow overlayWindow)
        {
            _config = config;
            _volumeController = volumeController;
            _overlayWindow = overlayWindow;
        }

        public void Start()
        {
            if (_running) return;

            _running = true;
            _connectionCts = new CancellationTokenSource();
            _connectionTask = Task.Run(() => ConnectionLoop(_connectionCts.Token));
            Console.WriteLine("WebSocket client started");
        }

        public void Stop()
        {
            _running = false;
            IsConnected = false;
            _connectionCts?.Cancel();
            _webSocket?.Dispose();
            _connectionTask?.Wait(TimeSpan.FromSeconds(2));
            Console.WriteLine("WebSocket client stopped");
        }

        private async Task ConnectionLoop(CancellationToken cancellationToken)
        {
            while (_running && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine($"Connecting to {_config.WebSocketUrl}...");
                    _webSocket = new ClientWebSocket();
                    await _webSocket.ConnectAsync(new Uri(_config.WebSocketUrl), cancellationToken);
                    IsConnected = true;
                    Console.WriteLine("WebSocket connected!");

                    await ReceiveLoop(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket connection error: {ex.Message}");
                    IsConnected = false;

                    if (_running && !cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Reconnecting in 5 seconds...");
                        await Task.Delay(5000, cancellationToken);
                    }
                }
                finally
                {
                    _webSocket?.Dispose();
                    _webSocket = null;
                }
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("WebSocket connection closed by server");
                        IsConnected = false;
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await ProcessMessage(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving message: {ex.Message}");
                    IsConnected = false;
                    break;
                }
            }
        }

        private async Task ProcessMessage(string message)
        {
            try
            {
                var data = JObject.Parse(message);
                var command = data["command"]?.ToString() ?? "";
                var parameters = data["params"] as JObject ?? new JObject();

                Console.WriteLine($"Received command: {command}");

                await Task.Run(() => ExecuteCommand(command, parameters));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private void ExecuteCommand(string command, JObject parameters)
        {
            switch (command.ToLower())
            {
                // Overlay commands
                case "show_overlay":
                    {
                        var msg = parameters["message"]?.ToString() ?? "";
                        var bgColor = ParseColor(parameters["bg_color"]?.ToString() ?? "black");
                        var textColor = ParseColor(parameters["text_color"]?.ToString() ?? "white");
                        var opacity = parameters["opacity"]?.ToObject<double>() ?? 0.9;
                        _overlayWindow.ShowAsync(msg, bgColor, textColor, opacity);
                        break;
                    }

                case "hide_overlay":
                    _overlayWindow.Hide();
                    break;

                case "show_blocking_screen":
                    {
                        var msg = parameters["message"]?.ToString() ?? "Screen Locked";
                        _overlayWindow.ShowBlockingScreen(msg);
                        break;
                    }

                case "show_notification":
                    {
                        var msg = parameters["message"]?.ToString() ?? "Notification";
                        _overlayWindow.ShowNotification(msg);
                        break;
                    }

                case "show_warning":
                    {
                        var msg = parameters["message"]?.ToString() ?? "Warning";
                        _overlayWindow.ShowWarning(msg);
                        break;
                    }

                case "show_error":
                    {
                        var msg = parameters["message"]?.ToString() ?? "Error";
                        _overlayWindow.ShowError(msg);
                        break;
                    }

                // Volume commands
                case "set_volume":
                    {
                        var volume = parameters["volume"]?.ToObject<int>() ?? 50;
                        _volumeController.SetVolumePercent(volume);
                        break;
                    }

                case "get_volume":
                    {
                        var volume = _volumeController.GetVolumePercent();
                        Console.WriteLine($"Current volume: {volume}%");
                        break;
                    }

                case "set_mute":
                    {
                        var mute = parameters["mute"]?.ToObject<bool>() ?? true;
                        _volumeController.SetMute(mute);
                        break;
                    }

                case "toggle_mute":
                    _volumeController.ToggleMute();
                    break;

                case "set_max_volume":
                    {
                        var maxVolume = parameters["max_volume"]?.ToObject<int>() ?? 100;
                        _volumeController.SetMaxVolumeLimit(maxVolume);
                        break;
                    }

                case "enable_volume_enforcement":
                    {
                        var enable = parameters["enable"]?.ToObject<bool>() ?? true;
                        _volumeController.EnableEnforcement(enable);
                        break;
                    }

                case "ping":
                    Console.WriteLine("Pong!");
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    break;
            }
        }

        private Color ParseColor(string colorName)
        {
            try
            {
                return Color.FromName(colorName);
            }
            catch
            {
                return Color.Black;
            }
        }

        public string GetStatus()
        {
            return IsConnected ? "Connected" : "Disconnected";
        }

        public void Dispose()
        {
            Stop();
            _connectionCts?.Dispose();
            _webSocket?.Dispose();
        }
    }
}
