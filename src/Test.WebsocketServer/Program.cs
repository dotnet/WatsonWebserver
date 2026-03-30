namespace Test.WebsocketServer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.WebSockets;

    internal static class Program
    {
        private static readonly string _Hostname = "127.0.0.1";
        private static readonly int _Port = 8181;
        private static readonly object _ConsoleSync = new object();
        private static Webserver _Server = null;

        private static async Task Main(string[] args)
        {
            WebserverSettings settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };

            settings.IO.EnableKeepAlive = true;
            settings.IO.ReadTimeoutMs = 30000;
            settings.Protocols.IdleTimeoutMs = 30000;
            settings.WebSockets.Enable = true;
            settings.WebSockets.AllowClientSuppliedGuid = true;

            _Server = new Webserver(settings, DefaultRouteAsync);
            _Server.Events.Logger = msg => WriteLine(msg);
            AttachServerEventHandlers();

            ConfigureHttpRoutes();
            ConfigureWebSocketRoutes();

            WriteBanner();
            _Server.Start();
            WriteLine("Listening on " + _Server.Settings.Prefix);

            bool run = true;
            while (run)
            {
                Console.Write("ws-server> ");
                string input = Console.ReadLine();
                string command = (input ?? String.Empty).Trim().ToLowerInvariant();

                switch (command)
                {
                    case "?":
                    case "help":
                    case "menu":
                        WriteMenu();
                        break;
                    case "routes":
                        WriteBanner();
                        break;
                    case "clients":
                    case "sessions":
                    case "list":
                        PrintSessions();
                        break;
                    case "kick":
                        await KickClientAsync().ConfigureAwait(false);
                        break;
                    case "send":
                        await SendToClientAsync().ConfigureAwait(false);
                        break;
                    case "sendn":
                    case "burst":
                        await SendBurstToClientAsync().ConfigureAwait(false);
                        break;
                    case "sendall":
                        await SendToAllClientsAsync().ConfigureAwait(false);
                        break;
                    case "stats":
                        WriteLine(_Server.Statistics.ToString());
                        break;
                    case "start":
                        if (!_Server.IsListening) _Server.Start();
                        WriteLine("Listening: " + _Server.IsListening);
                        break;
                    case "stop":
                        if (_Server.IsListening) _Server.Stop();
                        WriteLine("Listening: " + _Server.IsListening);
                        break;
                    case "state":
                        WriteLine("Listening: " + _Server.IsListening);
                        WriteLine("Active websocket sessions: " + GetSessions().Count);
                        break;
                    case "cls":
                    case "clear":
                        Console.Clear();
                        break;
                    case "q":
                    case "quit":
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

            if (_Server.IsListening) _Server.Stop();
            _Server.Dispose();
        }

        private static void ConfigureHttpRoutes()
        {
            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/", async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send(BuildRouteSummary(), ctx.Token).ConfigureAwait(false);
            });

            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/healthz", async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("ok", ctx.Token).ConfigureAwait(false);
            });

            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/sessions", async ctx =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";

                List<object> sessions = new List<object>();
                foreach (WebSocketSession session in GetSessions())
                {
                    sessions.Add(new
                    {
                        session.Id,
                        session.IsConnected,
                        session.Request.Path,
                        session.Request.Query,
                        session.Subprotocol,
                        session.Metadata,
                        session.RemoteIp,
                        session.RemotePort,
                        Statistics = session.Statistics.Snapshot()
                    });
                }

                await ctx.Response.Send(_Server.Serializer.SerializeJson(sessions, true), ctx.Token).ConfigureAwait(false);
            });
        }

        private static void AttachServerEventHandlers()
        {
            _Server.Events.ConnectionReceived += (sender, args) =>
                WriteLine("[conn:recv] " + SafeProtocol(args?.Protocol) + " " + SafeText(args?.Ip, "(unknown)") + ":" + SafePort(args?.Port));
            _Server.Events.ConnectionDenied += (sender, args) =>
                WriteLine("[conn:deny] " + SafeProtocol(args?.Protocol) + " " + SafeText(args?.Ip, "(unknown)") + ":" + SafePort(args?.Port));
            _Server.Events.RequestReceived += (sender, args) =>
                WriteLine("[req:recv] " + SafeMethod(args?.Method) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.RequestDenied += (sender, args) =>
                WriteLine("[req:deny] " + SafeMethod(args?.Method) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.RequestAborted += (sender, args) =>
                WriteLine("[req:abort] " + SafeMethod(args?.Method) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.ResponseStarting += (sender, args) =>
                WriteLine("[resp:start] " + SafeStatusCode(args?.StatusCode) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.ResponseSent += (sender, args) =>
                WriteLine("[resp:sent] " + SafeStatusCode(args?.StatusCode) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.ResponseCompleted += (sender, args) =>
                WriteLine("[resp:done] " + SafeStatusCode(args?.StatusCode) + " " + SafeText(args?.Url, "(unknown-url)"));
            _Server.Events.ExceptionEncountered += (sender, args) =>
            {
                if (args?.Exception != null) WriteLine("[error] " + args.Exception.Message);
            };
            _Server.Events.ServerStarted += (sender, args) =>
                WriteLine("[server:start]");
            _Server.Events.ServerStopped += (sender, args) =>
                WriteLine("[server:stop]");
            _Server.Events.ServerDisposing += (sender, args) =>
                WriteLine("[server:dispose]");
            _Server.Events.WebSocketSessionStarted += (sender, args) =>
                WriteLine("[ws:start] " + SafeSessionId(args?.Session) + " " + SafeSessionPath(args?.Session));
            _Server.Events.WebSocketSessionEnded += (sender, args) =>
                WriteLine("[ws:end]   " + SafeSessionId(args?.Session) + " " + SafeSessionPath(args?.Session));
            _Server.Events.WebSocketHandshakeFailed += (sender, args) =>
                WriteLine("[ws:fail]  " + SafeText(args?.Reason, "(no-reason)"));
        }

        private static void ConfigureWebSocketRoutes()
        {
            _Server.WebSocket("/ws/echo", async (ctx, session) =>
            {
                session.Metadata = "echo";
                await session.SendTextAsync("connected:echo", ctx.Token).ConfigureAwait(false);

                await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                {
                    if (message.MessageType == WebSocketMessageType.Text)
                    {
                        await session.SendTextAsync("echo:" + message.Text, ctx.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await session.SendBinaryAsync(message.Data, ctx.Token).ConfigureAwait(false);
                    }
                }
            });

            _Server.WebSocket("/ws/time", async (ctx, session) =>
            {
                session.Metadata = "time";
                await session.SendTextAsync("connected:time", ctx.Token).ConfigureAwait(false);

                await foreach (WebSocketMessage _ in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                {
                    await session.SendTextAsync(DateTime.UtcNow.ToString("O"), ctx.Token).ConfigureAwait(false);
                }
            });

            _Server.WebSocket("/ws/upper", async (ctx, session) =>
            {
                session.Metadata = "upper";
                await session.SendTextAsync("connected:upper", ctx.Token).ConfigureAwait(false);

                await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                {
                    string text = message.Text ?? String.Empty;
                    await session.SendTextAsync(text.ToUpperInvariant(), ctx.Token).ConfigureAwait(false);
                }
            });

            _Server.WebSocket("/ws/inspect", async (ctx, session) =>
            {
                session.Metadata = "inspect";
                string payload = _Server.Serializer.SerializeJson(new
                {
                    session.Id,
                    session.Request.Path,
                    session.Request.Query,
                    Headers = ToHeaderDictionary(session.Request.Headers),
                    session.RemoteIp,
                    session.RemotePort
                }, true);

                await session.SendTextAsync(payload, ctx.Token).ConfigureAwait(false);
                await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
            });

            _Server.WebSocket("/ws/broadcast/{room}", async (ctx, session) =>
            {
                string room = ctx.Request.Url.Parameters["room"] ?? "default";
                session.Metadata = "broadcast:" + room;
                await session.SendTextAsync("connected:broadcast:" + room, ctx.Token).ConfigureAwait(false);

                await foreach (WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                {
                    string senderText = message.Text ?? Convert.ToBase64String(message.Data);
                    string outbound = "[" + room + "] " + session.Id + ": " + senderText;

                    List<Task> sends = new List<Task>();
                    foreach (WebSocketSession other in GetSessions().Where(s => String.Equals(s.Metadata as string, "broadcast:" + room, StringComparison.Ordinal)))
                    {
                        sends.Add(other.SendTextAsync(outbound, ctx.Token));
                    }

                    await Task.WhenAll(sends).ConfigureAwait(false);
                }
            });

            _Server.WebSocket("/ws/server-close", async (ctx, session) =>
            {
                session.Metadata = "server-close";
                await session.SendTextAsync("connected:server-close", ctx.Token).ConfigureAwait(false);
                await session.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "server-close", ctx.Token).ConfigureAwait(false);
            });
        }

        private static Task DefaultRouteAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "text/plain";
            return ctx.Response.Send("not-found", ctx.Token);
        }

        private static void WriteBanner()
        {
            WriteLine("");
            WriteLine("HTTP routes:");
            WriteLine("  GET  /              route summary");
            WriteLine("  GET  /healthz       health probe");
            WriteLine("  GET  /sessions      connected websocket sessions");
            WriteLine("");
            WriteLine("WebSocket routes:");
            WriteLine("  ws://127.0.0.1:8181/ws/echo");
            WriteLine("    text: echoes text back as echo:<message>");
            WriteLine("    binary: echoes raw binary back");
            WriteLine("  ws://127.0.0.1:8181/ws/time");
            WriteLine("    any message returns current UTC timestamp");
            WriteLine("  ws://127.0.0.1:8181/ws/upper");
            WriteLine("    text messages return uppercase");
            WriteLine("  ws://127.0.0.1:8181/ws/inspect?name=alice");
            WriteLine("    sends connection/request metadata as JSON");
            WriteLine("  ws://127.0.0.1:8181/ws/broadcast/general");
            WriteLine("    broadcasts text to all clients in the same room");
            WriteLine("  ws://127.0.0.1:8181/ws/server-close");
            WriteLine("    accepts, sends one message, then closes from the server side");
            WriteLine("");
            WriteMenu();
        }

        private static string BuildRouteSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Watson websocket sample server");
            sb.AppendLine("GET /healthz");
            sb.AppendLine("GET /sessions");
            sb.AppendLine("WS  /ws/echo");
            sb.AppendLine("WS  /ws/time");
            sb.AppendLine("WS  /ws/upper");
            sb.AppendLine("WS  /ws/inspect?name=alice");
            sb.AppendLine("WS  /ws/broadcast/{room}");
            sb.AppendLine("WS  /ws/server-close");
            return sb.ToString();
        }

        private static void WriteMenu()
        {
            WriteLine("Commands:");
            WriteLine("  ? / help    show commands");
            WriteLine("  routes      show route summary");
            WriteLine("  clients     list active websocket sessions");
            WriteLine("  kick        close a selected websocket session");
            WriteLine("  send        send one unsolicited text message to a selected client");
            WriteLine("  sendn       send N unsolicited text messages to a selected client");
            WriteLine("  sendall     send one unsolicited text message to all connected clients");
            WriteLine("  state       show listening state and active-client count");
            WriteLine("  start       start the server");
            WriteLine("  stop        stop the server");
            WriteLine("  stats       print server statistics");
            WriteLine("  clear       clear the screen");
            WriteLine("  quit        exit");
            WriteLine("");
        }

        private static void PrintSessions()
        {
            List<WebSocketSession> sessions = GetSessions();
            if (sessions.Count < 1)
            {
                WriteLine("No active websocket sessions.");
                return;
            }

            WriteLine("Active websocket sessions:");
            for (int i = 0; i < sessions.Count; i++)
            {
                WebSocketSession session = sessions[i];
                WebSocketSessionStatistics stats = session.Statistics.Snapshot();
                WriteLine(
                    (i + 1).ToString().PadLeft(2) + ". " +
                    session.Id +
                    " path=" + session.Request.Path +
                    " remote=" + session.RemoteIp + ":" + session.RemotePort +
                    " connected=" + session.IsConnected +
                    " sent=" + stats.MessagesSent + "/" + stats.BytesSent +
                    " recv=" + stats.MessagesReceived + "/" + stats.BytesReceived +
                    " metadata=" + FormatMetadata(session.Metadata));
            }
        }

        private static async Task KickClientAsync()
        {
            WebSocketSession session = PromptForSession();
            if (session == null) return;

            Console.Write("Close reason [server kick]> ");
            string reason = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(reason)) reason = "server kick";

            bool result = await _Server.DisconnectWebSocketSessionAsync(session.Id, WebSocketCloseStatus.NormalClosure, reason).ConfigureAwait(false);
            WriteLine(result
                ? "Closed " + session.Id + " (" + reason + ")"
                : "Unable to close " + session.Id + ".");
        }

        private static async Task SendToClientAsync()
        {
            WebSocketSession session = PromptForSession();
            if (session == null) return;

            Console.Write("Text payload> ");
            string payload = Console.ReadLine() ?? String.Empty;
            if (String.IsNullOrEmpty(payload))
            {
                WriteLine("No payload sent.");
                return;
            }

            await SendTextToSessionAsync(session, payload).ConfigureAwait(false);
            WriteLine("Sent 1 message to " + session.Id);
        }

        private static async Task SendBurstToClientAsync()
        {
            WebSocketSession session = PromptForSession();
            if (session == null) return;

            Console.Write("Message count> ");
            string countInput = (Console.ReadLine() ?? String.Empty).Trim();
            if (!Int32.TryParse(countInput, out int count) || count < 1)
            {
                WriteLine("Invalid count.");
                return;
            }

            Console.Write("Text payload template [server message]> ");
            string payload = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(payload)) payload = "server message";

            Console.Write("Append index suffix? [y/N]> ");
            string suffixChoice = (Console.ReadLine() ?? String.Empty).Trim();
            bool appendIndex = suffixChoice.Equals("y", StringComparison.OrdinalIgnoreCase) || suffixChoice.Equals("yes", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                string outbound = appendIndex ? payload + " #" + (i + 1) : payload;
                await SendTextToSessionAsync(session, outbound).ConfigureAwait(false);
            }

            WriteLine("Sent " + count + " messages to " + session.Id);
        }

        private static async Task SendToAllClientsAsync()
        {
            List<WebSocketSession> sessions = GetSessions();
            if (sessions.Count < 1)
            {
                WriteLine("No active websocket sessions.");
                return;
            }

            Console.Write("Broadcast text payload> ");
            string payload = Console.ReadLine() ?? String.Empty;
            if (String.IsNullOrWhiteSpace(payload))
            {
                WriteLine("No payload sent.");
                return;
            }

            int sent = 0;
            for (int i = 0; i < sessions.Count; i++)
            {
                WebSocketSession session = sessions[i];
                if (!session.IsConnected) continue;
                await SendTextToSessionAsync(session, payload).ConfigureAwait(false);
                sent++;
            }

            WriteLine("Sent to " + sent + " connected clients.");
        }

        private static async Task SendTextToSessionAsync(WebSocketSession session, string payload)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (!session.IsConnected)
            {
                throw new InvalidOperationException("The selected client is no longer connected.");
            }

            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                await session.SendTextAsync(payload, timeout.Token).ConfigureAwait(false);
            }
        }

        private static WebSocketSession PromptForSession()
        {
            List<WebSocketSession> sessions = GetSessions();
            if (sessions.Count < 1)
            {
                WriteLine("No active websocket sessions.");
                return null;
            }

            PrintSessions();
            Console.Write("Client number or GUID> ");
            string input = (Console.ReadLine() ?? String.Empty).Trim();
            if (String.IsNullOrWhiteSpace(input))
            {
                WriteLine("No client selected.");
                return null;
            }

            if (Int32.TryParse(input, out int index) && index >= 1 && index <= sessions.Count)
            {
                return sessions[index - 1];
            }

            if (Guid.TryParse(input, out Guid guid))
            {
                WebSocketSession match = sessions.FirstOrDefault(s => s.Id == guid);
                if (match != null) return match;
            }

            WebSocketSession prefixMatch = sessions.FirstOrDefault(s => s.Id.ToString().StartsWith(input, StringComparison.OrdinalIgnoreCase));
            if (prefixMatch != null) return prefixMatch;

            WriteLine("Client not found.");
            return null;
        }

        private static List<WebSocketSession> GetSessions()
        {
            return _Server.ListWebSocketSessions()
                .OrderBy(s => s.Request.Path, StringComparer.Ordinal)
                .ThenBy(s => s.Id)
                .ToList();
        }

        private static Dictionary<string, string> ToHeaderDictionary(NameValueCollection headers)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers?.AllKeys == null) return ret;

            for (int i = 0; i < headers.AllKeys.Length; i++)
            {
                string key = headers.AllKeys[i];
                if (String.IsNullOrWhiteSpace(key)) continue;
                ret[key] = headers[key] ?? String.Empty;
            }

            return ret;
        }

        private static string FormatMetadata(object metadata)
        {
            if (metadata == null) return "(null)";
            return metadata.ToString();
        }

        private static string SafeText(string value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string SafeProtocol(HttpProtocol? protocol)
        {
            return protocol.HasValue ? protocol.Value.ToString() : "(unknown-protocol)";
        }

        private static string SafeMethod(HttpMethod? method)
        {
            return method.HasValue ? method.Value.ToString() : "(unknown-method)";
        }

        private static int SafePort(int? port)
        {
            return port ?? 0;
        }

        private static int SafeStatusCode(int? statusCode)
        {
            return statusCode ?? 0;
        }

        private static string SafeSessionId(WebSocketSession session)
        {
            return session != null ? session.Id.ToString() : "(no-session)";
        }

        private static string SafeSessionPath(WebSocketSession session)
        {
            return SafeText(session?.Request?.Path, "(unknown-path)");
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
