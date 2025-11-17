using System;
using System.Drawing;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        private readonly DisplayController _displayController;
        private readonly OverlayWindow? _overlayWindow;
        private readonly ServicePipeServer? _pipeServer;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _connectionCts;
        private Task? _connectionTask;
        private bool _running;
        private bool _authenticated;

        public bool IsConnected { get; private set; }

        public WebSocketClient(ConfigManager config, VolumeController volumeController, DisplayController displayController, OverlayWindow? overlayWindow = null, ServicePipeServer? pipeServer = null)
        {
            _config = config;
            _volumeController = volumeController;
            _displayController = displayController;
            _overlayWindow = overlayWindow;
            _pipeServer = pipeServer;
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

                    // Configure SSL/TLS validation
                    if (_config.WsUseSSL && _config.IgnoreSslErrors)
                    {
                        _webSocket.Options.RemoteCertificateValidationCallback =
                            (sender, certificate, chain, sslPolicyErrors) => true;
                    }

                    await _webSocket.ConnectAsync(new Uri(_config.WebSocketUrl), cancellationToken);
                    IsConnected = true;
                    _authenticated = false;
                    Console.WriteLine("WebSocket connected!");

                    // Send identification message
                    await SendIdentification();

                    await ReceiveLoop(cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket connection error: {ex.Message}");
                    IsConnected = false;
                    _authenticated = false;

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

        private async Task SendIdentification()
        {
            try
            {
                var identMessage = new JObject
                {
                    ["IDENT"] = _config.DeviceName,
                    ["deviceId"] = _config.DeviceId,
                    ["IP"] = _config.LocalIpAddress,
                    ["PORT"] = _config.WsPort,
                    ["deviceType"] = _config.DeviceType,
                    ["platform"] = _config.Platform,
                    ["osVersion"] = _config.OSVersion,
                    ["volume"] = _volumeController.GetVolumePercent(),
                    ["displayon"] = _displayController.IsDisplayOn() ? "true" : "false",
                    ["akku"] = -1,  // -1 indicates no battery (desktop PC)
                    ["serverrunning"] = "true"
                };

                // Add authentication if configured
                if (!string.IsNullOrEmpty(_config.Username))
                {
                    identMessage["USER"] = _config.Username;
                    identMessage["username"] = _config.Username;
                    identMessage["owner"] = _config.Username;
                }

                if (!string.IsNullOrEmpty(_config.Password))
                {
                    identMessage["PWD"] = _config.Password;
                    identMessage["password"] = _config.Password;
                }

                await SendMessage(identMessage);
                Console.WriteLine($"Sent identification: {_config.DeviceName} ({_config.DeviceId}) [Type: {_config.DeviceType}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending identification: {ex.Message}");
            }
        }

        private async Task SendMessage(JObject message)
        {
            if (_webSocket?.State == WebSocketState.Open)
            {
                var json = message.ToString(Formatting.None);
                var bytes = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
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

                // Handle server authentication/registration messages
                var messageType = data["type"]?.ToString();
                if (!string.IsNullOrEmpty(messageType))
                {
                    await HandleServerMessage(messageType, data);
                    return;
                }

                // Handle regular commands
                var command = data["command"]?.ToString() ?? "";
                var parameters = data["params"] as JObject ?? new JObject();

                if (!string.IsNullOrEmpty(command))
                {
                    Console.WriteLine($"Received command: {command}");
                    await Task.Run(() => ExecuteCommand(command, parameters));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }

        private async Task HandleServerMessage(string messageType, JObject data)
        {
            Console.WriteLine($"Received server message: {messageType}");

            switch (messageType.ToUpper())
            {
                case "REQUEST_USER":
                    Console.WriteLine("Server requests user authentication");
                    Console.WriteLine("Please configure Username and Password in config.json");
                    // Could potentially show a prompt here in GUI mode
                    break;

                case "AUTH_FAILED":
                    {
                        var reason = data["reason"]?.ToString() ?? "Unknown";
                        Console.WriteLine($"Authentication failed: {reason}");
                        _authenticated = false;
                        break;
                    }

                case "USER_AUTH_OK":
                    {
                        var user = data["user"]?.ToString();
                        Console.WriteLine($"Successfully authenticated as user: {user}");
                        _authenticated = true;
                        break;
                    }

                case "USER_AUTH_FAILED":
                    {
                        var reason = data["reason"]?.ToString() ?? "Unknown";
                        Console.WriteLine($"User authentication failed: {reason}");
                        _authenticated = false;
                        break;
                    }

                case "ALL_SETTINGS":
                    // Server requesting all settings - send device state
                    await SendDeviceSettings(data["UUID"]?.ToString());
                    break;

                // SockOperation messages from CentralServer
                case "REC":
                    await HandleRecordOperation(data);
                    break;

                case "AUDIO_SETTINGS":
                    await HandleAudioSettingsOperation(data);
                    break;

                case "DISPLAY":
                    await HandleDisplayOperation(data);
                    break;

                case "AUDIO_PLAYBACK":
                case "FILES":
                case "APPS":
                case "GPS":
                case "UPLOAD_BASE64":
                    // Not implemented for Windows - send default response
                    await SendNotSupportedResponse(data["UUID"]?.ToString(), messageType);
                    break;

                default:
                    Console.WriteLine($"Unknown server message type: {messageType}");
                    break;
            }
        }

        private async Task HandleRecordOperation(JObject data)
        {
            var operation = data["OP"]?.ToString();
            var uuid = data["UUID"]?.ToString();

            Console.WriteLine($"Received REC operation: {operation}");

            switch (operation?.ToLower())
            {
                case "load_settings":
                    // Return default recording settings (recording not implemented for Windows)
                    await SendMessage(new JObject
                    {
                        ["UUID"] = uuid,
                        ["record_filename"] = "recording.wav",
                        ["record_duration"] = "60",
                        ["record_partLength"] = "10",
                        ["record_doUpload"] = false,
                        ["supported"] = false  // Indicate recording is not supported
                    });
                    break;

                case "save_settings":
                case "start":
                case "stop":
                case "status":
                    // Acknowledge but don't actually do anything
                    await SendMessage(new JObject
                    {
                        ["UUID"] = uuid,
                        ["status"] = "not_supported",
                        ["message"] = "Audio recording is not implemented for Windows devices"
                    });
                    break;

                default:
                    Console.WriteLine($"Unknown REC operation: {operation}");
                    break;
            }
        }

        private async Task HandleAudioSettingsOperation(JObject data)
        {
            var operation = data["Operation"]?.ToString();
            var uuid = data["UUID"]?.ToString();

            Console.WriteLine($"Received AUDIO_SETTINGS operation: {operation}");

            switch (operation?.ToLower())
            {
                case "get_volumeinfo":
                    await SendMessage(new JObject
                    {
                        ["UUID"] = uuid,
                        ["volume"] = _volumeController.GetVolumePercent(),
                        ["muted"] = _volumeController.GetMute()
                    });
                    break;

                case "set_volume":
                    {
                        var volume = data["vol"]?.ToObject<int>() ?? 50;
                        _volumeController.SetVolumePercent(volume);
                        await SendMessage(new JObject
                        {
                            ["UUID"] = uuid,
                            ["volume"] = _volumeController.GetVolumePercent()
                        });
                        break;
                    }

                case "set_maxvolume":
                    {
                        var maxVol = data["vol"]?.ToObject<int>() ?? 100;
                        _volumeController.SetMaxVolumeLimit(maxVol);
                        await SendMessage(new JObject
                        {
                            ["UUID"] = uuid,
                            ["maxVolume"] = _config.MaxVolume
                        });
                        break;
                    }

                default:
                    await SendNotSupportedResponse(uuid, $"AUDIO_SETTINGS.{operation}");
                    break;
            }
        }

        private async Task HandleDisplayOperation(JObject data)
        {
            var operation = data["OP"]?.ToString();
            var uuid = data["UUID"]?.ToString();

            Console.WriteLine($"Received DISPLAY operation: {operation}");

            switch (operation?.ToLower())
            {
                case "setbrightness":
                    {
                        var brightness = data["val"]?.ToObject<int>() ?? 50;
                        _displayController.SetBrightness(brightness);
                        await SendMessage(new JObject
                        {
                            ["UUID"] = uuid,
                            ["brightness"] = _displayController.GetBrightness()
                        });
                        break;
                    }

                case "aus":
                    _displayController.TurnDisplayOff();
                    await SendMessage(new JObject
                    {
                        ["UUID"] = uuid,
                        ["status"] = "ok"
                    });
                    break;

                case "closewindow":
                    await SendOverlayCommand(PipeMessage.MessageTypes.HideOverlay);
                    await SendMessage(new JObject
                    {
                        ["UUID"] = uuid,
                        ["status"] = "ok"
                    });
                    break;

                default:
                    await SendNotSupportedResponse(uuid, $"DISPLAY.{operation}");
                    break;
            }
        }

        private async Task SendNotSupportedResponse(string? uuid, string operation)
        {
            if (string.IsNullOrEmpty(uuid)) return;

            await SendMessage(new JObject
            {
                ["UUID"] = uuid,
                ["status"] = "not_supported",
                ["message"] = $"Operation '{operation}' is not supported on Windows devices"
            });
            Console.WriteLine($"Operation '{operation}' not supported - sent default response");
        }

        private async Task SendOverlayCommand(string messageType, object? data = null)
        {
            if (_overlayWindow != null)
            {
                // Direct mode: use overlay window directly
                ExecuteOverlayCommandDirect(messageType, data);
            }
            else if (_pipeServer != null)
            {
                // Service mode: send via pipe to UI process
                await _pipeServer.SendMessageAsync(PipeMessage.Create(messageType, data));
            }
        }

        private void ExecuteOverlayCommandDirect(string messageType, object? data)
        {
            switch (messageType)
            {
                case PipeMessage.MessageTypes.ShowOverlay:
                    if (data is OverlayData overlayData)
                    {
                        Color? bgColor = null;
                        Color? txtColor = null;

                        if (!string.IsNullOrEmpty(overlayData.BackgroundColor))
                            bgColor = ColorTranslator.FromHtml(overlayData.BackgroundColor);

                        if (!string.IsNullOrEmpty(overlayData.TextColor))
                            txtColor = ColorTranslator.FromHtml(overlayData.TextColor);

                        _overlayWindow?.ShowAsync(overlayData.Message, bgColor, txtColor, overlayData.Opacity);
                    }
                    break;

                case PipeMessage.MessageTypes.HideOverlay:
                    _overlayWindow?.Hide();
                    break;

                case PipeMessage.MessageTypes.ShowNotification:
                    _overlayWindow?.ShowNotification(data?.ToString() ?? "Notification");
                    break;

                case PipeMessage.MessageTypes.ShowWarning:
                    _overlayWindow?.ShowWarning(data?.ToString() ?? "Warning");
                    break;

                case PipeMessage.MessageTypes.ShowError:
                    _overlayWindow?.ShowError(data?.ToString() ?? "Error");
                    break;

                case PipeMessage.MessageTypes.ShowBlockingScreen:
                    _overlayWindow?.ShowBlockingScreen(data?.ToString() ?? "Screen Locked");
                    break;
            }
        }

        private async Task SendDeviceSettings(string? uuid)
        {
            try
            {
                var currentVolume = _volumeController.GetVolumePercent();
                var currentBrightness = _displayController.GetBrightness();
                var isDisplayOn = _displayController.IsDisplayOn();

                // Build response in Android-compatible format
                var response = new JObject
                {
                    ["UUID"] = uuid,

                    // Battery info (Windows PCs typically don't have battery)
                    ["akku"] = new JObject
                    {
                        ["level"] = -1,  // -1 = no battery
                        ["charging"] = false,
                        ["temperature"] = 0
                    },

                    // File system info (not implemented, provide defaults)
                    ["fileSystem"] = new JObject
                    {
                        ["used"] = 0,
                        ["fullsize"] = 0,
                        ["available"] = 0
                    },

                    // Display info
                    ["display"] = new JObject
                    {
                        ["IS_ON"] = isDisplayOn,
                        ["MAGIC_WINDOW_VISIBLE"] = false,  // Windows doesn't use magic window
                        ["CURRENT_BRIGHTNESS"] = currentBrightness
                    },

                    // Sound/Volume info
                    ["sound"] = new JObject
                    {
                        ["CURRENT"] = currentVolume,
                        ["CURRENT_MAX"] = _config.MaxVolume,
                        ["MIN"] = 0
                    },

                    // Audio playback status
                    ["playback"] = false,  // Not implemented for Windows

                    // Do Not Disturb
                    ["DoNotDisturb"] = _config.EnforceMaxVolume,  // Map to volume enforcement

                    // Ringtone (not applicable for Windows)
                    ["klingelton"] = "default",

                    // Additional Windows-specific info
                    ["deviceType"] = _config.DeviceType,
                    ["platform"] = _config.Platform,
                    ["osVersion"] = _config.OSVersion,
                    ["deviceName"] = _config.DeviceName,
                    ["deviceId"] = _config.DeviceId
                };

                await SendMessage(response);
                Console.WriteLine($"Sent device settings response for UUID: {uuid}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending device settings: {ex.Message}");
            }
        }

        private async void ExecuteCommand(string command, JObject parameters)
        {
            try
            {
                switch (command.ToLower())
                {
                    // Overlay commands
                    case "show_overlay":
                        {
                            var msg = parameters["message"]?.ToString() ?? "";
                            var bgColorStr = parameters["bg_color"]?.ToString();
                            var textColorStr = parameters["text_color"]?.ToString();
                            var opacity = parameters["opacity"]?.ToObject<double>() ?? 0.9;

                            var overlayData = new OverlayData
                            {
                                Message = msg,
                                BackgroundColor = bgColorStr,
                                TextColor = textColorStr,
                                Opacity = opacity
                            };

                            await SendOverlayCommand(PipeMessage.MessageTypes.ShowOverlay, overlayData);
                            break;
                        }

                    case "hide_overlay":
                        await SendOverlayCommand(PipeMessage.MessageTypes.HideOverlay);
                        break;

                    case "show_blocking_screen":
                    case "displayaus":
                        {
                            var msg = parameters["message"]?.ToString() ?? "Screen Locked";
                            await SendOverlayCommand(PipeMessage.MessageTypes.ShowBlockingScreen, msg);
                            break;
                        }

                    case "closewindow":
                        await SendOverlayCommand(PipeMessage.MessageTypes.HideOverlay);
                        break;

                    case "show_notification":
                        {
                            var msg = parameters["message"]?.ToString() ?? "Notification";
                            await SendOverlayCommand(PipeMessage.MessageTypes.ShowNotification, msg);
                            break;
                        }

                    case "show_warning":
                        {
                            var msg = parameters["message"]?.ToString() ?? "Warning";
                            await SendOverlayCommand(PipeMessage.MessageTypes.ShowWarning, msg);
                            break;
                        }

                    case "show_error":
                        {
                            var msg = parameters["message"]?.ToString() ?? "Error";
                            await SendOverlayCommand(PipeMessage.MessageTypes.ShowError, msg);
                            break;
                        }

                    // Volume commands
                    case "set_volume":
                    case "setvolume":
                        {
                            var volume = parameters["volume"]?.ToObject<int>()
                                      ?? parameters["value"]?.ToObject<int>() ?? 50;
                            _volumeController.SetVolumePercent(volume);
                            await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                            {
                                ["volume"] = _volumeController.GetVolumePercent()
                            });
                            break;
                        }

                    case "get_volume":
                    case "getvolume":
                    case "getvolumeinfo":
                        {
                            var volume = _volumeController.GetVolumePercent();
                            Console.WriteLine($"Current volume: {volume}%");
                            await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                            {
                                ["volume"] = volume,
                                ["muted"] = _volumeController.GetMute()
                            });
                            break;
                        }

                    case "set_mute":
                    case "setmute":
                        {
                            var mute = parameters["mute"]?.ToObject<bool>()
                                    ?? parameters["value"]?.ToObject<bool>() ?? true;
                            _volumeController.SetMute(mute);
                            break;
                        }

                    case "toggle_mute":
                    case "togglemute":
                        _volumeController.ToggleMute();
                        break;

                    case "set_max_volume":
                    case "setmaxvolume":
                        {
                            var maxVolume = parameters["max_volume"]?.ToObject<int>()
                                         ?? parameters["value"]?.ToObject<int>() ?? 100;
                            _volumeController.SetMaxVolumeLimit(maxVolume);
                            await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                            {
                                ["maxVolume"] = _config.MaxVolume
                            });
                            break;
                        }

                    case "setminvolume":
                        // Not implemented yet
                        Console.WriteLine("SetMinVolume not implemented");
                        break;

                    case "enable_volume_enforcement":
                    case "setdonotdisturb":
                        {
                            var enable = parameters["enable"]?.ToObject<bool>()
                                      ?? parameters["value"]?.ToObject<bool>() ?? true;
                            _volumeController.EnableEnforcement(enable);
                            break;
                        }

                    // Display commands
                    case "setbrightness":
                        {
                            var brightness = parameters["brightness"]?.ToObject<int>()
                                          ?? parameters["val"]?.ToObject<int>()
                                          ?? parameters["value"]?.ToObject<int>() ?? 50;
                            var success = _displayController.SetBrightness(brightness);
                            await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                            {
                                ["brightness"] = _displayController.GetBrightness(),
                                ["success"] = success
                            });
                            break;
                        }

                    case "getbrightness":
                        {
                            var brightness = _displayController.GetBrightness();
                            await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                            {
                                ["brightness"] = brightness
                            });
                            break;
                        }

                    case "display_off":
                    case "aus":
                        _displayController.TurnDisplayOff();
                        break;

                    case "display_on":
                        _displayController.TurnDisplayOn();
                        break;

                    case "setnightmode":
                        // Night mode not directly supported on Windows
                        // Could be implemented as low brightness
                        Console.WriteLine("Night mode not implemented for Windows");
                        break;

                    case "ping":
                        Console.WriteLine("Pong!");
                        await SendCommandResponse(parameters["UUID"]?.ToString(), new JObject
                        {
                            ["status"] = "pong"
                        });
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command {command}: {ex.Message}");
            }
        }

        private async Task SendCommandResponse(string? uuid, JObject data)
        {
            if (string.IsNullOrEmpty(uuid)) return;

            try
            {
                var response = new JObject
                {
                    ["UUID"] = uuid,
                    ["data"] = data
                };

                await SendMessage(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command response: {ex.Message}");
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
