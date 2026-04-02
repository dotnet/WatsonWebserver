namespace WatsonWebserver
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.WebSockets;
    using System.Net.Quic;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text.Json.Serialization;
    using System.Runtime.Versioning;
    using WatsonWebserver.Http1;
    using WatsonWebserver.Http2;
    using WatsonWebserver.Http3;
    using WatsonWebserver.WebSockets;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http1;
    using WatsonWebserver.Core.Http2;
    using WatsonWebserver.Core.Http3;
    using WatsonWebserver.Core.WebSockets;
    using System.Runtime.InteropServices;
    using System.Text;
    using NetWebSocket = System.Net.WebSockets.WebSocket;

    /// <summary>
    /// Watson webserver.
    /// </summary>
    public class Webserver : WebserverBase, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public override bool IsListening
        {
            get
            {
                return (_TcpListener != null) ? _IsListening : false;
            }
        }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public override int RequestCount
        {
            get
            {
                return _RequestCount;
            }
        }

        /// <summary>
        /// Indicates whether the current transport supports HTTP/2.
        /// </summary>
        protected override bool SupportsHttp2
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Indicates whether the current transport supports HTTP/3.
        /// </summary>
        protected override bool SupportsHttp3
        {
            get
            {
                return Http3RuntimeDetector.Detect().IsAvailable;
            }
        }

        /// <summary>
        /// Execute a matched WebSocket route.
        /// </summary>
        protected override async Task<bool> ProcessWebSocketRouteAsync(
            HttpContextBase ctx,
            WebSocketRoute route,
            Func<HttpContextBase, WebSocketSession, Task> handler,
            CancellationToken token)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (route == null) throw new ArgumentNullException(nameof(route));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Stream stream = ctx.Request?.Data;

            if (stream is Http1.ContentLengthStream contentLengthStream)
            {
                stream = contentLengthStream.InnerStream;
            }

            if (stream == null || !stream.CanWrite)
            {
                return false;
            }

            if (!Http1WebSocketHandshake.TryValidate(Settings, ctx, out int statusCode, out string failureReason, out Dictionary<string, string> responseHeaders, out string acceptKey))
            {
                await Http1WebSocketHandshake.SendFailureResponseAsync(stream, statusCode, failureReason, responseHeaders, token).ConfigureAwait(false);
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ResponseSent = true;

                if (Events.HasWebSocketHandshakeFailedHandlers)
                {
                    Events.HandleWebSocketHandshakeFailed(this, new WebSocketHandshakeFailureEventArgs(ctx, failureReason));
                }

                return true;
            }

            ctx.Response.StatusCode = 101;
            await Http1WebSocketHandshake.SendUpgradeResponseAsync(stream, acceptKey, null, token).ConfigureAwait(false);
            ctx.Response.ResponseSent = true;

            NetWebSocket socket = NetWebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
            WebSocketSession session = CreateWebSocketSession(ctx, socket);

            WebSocketConnections.Add(session);

            if (Events.HasWebSocketSessionStartedHandlers)
            {
                Events.HandleWebSocketSessionStarted(this, new WebSocketSessionEventArgs(ctx, session));
            }

            try
            {
                try
                {
                    await handler(ctx, session).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, e));

                    try
                    {
                        await session.CloseAsync(WebSocketCloseStatus.InternalServerError, "WebSocket route handler exception.", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }

                    return true;
                }

                if (session.IsConnected)
                {
                    await session.CloseAsync(WebSocketCloseStatus.NormalClosure, "WebSocket route completed.", CancellationToken.None).ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                WebSocketConnections.Remove(session.Id);

                if (Events.HasWebSocketSessionEndedHandlers)
                {
                    Events.HandleWebSocketSessionEnded(this, new WebSocketSessionEventArgs(ctx, session));
                }
            }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[Webserver] ";
        private TcpListener _TcpListener = null;
        private QuicListener _QuicListener = null;
        private int _RequestCount = 0;
        private SemaphoreSlim _RequestSemaphore = null;
        private bool _IsListening = false;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private Task _AcceptConnections = null;
        private Task _AcceptQuicConnections = null;
        private readonly ConcurrentBag<HttpContext> _HttpContextPool = new ConcurrentBag<HttpContext>();
        private readonly ConcurrentBag<HttpRequest> _HttpRequestPool = new ConcurrentBag<HttpRequest>();
        private readonly ConcurrentBag<HttpResponse> _HttpResponsePool = new ConcurrentBag<HttpResponse>();
        private int _RetainedHttpContextCount = 0;
        private int _RetainedHttpRequestCount = 0;
        private int _RetainedHttpResponseCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Creates a new instance of the webserver.
        /// If you do not provide a settings object, default settings will be used, which will cause the webserver to listen on http://localhost:8000, and send events to the console.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Webserver(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute) : base(settings, defaultRoute)
        {
            if (settings == null) settings = new WebserverSettings();

            Settings = settings;

            string hostnameForHeader = settings.UseMachineHostname ? GetBestLocalHostName() : settings.Hostname;
            Settings.Headers.DefaultHeaders[WebserverConstants.HeaderHost] = hostnameForHeader + ":" + settings.Port;

            Routes.Default = defaultRoute;

            _Header = "[Webserver " + Settings.Prefix + "] ";
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use this object after disposal.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        public override void Start(CancellationToken token = default)
        {
            if (_TcpListener != null && _IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");
            NormalizeProtocolSettingsForCurrentRuntime();
            ValidateSettings();
            if (Settings.Ssl.Enable && Settings.Ssl.SslCertificate == null) throw new WebserverConfigurationException("SSL is enabled but no certificate is configured. Set Settings.Ssl.SslCertificate or PfxCertificateFile.");

            Statistics = new WebserverStatistics();

            DisposeTokenSource();
            DisposeRequestSemaphore();
            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;
            _RequestSemaphore = new SemaphoreSlim(Settings.IO.MaxRequests, Settings.IO.MaxRequests);

            _TcpListener = new TcpListener(ResolveBindIpAddress(Settings.Hostname), Settings.Port);
            _TcpListener.Start();

            if (Settings.Protocols.EnableHttp3 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            {
                _QuicListener = BuildQuicListenerAsync(_Token).GetAwaiter().GetResult();
                _AcceptQuicConnections = StartQuicAcceptLoop();
            }

            _IsListening = true;

            _AcceptConnections = Task.Run(() => AcceptConnections(_Token), _Token);

            Events.HandleServerStarted(this, EventArgs.Empty);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        /// <returns>Task.</returns>
        public override Task StartAsync(CancellationToken token = default)
        {
            if (_TcpListener != null && _IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");
            NormalizeProtocolSettingsForCurrentRuntime();
            ValidateSettings();
            if (Settings.Ssl.Enable && Settings.Ssl.SslCertificate == null) throw new WebserverConfigurationException("SSL is enabled but no certificate is configured. Set Settings.Ssl.SslCertificate or PfxCertificateFile.");

            Statistics = new WebserverStatistics();

            DisposeTokenSource();
            DisposeRequestSemaphore();
            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;
            _RequestSemaphore = new SemaphoreSlim(Settings.IO.MaxRequests, Settings.IO.MaxRequests);

            _TcpListener = new TcpListener(ResolveBindIpAddress(Settings.Hostname), Settings.Port);
            _TcpListener.Start();
            _IsListening = true;

            return StartTransportLoopsAsync();
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public override void Stop()
        {
            if (!_IsListening) throw new InvalidOperationException("WatsonWebserver is already stopped.");

            CloseActiveWebSocketSessions();

            if (_TcpListener != null && _IsListening)
            {
                _TcpListener.Stop();
                _IsListening = false;
            }

            if (_QuicListener != null && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            {
                _QuicListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _QuicListener = null;
            }

            if (_TokenSource != null && !_TokenSource.IsCancellationRequested)
            {
                _TokenSource.Cancel();
            }

            DisposeTokenSource();
        }

        #endregion

        #region Private-Methods

        private void NormalizeProtocolSettingsForCurrentRuntime()
        {
            Http3RuntimeAvailability availability = Http3RuntimeDetector.Detect();
            WebserverSettingsValidator.NormalizeForRuntime(Settings, availability, Events.Logger);
        }

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use this object after disposal.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_TcpListener != null && _IsListening)
                {
                    Stop();
                }
                else
                {
                    CloseActiveWebSocketSessions();
                }

                Events.HandleServerDisposing(this, EventArgs.Empty);

                if (_RequestSemaphore != null)
                {
                    _RequestSemaphore.Dispose();
                    _RequestSemaphore = null;
                }

                _TcpListener = null;
                _QuicListener = null;
                Settings = null;
                DisposeTokenSource();
                _AcceptConnections = null;
                _AcceptQuicConnections = null;
            }
        }

        private void DisposeTokenSource()
        {
            if (_TokenSource == null) return;

            if (!_TokenSource.IsCancellationRequested)
            {
                _TokenSource.Cancel();
            }

            _TokenSource.Dispose();
            _TokenSource = null;
        }

        private void DisposeRequestSemaphore()
        {
            if (_RequestSemaphore == null) return;
            _RequestSemaphore.Dispose();
            _RequestSemaphore = null;
        }

        private async Task StartTransportLoopsAsync()
        {
            if (Settings.Protocols.EnableHttp3 && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            {
                _QuicListener = await BuildQuicListenerAsync(_Token).ConfigureAwait(false);
                _AcceptQuicConnections = StartQuicAcceptLoop();
            }

            _AcceptConnections = Task.Run(() => AcceptConnections(_Token), _Token);
            Events.HandleServerStarted(this, EventArgs.Empty);
            await _AcceptConnections.ConfigureAwait(false);
        }

        private async Task AcceptConnections(CancellationToken token)
        {
            try
            {
                #region Process-Requests

                while (_IsListening)
                {
                    TcpClient tcpClient = await _TcpListener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    ThreadPool.UnsafeQueueUserWorkItem(new ClientConnectionWorkItem(this, tcpClient, token), preferLocal: false);
                }

                #endregion
            }
            catch (Exception e)
            {
                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, e));
            }
            finally
            {
                _IsListening = false;
                Events.HandleServerStopped(this, EventArgs.Empty);
            }
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task<QuicListener> BuildQuicListenerAsync(CancellationToken token)
        {
            IPAddress bindAddress = ResolveBindIpAddress(Settings.Hostname);
            QuicListenerOptions listenerOptions = new QuicListenerOptions();
            listenerOptions.ListenEndPoint = new IPEndPoint(bindAddress, Settings.Port);
            listenerOptions.ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 };
            listenerOptions.ListenBacklog = Math.Max(16, Settings.IO.MaxRequests);
            listenerOptions.ConnectionOptionsCallback = BuildQuicConnectionOptionsAsync;
            return await QuicListener.ListenAsync(listenerOptions, token).ConfigureAwait(false);
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private ValueTask<QuicServerConnectionOptions> BuildQuicConnectionOptionsAsync(QuicConnection connection, SslClientHelloInfo clientHelloInfo, CancellationToken token)
        {
            SslServerAuthenticationOptions authenticationOptions = new SslServerAuthenticationOptions();
            authenticationOptions.ServerCertificate = Settings.Ssl.SslCertificate;
            authenticationOptions.ClientCertificateRequired = Settings.Ssl.MutuallyAuthenticate;
            authenticationOptions.EnabledSslProtocols = SslProtocols.Tls13;
            authenticationOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
            authenticationOptions.ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 };

            QuicServerConnectionOptions connectionOptions = new QuicServerConnectionOptions();
            connectionOptions.ServerAuthenticationOptions = authenticationOptions;
            connectionOptions.MaxInboundBidirectionalStreams = Math.Max(1, Settings.Protocols.MaxConcurrentStreams);
            connectionOptions.MaxInboundUnidirectionalStreams = Math.Max(3, Settings.Protocols.MaxConcurrentStreams + 2);
            connectionOptions.IdleTimeout = TimeSpan.FromMilliseconds(Settings.Protocols.IdleTimeoutMs);
            connectionOptions.DefaultCloseErrorCode = 0;
            connectionOptions.DefaultStreamErrorCode = 0;
            return ValueTask.FromResult(connectionOptions);
        }

        private void CloseActiveWebSocketSessions()
        {
            List<WebSocketSession> sessions = ListWebSocketSessions().ToList();
            for (int i = 0; i < sessions.Count; i++)
            {
                WebSocketSession session = sessions[i];
                if (session == null) continue;

                try
                {
                    if (session.IsConnected)
                    {
                        session.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server stopping.", CancellationToken.None).GetAwaiter().GetResult();
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        session.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private Task StartQuicAcceptLoop()
        {
            return Task.Run(() => AcceptQuicConnectionsAsync(_Token), _Token);
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task AcceptQuicConnectionsAsync(CancellationToken token)
        {
            try
            {
                while (_IsListening && _QuicListener != null)
                {
                    QuicConnection quicConnection = await _QuicListener.AcceptConnectionAsync(token).ConfigureAwait(false);
                    ThreadPool.UnsafeQueueUserWorkItem(new QuicConnectionWorkItem(this, quicConnection, token), preferLocal: false);
                }
            }
            catch (Exception e)
            {
                if (_IsListening)
                {
                    Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, e));
                }
            }
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task HandleQuicConnectionAsync(QuicConnection quicConnection, CancellationToken token)
        {
            if (quicConnection == null) throw new ArgumentNullException(nameof(quicConnection));
            bool connectionSlotAcquired = false;

            try
            {
                await _RequestSemaphore.WaitAsync(token).ConfigureAwait(false);
                connectionSlotAcquired = true;

                Events.HandleConnectionReceived(this, new ConnectionEventArgs(HttpProtocol.Http3, Guid.NewGuid(), quicConnection.RemoteEndPoint.Address.ToString(), quicConnection.RemoteEndPoint.Port));

                Http3ConnectionSession session = new Http3ConnectionSession(
                    Settings,
                    Events,
                    quicConnection,
                    async (httpContext, cancellationToken) => await ProcessHttpContextAsync(httpContext, _Header, cancellationToken).ConfigureAwait(false));
                await session.RunAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, e));
            }
            finally
            {
                if (connectionSlotAcquired)
                {
                    try { _RequestSemaphore.Release(); } catch (ObjectDisposedException) { }
                }

                try
                {
                    await quicConnection.CloseAsync(0, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                await quicConnection.DisposeAsync().ConfigureAwait(false);
            }
        }

        private async Task<ClientStreamContext> BuildClientStreamAsync(TcpClient tcpClient, CancellationToken token)
        {
            if (tcpClient == null) throw new ArgumentNullException(nameof(tcpClient));

            Stream stream = tcpClient.GetStream();
            ClientStreamContext context = new ClientStreamContext();

            if (!Settings.Ssl.Enable)
            {
                context.Protocol = HttpProtocol.Http1;
                context.Stream = stream;
                return context;
            }

            SslStream sslStream = new SslStream(stream, false);
            SslServerAuthenticationOptions authenticationOptions = new SslServerAuthenticationOptions();
            authenticationOptions.ServerCertificate = Settings.Ssl.SslCertificate;
            authenticationOptions.ClientCertificateRequired = Settings.Ssl.MutuallyAuthenticate;
            authenticationOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            authenticationOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;

            if (Settings.Protocols.EnableHttp2)
            {
                authenticationOptions.ApplicationProtocols = new List<SslApplicationProtocol>();
                authenticationOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http2);
                if (Settings.Protocols.EnableHttp1) authenticationOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http11);
            }

            await sslStream.AuthenticateAsServerAsync(authenticationOptions, token).ConfigureAwait(false);

            context.Protocol = sslStream.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2 ? HttpProtocol.Http2 : HttpProtocol.Http1;
            context.Stream = sslStream;
            return context;
        }

        private static readonly byte[] _ContinueResponseBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

        private async Task SendContinueAsync(Stream stream, CancellationToken token)
        {
            if (stream == null || !stream.CanWrite) return;
            await stream.WriteAsync(_ContinueResponseBytes, 0, _ContinueResponseBytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task SendBadRequestAsync(Stream stream, CancellationToken token)
        {
            if (stream == null || !stream.CanWrite) return;

            string response =
                "HTTP/1.1 400 Bad Request\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "Date: " + DateTime.UtcNow.ToString(WebserverConstants.HeaderDateValueFormat) + "\r\n" +
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task SendHttpVersionNotSupportedAsync(Stream stream, CancellationToken token)
        {
            if (stream == null || !stream.CanWrite) return;

            string response =
                "HTTP/1.1 505 HTTP Version Not Supported\r\n" +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n" +
                "Date: " + DateTime.UtcNow.ToString(WebserverConstants.HeaderDateValueFormat) + "\r\n" +
                "\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private async Task HandleClientConnectionAsync(TcpClient tcpClient, CancellationToken token)
        {
            if (tcpClient == null) throw new ArgumentNullException(nameof(tcpClient));

            HttpContext ctx = null;
            Stream clientStream = null;
            ClientStreamContext streamContext = null;
            bool connectionSlotAcquired = false;

            try
            {
                await _RequestSemaphore.WaitAsync(token).ConfigureAwait(false);
                connectionSlotAcquired = true;

                tcpClient.NoDelay = true;
                streamContext = await BuildClientStreamAsync(tcpClient, token).ConfigureAwait(false);
                clientStream = streamContext.Stream;

                IPEndPoint sourceEndpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                IPEndPoint destinationEndpoint = (IPEndPoint)tcpClient.Client.LocalEndPoint;
                string sourceIp = sourceEndpoint.Address.ToString();
                int sourcePort = sourceEndpoint.Port;
                string destinationIp = destinationEndpoint.Address.ToString();
                int destinationPort = destinationEndpoint.Port;

                if (!Settings.Ssl.Enable && Settings.Protocols.EnableHttp2 && Settings.Protocols.EnableHttp2Cleartext)
                {
                    streamContext = await DetectCleartextProtocolAsync(streamContext, token).ConfigureAwait(false);
                    clientStream = streamContext.Stream;
                }

                Events.HandleConnectionReceived(this, new ConnectionEventArgs(streamContext.Protocol, Guid.NewGuid(), sourceIp, sourcePort));

                if (streamContext.Protocol == HttpProtocol.Http2)
                {
                    if (!Settings.Protocols.EnableHttp2)
                    {
                        await SendHttpVersionNotSupportedAsync(clientStream, token).ConfigureAwait(false);
                        return;
                    }

                        Http2ConnectionSession session = new Http2ConnectionSession(
                        Settings,
                        Events,
                        clientStream,
                        async (httpContext, cancellationToken) => await ProcessHttpContextAsync(httpContext, _Header, cancellationToken).ConfigureAwait(false),
                        sourceEndpoint,
                        destinationEndpoint,
                        Settings.Ssl.Enable);
                    await session.RunAsync(token).ConfigureAwait(false);
                    return;
                }

                if (!Settings.Protocols.EnableHttp1)
                {
                    await SendHttpVersionNotSupportedAsync(clientStream, token).ConfigureAwait(false);
                    return;
                }

                bool keepAlive = true;
                bool firstRequest = true;
                using (CancellationTokenSource connectionReadTimeout = new CancellationTokenSource())
                {
                    using (CancellationTokenRegistration readTimeoutRegistration = token.Register(static state => ((CancellationTokenSource)state).Cancel(), connectionReadTimeout))
                    {
                        while (_IsListening && keepAlive && !token.IsCancellationRequested)
                        {
                            Interlocked.Increment(ref _RequestCount);

                            try
                            {
                                if (clientStream == null || !clientStream.CanRead) break;

                                connectionReadTimeout.CancelAfter(firstRequest ? Settings.IO.ReadTimeoutMs : Settings.Protocols.IdleTimeoutMs);
                                Http1HeaderReadResult headerReadResult = await Http1HeaderReader.ReadAsync(clientStream, Settings.IO.MaxIncomingHeadersSize, connectionReadTimeout.Token).ConfigureAwait(false);
                                connectionReadTimeout.CancelAfter(Timeout.InfiniteTimeSpan);
                                if (headerReadResult == null || headerReadResult.HeaderBytes == null || headerReadResult.HeaderBytes.Length < 1) break;
                                if (headerReadResult.PrefixBytes != null && headerReadResult.PrefixBytes.Length > 0)
                                {
                                    if (clientStream is PrefixBufferedStream prefixBufferedStream && prefixBufferedStream.PrefixConsumed)
                                    {
                                        clientStream = prefixBufferedStream.InnerStream;
                                    }

                                    clientStream = new PrefixBufferedStream(clientStream, headerReadResult.PrefixBytes);
                                }

                                Http1RequestMetadata requestMetadata = Http1RequestParser.Parse(
                                    Settings,
                                    sourceIp,
                                    sourcePort,
                                    destinationIp,
                                    destinationPort,
                                    headerReadResult.HeaderBytes);

                                if (requestMetadata.ExpectContinue)
                                {
                                    await SendContinueAsync(clientStream, token).ConfigureAwait(false);
                                }

                                ctx = RentHttpContext(clientStream, requestMetadata);

                                await ProcessHttpContextAsync(ctx, _Header, token).ConfigureAwait(false);
                                keepAlive = ShouldKeepConnectionOpen(ctx);
                                firstRequest = false;
                            }
                            catch (MalformedHttpRequestException e)
                            {
                                await SendBadRequestAsync(clientStream, token).ConfigureAwait(false);
                                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, e));
                                break;
                            }
                            catch (IOException)
                            {
                                break;
                            }
                            finally
                            {
                                Interlocked.Decrement(ref _RequestCount);

                                if (ctx != null)
                                {
                                    ReturnHttpContext(ctx);
                                    ctx = null;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, e));
            }
            finally
            {
                if (connectionSlotAcquired)
                {
                    try { _RequestSemaphore.Release(); } catch (ObjectDisposedException) { }
                }

                try
                {
                    clientStream?.Dispose();
                    tcpClient.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }

        private bool ShouldKeepConnectionOpen(HttpContext ctx)
        {
            if (ctx == null) return false;
            if (ctx.RouteType == WatsonWebserver.Core.Routing.RouteTypeEnum.WebSocket) return false;
            if (!Settings.IO.EnableKeepAlive) return false;
            if (ctx.Response == null || !ctx.Response.ResponseSent) return false;
            if (!ctx.Request.Keepalive) return false;
            if (ctx.Response.ServerSentEvents) return false;

            HttpRequest request = ctx.Request as HttpRequest;
            if (request == null) return false;

            return request.IsRequestBodyComplete;
        }

        private WebSocketSession CreateWebSocketSession(HttpContextBase ctx, NetWebSocket socket)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (socket == null) throw new ArgumentNullException(nameof(socket));

            WebSocketRequestDescriptor request = WebSocketRequestDescriptor.FromHttpContext(ctx);
            Guid sessionGuid = ResolveWebSocketSessionGuid(ctx.Request);
            return new WebSocketSession(
                socket,
                request,
                sessionGuid,
                Settings.WebSockets.ReceiveBufferSize,
                Settings.WebSockets.MaxMessageSize,
                Settings.WebSockets.CloseHandshakeTimeoutMs);
        }

        private Guid ResolveWebSocketSessionGuid(HttpRequestBase request)
        {
            if (request == null) return Guid.NewGuid();
            if (!Settings.WebSockets.AllowClientSuppliedGuid) return Guid.NewGuid();

            string headerValue = request.RetrieveHeaderValue(Settings.WebSockets.ClientGuidHeaderName);
            if (Guid.TryParse(headerValue, out Guid guid) && guid != Guid.Empty)
            {
                return guid;
            }

            return Guid.NewGuid();
        }

        private HttpContext RentHttpContext(Stream stream, Http1RequestMetadata requestMetadata)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (requestMetadata == null) throw new ArgumentNullException(nameof(requestMetadata));

            if (!_HttpRequestPool.TryTake(out HttpRequest request))
            {
                request = new HttpRequest();
            }
            else
            {
                Interlocked.Decrement(ref _RetainedHttpRequestCount);
            }

            if (!_HttpResponsePool.TryTake(out HttpResponse response))
            {
                response = new HttpResponse();
            }
            else
            {
                Interlocked.Decrement(ref _RetainedHttpResponseCount);
            }

            if (!_HttpContextPool.TryTake(out HttpContext context))
            {
                context = new HttpContext();
            }
            else
            {
                Interlocked.Decrement(ref _RetainedHttpContextCount);
            }

            request.Initialize(Settings, stream, requestMetadata);
            response.Initialize(request, Settings, Events, stream, Settings.IO.StreamBufferSize);
            context.Initialize(
                new ConnectionMetadata
                {
                    Protocol = HttpProtocol.Http1,
                    IsEncrypted = Settings.Ssl.Enable,
                    Source = new SourceDetails(requestMetadata.Source.IpAddress, requestMetadata.Source.Port),
                    Destination = new DestinationDetails(requestMetadata.Destination.IpAddress, requestMetadata.Destination.Port, Settings.Hostname)
                },
                request,
                response);

            return context;
        }

        private void ReturnHttpContext(HttpContext context)
        {
            if (context == null) return;

            HttpRequest request = context.Request as HttpRequest;
            HttpResponse response = context.Response as HttpResponse;

            response?.ReturnToPool();
            request?.ReturnToPool();
            context.ReturnToPool();

            if (response != null && TryRetainHttp1PooledObject(ref _RetainedHttpResponseCount))
            {
                _HttpResponsePool.Add(response);
            }

            if (request != null && TryRetainHttp1PooledObject(ref _RetainedHttpRequestCount))
            {
                _HttpRequestPool.Add(request);
            }

            if (TryRetainHttp1PooledObject(ref _RetainedHttpContextCount))
            {
                _HttpContextPool.Add(context);
            }
        }

        private bool TryRetainHttp1PooledObject(ref int retainedCount)
        {
            int limit = GetHttp1PoolMaxRetainedPerType();
            if (limit < 1) return false;

            while (true)
            {
                int current = retainedCount;
                if (current >= limit) return false;

                if (Interlocked.CompareExchange(ref retainedCount, current + 1, current) == current)
                {
                    return true;
                }
            }
        }

        private int GetHttp1PoolMaxRetainedPerType()
        {
            if (Settings?.IO?.Http1 == null) return 256;
            return Settings.IO.Http1.PoolMaxRetainedPerType;
        }

        private async Task<ClientStreamContext> DetectCleartextProtocolAsync(ClientStreamContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Stream == null) throw new ArgumentNullException(nameof(context.Stream));

            byte[] probeBytes = await ReadProtocolProbeAsync(context.Stream, token).ConfigureAwait(false);
            PrefixBufferedStream bufferedStream = new PrefixBufferedStream(context.Stream, probeBytes);

            ClientStreamContext detectedContext = new ClientStreamContext();
            detectedContext.Stream = bufferedStream;
            detectedContext.Protocol = Http2ConnectionPreface.IsClientPreface(probeBytes) ? HttpProtocol.Http2 : HttpProtocol.Http1;
            return detectedContext;
        }

        private async Task<byte[]> ReadProtocolProbeAsync(Stream stream, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] buffer = new byte[Http2Constants.ClientConnectionPrefaceBytes.Length];
            int bytesReadTotal = 0;

            using (CancellationTokenSource readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                readTimeout.CancelAfter(Settings.IO.ReadTimeoutMs);

                while (bytesReadTotal < buffer.Length)
                {
                    int bytesRead = await stream.ReadAsync(buffer, bytesReadTotal, buffer.Length - bytesReadTotal, readTimeout.Token).ConfigureAwait(false);
                    if (bytesRead < 1) break;
                    bytesReadTotal += bytesRead;
                }
            }

            byte[] probeBytes = new byte[bytesReadTotal];
            if (bytesReadTotal > 0) Buffer.BlockCopy(buffer, 0, probeBytes, 0, bytesReadTotal);
            return probeBytes;
        }

        private IPAddress ResolveBindIpAddress(string hostname)
        {
            if (String.IsNullOrEmpty(hostname)) return IPAddress.Loopback;
            if (hostname.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)) return IPAddress.Loopback;
            if (hostname.Equals("*")) return IPAddress.Any;
            if (hostname.Equals("+")) return IPAddress.Any;

            if (IPAddress.TryParse(hostname, out IPAddress ipAddress))
            {
                return ipAddress;
            }

            IPAddress[] addresses = Dns.GetHostAddresses(hostname);
            IPAddress address = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (address != null) return address;
            return addresses.First();
        }

        private readonly struct ClientConnectionWorkItem : IThreadPoolWorkItem
        {
            private readonly Webserver _Server;
            private readonly TcpClient _TcpClient;
            private readonly CancellationToken _Token;

            internal ClientConnectionWorkItem(Webserver server, TcpClient tcpClient, CancellationToken token)
            {
                _Server = server;
                _TcpClient = tcpClient;
                _Token = token;
            }

            public void Execute()
            {
                _ = _Server.HandleClientConnectionAsync(_TcpClient, _Token);
            }
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private readonly struct QuicConnectionWorkItem : IThreadPoolWorkItem
        {
            private readonly Webserver _Server;
            private readonly QuicConnection _QuicConnection;
            private readonly CancellationToken _Token;

            internal QuicConnectionWorkItem(Webserver server, QuicConnection quicConnection, CancellationToken token)
            {
                _Server = server;
                _QuicConnection = quicConnection;
                _Token = token;
            }

            public void Execute()
            {
                _ = _Server.HandleQuicConnectionAsync(_QuicConnection, _Token);
            }
        }

        #endregion
    }
}
