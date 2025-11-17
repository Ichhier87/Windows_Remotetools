using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsRemoteToolsUI
{
    /// <summary>
    /// Named Pipe Client for UI process to communicate with Service
    /// </summary>
    public class UIPipeClient : IDisposable
    {
        private const string PipeName = "WindowsRemoteToolsPipe";
        private NamedPipeClientStream? _pipeClient;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenTask;
        private bool _isConnected;

        public event EventHandler<PipeMessage>? MessageReceived;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        public bool IsConnected => _isConnected;

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _pipeClient = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                Console.WriteLine("UIPipeClient: Connecting to service...");
                await _pipeClient.ConnectAsync(5000);

                _isConnected = true;
                Console.WriteLine("UIPipeClient: Connected to service!");

                _reader = new StreamReader(_pipeClient, Encoding.UTF8);
                _writer = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };

                Connected?.Invoke(this, EventArgs.Empty);

                // Start listening for messages
                _cancellationTokenSource = new CancellationTokenSource();
                _listenTask = Task.Run(() => ListenLoop(_cancellationTokenSource.Token));

                // Send ready message
                await SendMessageAsync(PipeMessage.Create(PipeMessage.MessageTypes.Ready));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UIPipeClient connection error: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_reader == null)
                        break;

                    var line = await _reader.ReadLineAsync();
                    if (line == null)
                    {
                        // Service disconnected
                        break;
                    }

                    var message = PipeMessage.FromJson(line);
                    if (message != null)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
                catch (IOException)
                {
                    // Pipe broken
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"UIPipeClient listen error: {ex.Message}");
                }
            }

            _isConnected = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> SendMessageAsync(PipeMessage message)
        {
            if (!_isConnected || _writer == null)
                return false;

            try
            {
                await _writer.WriteLineAsync(message.ToJson());
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UIPipeClient SendMessage error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _listenTask?.Wait(TimeSpan.FromSeconds(2));

            _reader?.Dispose();
            _writer?.Dispose();
            _pipeClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
