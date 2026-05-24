using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Named Pipe Server for Service to communicate with UI process
    /// </summary>
    public class ServicePipeServer : IDisposable
    {
        private const string PipeName = "WindowsRemoteToolsPipe";
        private NamedPipeServerStream? _pipeServer;
        private StreamWriter? _writer;
        private StreamReader? _reader;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _listenTask;
        private bool _isConnected;
        private readonly object _sync = new object();

        public event EventHandler<PipeMessage>? MessageReceived;
        public event EventHandler? ClientConnected;
        public event EventHandler? ClientDisconnected;

        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    return _isConnected && _writer != null;
                }
            }
        }

        public void Start()
        {
            if (_listenTask != null && !_listenTask.IsCompleted)
                return;

            _cancellationTokenSource = new CancellationTokenSource();
            _listenTask = Task.Run(() => ListenLoop(_cancellationTokenSource.Token));
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Create new pipe server with ACL allowing all authenticated users to connect.
                    // Required because the service runs as LocalSystem (Session 0) and the UI
                    // process runs as a regular user (Session 1+).
                    var security = new PipeSecurity();
                    security.AddAccessRule(new PipeAccessRule(
                        new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                        PipeAccessRights.ReadWrite,
                        AccessControlType.Allow));
                    security.AddAccessRule(new PipeAccessRule(
                        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                        PipeAccessRights.FullControl,
                        AccessControlType.Allow));

                    _pipeServer = NamedPipeServerStreamAcl.Create(
                        PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        0, 0,
                        security);

                    Console.WriteLine("ServicePipeServer: Waiting for client connection...");

                    // Wait for client connection
                    await _pipeServer.WaitForConnectionAsync(cancellationToken);

                    _reader = new StreamReader(_pipeServer, Encoding.UTF8);
                    _writer = new StreamWriter(_pipeServer, Encoding.UTF8) { AutoFlush = true };

                    lock (_sync)
                    {
                        _isConnected = true;
                    }

                    Console.WriteLine("ServicePipeServer: Client connected!");
                    ClientConnected?.Invoke(this, EventArgs.Empty);

                    // Read messages from client
                    while (_isConnected && !cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var line = await _reader.ReadLineAsync();
                            if (line == null)
                            {
                                // Client disconnected
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
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ServicePipeServer error: {ex.Message}");
                }
                finally
                {
                    lock (_sync)
                    {
                        _isConnected = false;
                    }
                    ClientDisconnected?.Invoke(this, EventArgs.Empty);

                    _reader?.Dispose();
                    _writer?.Dispose();
                    _pipeServer?.Dispose();

                    _reader = null;
                    _writer = null;
                    _pipeServer = null;
                }

                // Wait before recreating pipe
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public async Task<bool> SendMessageAsync(PipeMessage message)
        {
            StreamWriter? writer;
            bool isConnected;

            lock (_sync)
            {
                writer = _writer;
                isConnected = _isConnected;
            }

            if (!isConnected || writer == null)
            {
                Console.WriteLine($"ServicePipeServer SendMessage skipped: connected={isConnected}, writerReady={writer != null}, type={message.Type}");
                return false;
            }

            try
            {
                await writer.WriteLineAsync(message.ToJson());
                Console.WriteLine($"ServicePipeServer sent message: {message.Type}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ServicePipeServer SendMessage error: {ex}");
                return false;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            try
            {
                _listenTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
            }

            lock (_sync)
            {
                _isConnected = false;
                _reader?.Dispose();
                _writer?.Dispose();
                _pipeServer?.Dispose();
                _cancellationTokenSource?.Dispose();
                _reader = null;
                _writer = null;
                _pipeServer = null;
                _cancellationTokenSource = null;
                _listenTask = null;
            }
        }
    }
}
