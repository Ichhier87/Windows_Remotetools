using System;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Core functionality shared between GUI and Service modes
    /// </summary>
    public class RemoteToolsCore : IDisposable
    {
        private readonly ConfigManager _config;
        private readonly VolumeController _volumeController;
        private readonly DisplayController _displayController;
        private readonly OverlayWindow? _overlayWindow;
        private readonly ServicePipeServer? _pipeServer;
        private readonly WebSocketClient _webSocketClient;
        private bool _disposed = false;
        private readonly bool _useOverlayDirectly;

        public RemoteToolsCore(bool useOverlayDirectly = false)
        {
            Console.WriteLine("Initializing Remote Tools Core...");

            _useOverlayDirectly = useOverlayDirectly;

            // Initialize components
            _config = ConfigManager.Load();
            _volumeController = new VolumeController(_config);
            _displayController = new DisplayController();

            if (_useOverlayDirectly)
            {
                // GUI mode: use overlay directly
                _overlayWindow = new OverlayWindow();
                _webSocketClient = new WebSocketClient(_config, _volumeController, _displayController, _overlayWindow);
            }
            else
            {
                // Service mode: use pipe server to communicate with UI process
                _pipeServer = new ServicePipeServer();
                _webSocketClient = new WebSocketClient(_config, _volumeController, _displayController, null, _pipeServer);
            }
        }

        /// <summary>
        /// Starts all core services
        /// </summary>
        public void Start()
        {
            Console.WriteLine("Starting Remote Tools Core services...");

            // Start pipe server if in service mode
            if (_pipeServer != null)
            {
                _pipeServer.Start();
            }

            // Start WebSocket client
            _webSocketClient.Start();

            // Start volume monitoring if configured
            if (_config.EnforceMaxVolume)
            {
                _volumeController.StartMonitoring();
            }

            Console.WriteLine("Remote Tools Core services started successfully");
        }

        /// <summary>
        /// Stops all core services
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("Stopping Remote Tools Core services...");

            _webSocketClient?.Stop();
            _volumeController?.StopMonitoring();
            _overlayWindow?.Hide();
            _pipeServer?.Dispose();

            Console.WriteLine("Remote Tools Core services stopped");
        }

        /// <summary>
        /// Gets the configuration manager
        /// </summary>
        public ConfigManager Config => _config;

        /// <summary>
        /// Gets the volume controller
        /// </summary>
        public VolumeController VolumeController => _volumeController;

        /// <summary>
        /// Gets the display controller
        /// </summary>
        public DisplayController DisplayController => _displayController;

        /// <summary>
        /// Gets the overlay window (only available in GUI mode)
        /// </summary>
        public OverlayWindow? OverlayWindow => _overlayWindow;

        /// <summary>
        /// Gets the pipe server (only available in Service mode)
        /// </summary>
        public ServicePipeServer? PipeServer => _pipeServer;

        /// <summary>
        /// Gets the WebSocket client
        /// </summary>
        public WebSocketClient WebSocketClient => _webSocketClient;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop();
                    _webSocketClient?.Dispose();
                    _volumeController?.Dispose();
                    _displayController?.Dispose();
                    _pipeServer?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
