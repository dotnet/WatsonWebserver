namespace Test.Benchmark
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Watson benchmark host.
    /// </summary>
    internal class WatsonBenchmarkHost : IBenchmarkHost
    {
        private readonly BenchmarkProtocol _Protocol;
        private readonly BenchmarkOptions _Options;
        private readonly int _Port;
        private readonly string _HelloPayload;
        private readonly byte[] _HelloPayloadBytes;
        private readonly string _JsonPayload;
        private readonly BenchmarkJsonPayload _JsonObjectPayload;
        private readonly string _EventPayload;
        private readonly X509Certificate2 _Certificate;
        private readonly bool _DebugFailures;
        private Webserver _Server = null;
        private bool _PortReleased = false;

        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="protocol">Protocol.</param>
        /// <param name="options">Options.</param>
        public WatsonBenchmarkHost(BenchmarkProtocol protocol, BenchmarkOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _Protocol = protocol;
            _Options = options;
            _Port = BenchmarkPortFactory.GetAvailablePort(BenchmarkTarget.Watson7);
            _HelloPayload = new string('x', _Options.PayloadBytes);
            _HelloPayloadBytes = Encoding.UTF8.GetBytes(_HelloPayload);
            _JsonPayload = BuildJsonPayload(_Options.PayloadBytes);
            _JsonObjectPayload = BuildJsonObjectPayload(_JsonPayload);
            _EventPayload = new string('e', Math.Max(1, _Options.PayloadBytes / 4));
            _DebugFailures = String.Equals(Environment.GetEnvironmentVariable("WATSON_BENCHMARK_DEBUG_FAILURES"), "1", StringComparison.Ordinal);

            bool useTls = protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11;
            if (useTls)
            {
                _Certificate = BenchmarkCertificateFactory.Create();
            }
        }

        /// <inheritdoc />
        public string Name
        {
            get
            {
                return "Watson7";
            }
        }

        /// <inheritdoc />
        public BenchmarkProtocol Protocol
        {
            get
            {
                return _Protocol;
            }
        }

        /// <inheritdoc />
        public Uri BaseAddress
        {
            get
            {
                bool useTls = _Protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11;
                return new Uri((useTls ? "https" : "http") + "://127.0.0.1:" + _Port.ToString());
            }
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken token)
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, _Protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11);
            settings.Protocols.EnableHttp2 = _Protocol == BenchmarkProtocol.Http2;
            settings.Protocols.EnableHttp3 = _Protocol == BenchmarkProtocol.Http3;
            settings.Protocols.EnableHttp2Cleartext = false;
            settings.IO.EnableKeepAlive = true;
            settings.IO.MaxRequests = Math.Max(1024, _Options.Concurrency * 16);
            settings.IO.ReadTimeoutMs = Math.Max(30000, _Options.RequestTimeoutSeconds * 1000);
            settings.Protocols.IdleTimeoutMs = Math.Max(30000, _Options.RequestTimeoutSeconds * 1000);

            if (settings.Ssl.Enable)
            {
                settings.Ssl.SslCertificate = _Certificate;
            }

            _Server = new Webserver(settings, DefaultRouteAsync);
            _Server.Settings.WebSockets.Enable = true;
            if (_DebugFailures)
            {
                _Server.Events.ExceptionEncountered += HandleExceptionEncountered;
            }
            AddRoutes(_Server);
            _Server.Start(token);
            return Task.Delay(250, token);
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken token)
        {
            if (_Server != null)
            {
                _Server.Stop();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Server != null)
            {
                if (_DebugFailures)
                {
                    _Server.Events.ExceptionEncountered -= HandleExceptionEncountered;
                }

                _Server.Dispose();
                _Server = null;
            }

            if (_Certificate != null)
            {
                _Certificate.Dispose();
            }

            if (!_PortReleased)
            {
                BenchmarkPortFactory.ReleasePort(_Port);
                _PortReleased = true;
            }
        }

        private void AddRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/hello", HelloRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/benchmark/echo", EchoRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/chunked-response", ChunkedResponseRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/json", JsonRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/serialize-json", SerializeJsonRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/benchmark/json-echo", JsonEchoRouteAsync);
            server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/sse", ServerSentEventsRouteAsync);
            server.WebSocket("/benchmark/ws-echo", WebSocketEchoRouteAsync);
            server.WebSocket("/benchmark/ws-client-text", WebSocketClientTextRouteAsync);
            server.WebSocket("/benchmark/ws-server-text", WebSocketServerTextRouteAsync);
            server.WebSocket("/benchmark/ws-connect-close", WebSocketConnectCloseRouteAsync);
        }

        private Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            return context.Response.Send("not-found", context.Token);
        }

        private Task HelloRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            return context.Response.Send(_HelloPayloadBytes, context.Token);
        }

        private Task EchoRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            if (context.Request.ChunkedTransfer)
            {
                return SendChunkedEchoAsync(context);
            }

            if (context.Request.ContentLength > 0 && context.Request.Data != null)
            {
                return context.Response.Send(context.Request.ContentLength, context.Request.Data, context.Token);
            }

            return context.Response.Send(Array.Empty<byte>(), context.Token);
        }

        private static async Task SendChunkedEchoAsync(HttpContextBase context)
        {
            byte[] body = context.Request.DataAsBytes ?? Array.Empty<byte>();
            context.Response.ContentLength = body != null ? body.Length : 0;
            await context.Response.Send(body ?? Array.Empty<byte>(), context.Token).ConfigureAwait(false);
        }

        private Task JsonRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return context.Response.Send(_JsonPayload, context.Token);
        }

        private async Task ChunkedResponseRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            context.Response.ChunkedTransfer = true;

            const int chunkSize = 32;
            for (int offset = 0; offset < _HelloPayloadBytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, _HelloPayloadBytes.Length - offset);
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(_HelloPayloadBytes, offset, chunk, 0, count);
                await context.Response.SendChunk(chunk, offset + count >= _HelloPayloadBytes.Length, context.Token).ConfigureAwait(false);
            }
        }

        private Task SerializeJsonRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return context.Response.Send(_Server.Serializer.SerializeJson(_JsonObjectPayload, false), context.Token);
        }

        private Task JsonEchoRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return context.Response.Send(context.Request.DataAsString ?? String.Empty, context.Token);
        }

        private async Task ServerSentEventsRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ServerSentEvents = true;

            for (int i = 0; i < _Options.ServerSentEventCount; i++)
            {
                ServerSentEvent serverSentEvent = new ServerSentEvent
                {
                    Event = "benchmark",
                    Data = _EventPayload
                };

                await context.Response.SendEvent(serverSentEvent, i == (_Options.ServerSentEventCount - 1), context.Token).ConfigureAwait(false);
            }
        }

        private async Task WebSocketEchoRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            await foreach (WebSocketMessage message in session.ReadMessagesAsync(context.Token).ConfigureAwait(false))
            {
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    await session.SendTextAsync(message.Text, context.Token).ConfigureAwait(false);
                }
                else
                {
                    await session.SendBinaryAsync(message.Data, context.Token).ConfigureAwait(false);
                }
            }
        }

        private async Task WebSocketClientTextRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            await foreach (WebSocketMessage message in session.ReadMessagesAsync(context.Token).ConfigureAwait(false))
            {
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    await session.SendTextAsync("ok", context.Token).ConfigureAwait(false);
                }
            }
        }

        private async Task WebSocketServerTextRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            await foreach (WebSocketMessage message in session.ReadMessagesAsync(context.Token).ConfigureAwait(false))
            {
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    await session.SendTextAsync(_HelloPayload, context.Token).ConfigureAwait(false);
                }
            }
        }

        private async Task WebSocketConnectCloseRouteAsync(HttpContextBase context, WebSocketSession session)
        {
            await session.ReceiveAsync(context.Token).ConfigureAwait(false);
        }

        private static string BuildJsonPayload(int payloadBytes)
        {
            int clampedPayloadBytes = payloadBytes;
            if (clampedPayloadBytes < 128) clampedPayloadBytes = 128;

            BenchmarkJsonPayload payload = new BenchmarkJsonPayload();
            payload.Message = "benchmark";
            payload.Category = "json";
            payload.Sequence = 1;
            payload.Content = String.Empty;

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions();
            serializerOptions.WriteIndented = false;

            string json = JsonSerializer.Serialize(payload, serializerOptions);
            if (System.Text.Encoding.UTF8.GetByteCount(json) >= clampedPayloadBytes) return json;

            int remainingBytes = clampedPayloadBytes - System.Text.Encoding.UTF8.GetByteCount(json);
            payload.Content = new string('j', remainingBytes);

            json = JsonSerializer.Serialize(payload, serializerOptions);
            while (System.Text.Encoding.UTF8.GetByteCount(json) > clampedPayloadBytes && payload.Content.Length > 0)
            {
                payload.Content = payload.Content.Substring(0, payload.Content.Length - 1);
                json = JsonSerializer.Serialize(payload, serializerOptions);
            }

            return json;
        }

        private static BenchmarkJsonPayload BuildJsonObjectPayload(string json)
        {
            return JsonSerializer.Deserialize<BenchmarkJsonPayload>(json);
        }

        private void HandleExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            if (!_DebugFailures) return;
            if (args == null || args.Exception == null) return;

            Console.Error.WriteLine(
                "[watson-server-exception] "
                + Protocol.ToString()
                + " :: "
                + args.Exception.GetType().Name
                + " :: "
                + args.Exception.Message);
        }

    }
}
