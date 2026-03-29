namespace Test.WebsocketClient
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class Program
    {
        private static readonly object _ConsoleSync = new object();
        private static ClientWebSocket _Socket = null;
        private static CancellationTokenSource _ReceiveCts = null;
        private static Task _ReceiveTask = null;
        private static string _Uri = "ws://127.0.0.1:8181/ws/echo";
        private static readonly Dictionary<string, string> _Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> _Subprotocols = new List<string>();

        private static async Task Main(string[] args)
        {
            AttachLifecycleHandlers();
            PrintBanner();

            bool run = true;
            while (run)
            {
                Console.Write("ws-client> ");
                string input = Console.ReadLine();
                string command = (input ?? String.Empty).Trim().ToLowerInvariant();

                switch (command)
                {
                    case "?":
                    case "help":
                        PrintMenu();
                        break;
                    case "preset":
                        ChoosePreset();
                        break;
                    case "uri":
                        SetCustomUri();
                        break;
                    case "headers":
                        ManageHeaders();
                        break;
                    case "subprotocols":
                    case "subproto":
                        ManageSubprotocols();
                        break;
                    case "connect":
                        await ConnectAsync().ConfigureAwait(false);
                        break;
                    case "disconnect":
                        await DisconnectAsync().ConfigureAwait(false);
                        break;
                    case "text":
                        await SendTextAsync().ConfigureAwait(false);
                        break;
                    case "textn":
                    case "burst":
                        await SendTextBurstAsync().ConfigureAwait(false);
                        break;
                    case "binary":
                        await SendBinaryAsync().ConfigureAwait(false);
                        break;
                    case "close":
                    case "closewith":
                        await CloseWithReasonAsync().ConfigureAwait(false);
                        break;
                    case "state":
                        PrintState();
                        break;
                    case "clear":
                    case "cls":
                        Console.Clear();
                        break;
                    case "quit":
                    case "q":
                    case "exit":
                        run = false;
                        break;
                    default:
                        if (!String.IsNullOrWhiteSpace(command))
                        {
                            WriteLine("Unknown command. Use ? for help.");
                        }
                        break;
                }
            }

            await DisconnectAsync().ConfigureAwait(false);
        }

        private static void AttachLifecycleHandlers()
        {
            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = false;
                WriteLine("[event] Ctrl+C received, disconnecting and exiting");
                try
                {
                    DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception e)
                {
                    WriteLine("[event error] " + e.Message);
                }
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, args) =>
            {
                WriteLine("[event] ProcessExit");
                try
                {
                    DisconnectAsync().GetAwaiter().GetResult();
                }
                catch (Exception)
                {
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Exception exception = args.ExceptionObject as Exception;
                WriteLine("[event] UnhandledException " + (exception?.Message ?? "(non-exception)"));
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                WriteLine("[event] UnobservedTaskException " + args.Exception.GetBaseException().Message);
                args.SetObserved();
            };
        }

        private static void PrintBanner()
        {
            WriteLine("Watson websocket sample client");
            WriteLine("Default URI: " + _Uri);
            WriteLine("");
            PrintMenu();
        }

        private static void PrintMenu()
        {
            WriteLine("Commands:");
            WriteLine("  ? / help    show commands");
            WriteLine("  preset      choose a predefined endpoint");
            WriteLine("  uri         set a custom websocket URI");
            WriteLine("  headers     manage request headers");
            WriteLine("  subproto    manage requested subprotocols");
            WriteLine("  connect     connect to the current URI");
            WriteLine("  disconnect  close the active connection and tear down local state");
            WriteLine("  text        send a UTF-8 text message");
            WriteLine("  textn       send N UTF-8 text messages");
            WriteLine("  binary      send a UTF-8 string as binary bytes");
            WriteLine("  close       close with an explicit status and reason");
            WriteLine("  state       print connection state");
            WriteLine("  clear       clear the screen");
            WriteLine("  quit        exit");
            WriteLine("");
        }

        private static void ChoosePreset()
        {
            WriteLine("Presets:");
            WriteLine("  1  ws://127.0.0.1:8181/ws/echo");
            WriteLine("  2  ws://127.0.0.1:8181/ws/time");
            WriteLine("  3  ws://127.0.0.1:8181/ws/upper");
            WriteLine("  4  ws://127.0.0.1:8181/ws/inspect?name=alice");
            WriteLine("  5  ws://127.0.0.1:8181/ws/broadcast/general");
            Console.Write("Preset> ");
            string preset = (Console.ReadLine() ?? String.Empty).Trim();

            switch (preset)
            {
                case "1":
                    _Uri = "ws://127.0.0.1:8181/ws/echo";
                    break;
                case "2":
                    _Uri = "ws://127.0.0.1:8181/ws/time";
                    break;
                case "3":
                    _Uri = "ws://127.0.0.1:8181/ws/upper";
                    break;
                case "4":
                    _Uri = "ws://127.0.0.1:8181/ws/inspect?name=alice";
                    break;
                case "5":
                    _Uri = "ws://127.0.0.1:8181/ws/broadcast/general";
                    break;
                default:
                    WriteLine("Unknown preset.");
                    return;
            }

            WriteLine("Current URI: " + _Uri);
        }

        private static void SetCustomUri()
        {
            Console.Write("WebSocket URI> ");
            string value = (Console.ReadLine() ?? String.Empty).Trim();
            if (String.IsNullOrWhiteSpace(value))
            {
                WriteLine("URI unchanged.");
                return;
            }

            _Uri = value;
            WriteLine("Current URI: " + _Uri);
        }

        private static async Task ConnectAsync()
        {
            if (_Socket != null && _Socket.State == WebSocketState.Open)
            {
                WriteLine("Already connected.");
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);

            _Socket = new ClientWebSocket();
            _ReceiveCts = new CancellationTokenSource();
            ApplyConnectionOptions(_Socket);

            try
            {
                await _Socket.ConnectAsync(new Uri(_Uri), CancellationToken.None).ConfigureAwait(false);
                _ReceiveTask = Task.Run(() => ReceiveLoopAsync(_Socket, _ReceiveCts.Token));
                WriteLine("Connected to " + _Uri);
            }
            catch (Exception e)
            {
                WriteLine("Connect failed: " + e.Message);
                await DisconnectAsync().ConfigureAwait(false);
            }
        }

        private static async Task DisconnectAsync()
        {
            ClientWebSocket socket = _Socket;
            CancellationTokenSource receiveCts = _ReceiveCts;
            Task receiveTask = _ReceiveTask;

            _Socket = null;
            _ReceiveCts = null;
            _ReceiveTask = null;

            if (receiveCts != null)
            {
                try { receiveCts.Cancel(); } catch (ObjectDisposedException) { }
            }

            if (socket != null)
            {
                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client disconnect", CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    try { socket.Abort(); } catch (Exception) { }
                }

                socket.Dispose();
            }

            if (receiveTask != null)
            {
                try { await receiveTask.ConfigureAwait(false); } catch (Exception) { }
            }

            if (receiveCts != null) receiveCts.Dispose();
        }

        private static async Task SendTextAsync()
        {
            if (!EnsureConnected()) return;

            Console.Write("Text payload> ");
            string payload = Console.ReadLine() ?? String.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            WriteLine("[sent text] " + payload);
        }

        private static async Task SendBinaryAsync()
        {
            if (!EnsureConnected()) return;

            Console.Write("Binary payload (UTF-8 source text)> ");
            string payload = Console.ReadLine() ?? String.Empty;
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None).ConfigureAwait(false);
            WriteLine("[sent binary] " + bytes.Length + " bytes");
        }

        private static async Task SendTextBurstAsync()
        {
            if (!EnsureConnected()) return;

            Console.Write("Message count> ");
            string countInput = (Console.ReadLine() ?? String.Empty).Trim();
            if (!Int32.TryParse(countInput, out int count) || count < 1)
            {
                WriteLine("Invalid count.");
                return;
            }

            Console.Write("Text payload template [hello]> ");
            string payload = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(payload)) payload = "hello";

            Console.Write("Append index suffix? [Y/n]> ");
            string suffixChoice = (Console.ReadLine() ?? String.Empty).Trim();
            bool appendIndex = !suffixChoice.Equals("n", StringComparison.OrdinalIgnoreCase) &&
                !suffixChoice.Equals("no", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                string outbound = appendIndex ? payload + " #" + (i + 1) : payload;
                byte[] bytes = Encoding.UTF8.GetBytes(outbound);
                await _Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }

            WriteLine("[sent text burst] " + count + " messages");
        }

        private static async Task CloseWithReasonAsync()
        {
            if (!EnsureConnected()) return;

            Console.Write("Close status [NormalClosure]> ");
            string statusInput = (Console.ReadLine() ?? String.Empty).Trim();
            if (String.IsNullOrWhiteSpace(statusInput)) statusInput = nameof(WebSocketCloseStatus.NormalClosure);

            if (!Enum.TryParse(statusInput, true, out WebSocketCloseStatus closeStatus))
            {
                WriteLine("Invalid close status.");
                return;
            }

            Console.Write("Close reason [client close]> ");
            string reason = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(reason)) reason = "client close";

            await _Socket.CloseAsync(closeStatus, reason, CancellationToken.None).ConfigureAwait(false);
            WriteLine("[close sent] " + closeStatus + " " + reason);
        }

        private static void PrintState()
        {
            if (_Socket == null)
            {
                WriteLine("Socket: (null)");
                WriteLine("URI: " + _Uri);
                return;
            }

            WriteLine("Socket state: " + _Socket.State);
            WriteLine("Close status: " + (_Socket.CloseStatus?.ToString() ?? "(null)"));
            WriteLine("Close description: " + (_Socket.CloseStatusDescription ?? "(null)"));
            WriteLine("URI: " + _Uri);
            WriteLine("Headers: " + (_Headers.Count > 0 ? String.Join(", ", _Headers.Select(kvp => kvp.Key + "=" + kvp.Value)) : "(none)"));
            WriteLine("Subprotocols: " + (_Subprotocols.Count > 0 ? String.Join(", ", _Subprotocols) : "(none)"));
        }

        private static async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                while (!token.IsCancellationRequested && socket != null)
                {
                    int offset = 0;
                    WebSocketMessageType messageType = WebSocketMessageType.Text;

                    while (true)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token).ConfigureAwait(false);
                        messageType = result.MessageType;

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            WriteLine("[recv close] " + result.CloseStatus + " " + (result.CloseStatusDescription ?? String.Empty));
                            return;
                        }

                        offset += result.Count;
                        if (result.EndOfMessage)
                        {
                            break;
                        }

                        if (offset >= buffer.Length)
                        {
                            WriteLine("[recv] message exceeded local buffer");
                            return;
                        }
                    }

                    if (messageType == WebSocketMessageType.Text)
                    {
                        WriteLine("[recv text] " + Encoding.UTF8.GetString(buffer, 0, offset));
                    }
                    else
                    {
                        WriteLine("[recv binary] " + BitConverter.ToString(buffer, 0, offset));
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException e)
            {
                WriteLine("[recv error] " + e.Message);
            }
            catch (Exception e)
            {
                WriteLine("[recv error] " + e.Message);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static bool EnsureConnected()
        {
            if (_Socket == null || _Socket.State != WebSocketState.Open)
            {
                WriteLine("Not connected.");
                return false;
            }

            return true;
        }

        private static void ManageHeaders()
        {
            WriteLine("Headers:");
            if (_Headers.Count < 1)
            {
                WriteLine("  (none)");
            }
            else
            {
                foreach (KeyValuePair<string, string> header in _Headers)
                {
                    WriteLine("  " + header.Key + ": " + header.Value);
                }
            }

            Console.Write("Header command [set/remove/clear]> ");
            string command = (Console.ReadLine() ?? String.Empty).Trim().ToLowerInvariant();
            switch (command)
            {
                case "set":
                    Console.Write("Header name> ");
                    string name = (Console.ReadLine() ?? String.Empty).Trim();
                    if (String.IsNullOrWhiteSpace(name))
                    {
                        WriteLine("Header name is required.");
                        return;
                    }

                    Console.Write("Header value> ");
                    string value = Console.ReadLine() ?? String.Empty;
                    _Headers[name] = value;
                    WriteLine("Header set.");
                    break;
                case "remove":
                    Console.Write("Header name> ");
                    string removeName = (Console.ReadLine() ?? String.Empty).Trim();
                    if (_Headers.Remove(removeName)) WriteLine("Header removed.");
                    else WriteLine("Header not found.");
                    break;
                case "clear":
                    _Headers.Clear();
                    WriteLine("Headers cleared.");
                    break;
                default:
                    WriteLine("No changes made.");
                    break;
            }
        }

        private static void ManageSubprotocols()
        {
            WriteLine("Subprotocols: " + (_Subprotocols.Count > 0 ? String.Join(", ", _Subprotocols) : "(none)"));
            Console.Write("Subprotocol command [add/remove/clear]> ");
            string command = (Console.ReadLine() ?? String.Empty).Trim().ToLowerInvariant();

            switch (command)
            {
                case "add":
                    Console.Write("Subprotocol> ");
                    string add = (Console.ReadLine() ?? String.Empty).Trim();
                    if (String.IsNullOrWhiteSpace(add))
                    {
                        WriteLine("Subprotocol is required.");
                        return;
                    }

                    if (!_Subprotocols.Contains(add, StringComparer.OrdinalIgnoreCase))
                    {
                        _Subprotocols.Add(add);
                    }

                    WriteLine("Subprotocol added.");
                    break;
                case "remove":
                    Console.Write("Subprotocol> ");
                    string remove = (Console.ReadLine() ?? String.Empty).Trim();
                    _Subprotocols.RemoveAll(p => String.Equals(p, remove, StringComparison.OrdinalIgnoreCase));
                    WriteLine("Matching subprotocol entries removed.");
                    break;
                case "clear":
                    _Subprotocols.Clear();
                    WriteLine("Subprotocols cleared.");
                    break;
                default:
                    WriteLine("No changes made.");
                    break;
            }
        }

        private static void ApplyConnectionOptions(ClientWebSocket socket)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            foreach (KeyValuePair<string, string> header in _Headers)
            {
                socket.Options.SetRequestHeader(header.Key, header.Value);
            }

            for (int i = 0; i < _Subprotocols.Count; i++)
            {
                socket.Options.AddSubProtocol(_Subprotocols[i]);
            }
        }

        private static void WriteLine(string message)
        {
            lock (_ConsoleSync)
            {
                Console.WriteLine(message);
            }
        }
    }
}
