using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ExampleServer
{
    class Program
    {
        private static readonly List<WebSocket> _clients = new List<WebSocket>();
        private static readonly object _clientsLock = new object();

        static async Task Main(string[] args)
        {
            var host = "localhost";
            var port = 8765;

            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("WebSocket Server - Windows Remote Tools");
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine();

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{host}:{port}/");
            listener.Start();

            Console.WriteLine($"Server running on ws://{host}:{port}");
            Console.WriteLine("Waiting for clients to connect...");
            Console.WriteLine();

            var acceptTask = Task.Run(() => AcceptClients(listener));
            var menuTask = Task.Run(() => InteractiveMenu());

            await Task.WhenAll(acceptTask, menuTask);
        }

        static async Task AcceptClients(HttpListener listener)
        {
            while (true)
            {
                var context = await listener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var webSocket = wsContext.WebSocket;

                    lock (_clientsLock)
                    {
                        _clients.Add(webSocket);
                    }

                    Console.WriteLine($"Client connected. Total clients: {_clients.Count}");

                    _ = Task.Run(() => HandleClient(webSocket));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }

        static async Task HandleClient(WebSocket webSocket)
        {
            var buffer = new byte[4096];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                        break;
                    }

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received from client: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                lock (_clientsLock)
                {
                    _clients.Remove(webSocket);
                }
                Console.WriteLine($"Client disconnected. Total clients: {_clients.Count}");
                webSocket.Dispose();
            }
        }

        static async Task SendCommand(string command, object? parameters = null)
        {
            List<WebSocket> currentClients;
            lock (_clientsLock)
            {
                if (_clients.Count == 0)
                {
                    Console.WriteLine("No clients connected!");
                    return;
                }
                currentClients = new List<WebSocket>(_clients);
            }

            var message = new
            {
                command = command,
                @params = parameters ?? new { }
            };

            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            Console.WriteLine($"Sending command: {command}");

            foreach (var client in currentClients)
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending to client: {ex.Message}");
                }
            }
        }

        static async Task InteractiveMenu()
        {
            await Task.Delay(1000); // Wait for server to start

            while (true)
            {
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("Available Commands:");
                Console.WriteLine("=".PadRight(60, '='));
                Console.WriteLine("1.  Show Overlay");
                Console.WriteLine("2.  Show Blocking Screen");
                Console.WriteLine("3.  Show Notification");
                Console.WriteLine("4.  Show Warning");
                Console.WriteLine("5.  Show Error");
                Console.WriteLine("6.  Hide Overlay");
                Console.WriteLine("7.  Set Volume to 50%");
                Console.WriteLine("8.  Set Volume to 100%");
                Console.WriteLine("9.  Mute");
                Console.WriteLine("10. Unmute");
                Console.WriteLine("11. Toggle Mute");
                Console.WriteLine("12. Set Max Volume to 50%");
                Console.WriteLine("13. Set Max Volume to 75%");
                Console.WriteLine("14. Set Max Volume to 100%");
                Console.WriteLine("15. Enable Volume Enforcement");
                Console.WriteLine("16. Disable Volume Enforcement");
                Console.WriteLine("17. Ping");
                Console.WriteLine("0.  Quit");
                Console.WriteLine();

                Console.Write("Enter command number: ");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        Console.Write("Enter message: ");
                        var msg1 = Console.ReadLine();
                        await SendCommand("show_overlay", new
                        {
                            message = msg1,
                            bg_color = "darkblue",
                            text_color = "white",
                            opacity = 0.9
                        });
                        break;

                    case "2":
                        Console.Write("Enter message (default: Screen Locked): ");
                        var msg2 = Console.ReadLine();
                        await SendCommand("show_blocking_screen", new
                        {
                            message = string.IsNullOrEmpty(msg2) ? "Screen Locked" : msg2
                        });
                        break;

                    case "3":
                        Console.Write("Enter notification: ");
                        var msg3 = Console.ReadLine();
                        await SendCommand("show_notification", new { message = msg3 });
                        break;

                    case "4":
                        Console.Write("Enter warning: ");
                        var msg4 = Console.ReadLine();
                        await SendCommand("show_warning", new { message = msg4 });
                        break;

                    case "5":
                        Console.Write("Enter error message: ");
                        var msg5 = Console.ReadLine();
                        await SendCommand("show_error", new { message = msg5 });
                        break;

                    case "6":
                        await SendCommand("hide_overlay");
                        break;

                    case "7":
                        await SendCommand("set_volume", new { volume = 50 });
                        break;

                    case "8":
                        await SendCommand("set_volume", new { volume = 100 });
                        break;

                    case "9":
                        await SendCommand("set_mute", new { mute = true });
                        break;

                    case "10":
                        await SendCommand("set_mute", new { mute = false });
                        break;

                    case "11":
                        await SendCommand("toggle_mute");
                        break;

                    case "12":
                        await SendCommand("set_max_volume", new { max_volume = 50 });
                        break;

                    case "13":
                        await SendCommand("set_max_volume", new { max_volume = 75 });
                        break;

                    case "14":
                        await SendCommand("set_max_volume", new { max_volume = 100 });
                        break;

                    case "15":
                        await SendCommand("enable_volume_enforcement", new { enable = true });
                        break;

                    case "16":
                        await SendCommand("enable_volume_enforcement", new { enable = false });
                        break;

                    case "17":
                        await SendCommand("ping");
                        break;

                    case "0":
                        Console.WriteLine("Shutting down server...");
                        Environment.Exit(0);
                        break;

                    default:
                        Console.WriteLine("Invalid choice!");
                        break;
                }
            }
        }
    }
}
