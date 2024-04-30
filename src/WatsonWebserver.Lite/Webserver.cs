using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;
using WatsonWebserver.Core;

namespace WatsonWebserver.Lite
{
    /// <summary>
    /// HttpServerLite web server.
    /// </summary>
    public class WebserverLite : WebserverBase, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates if the server is listening for connections.
        /// </summary>
        public override bool IsListening
        {
            get
            {
                if (_TcpServer != null) return _TcpServer.IsListening;
                return false;
            }
        }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public override int RequestCount
        {
            get
            {
                if (_TcpServer != null) return _TcpServer.GetClients().Count();
                return 0;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[WebserverLite] ";
        private CavemanTcpServer _TcpServer = null;
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private int _RequestCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the webserver using the specified settings.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="defaultRoute">Default route.</param>
        public WebserverLite(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute) : base(settings, defaultRoute)
        {
            if (settings == null) settings = new WebserverSettings(); 

            Settings = settings;
            WebserverConstants.HeaderHost = settings.Hostname + ":" + settings.Port;
            Routes = new WebserverRoutes(Settings, defaultRoute);

            _Header = "[Webserver " + Settings.Prefix + "] ";

            InitializeServer(settings.Hostname, settings.Port);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use the object after disposal.
        /// </summary>
        public override void Dispose()
        {
            Events.HandleServerDisposing(this, EventArgs.Empty);

            if (_TcpServer != null)
            {
                if (_TcpServer.IsListening) Stop();

                _TcpServer.Dispose();
                _TcpServer = null;
            }

            if (_TokenSource != null && !_Token.IsCancellationRequested)
            {
                _TokenSource.Cancel();
                _TokenSource.Dispose();
            }

            Settings = null;
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override void Start(CancellationToken token = default)
        {
            if (_TcpServer == null) throw new ObjectDisposedException("Webserver has been disposed.");
            if (_TcpServer.IsListening) throw new InvalidOperationException("Webserver is already running.");

            _TcpServer.Start();

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = _TokenSource.Token;

            Events.HandleServerStarted(this, EventArgs.Empty);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override async Task StartAsync(CancellationToken token = default)
        {
            if (_TcpServer == null) throw new ObjectDisposedException("Webserver has been disposed.");
            if (_TcpServer.IsListening) throw new InvalidOperationException("Webserver is already running.");

            await _TcpServer.StartAsync(token).ConfigureAwait(false);

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = _TokenSource.Token;

            Events.HandleServerStarted(this, EventArgs.Empty);
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public override void Stop()
        {
            if (_TcpServer == null) throw new ObjectDisposedException("Webserver has been disposed.");
            if (!_TcpServer.IsListening) throw new InvalidOperationException("Webserver is already stopped.");

            _TcpServer.Stop();

            Events.HandleServerStopped(this, EventArgs.Empty);
        }

        #endregion

        #region Private-Methods

        private void InitializeServer(string hostname, int port)
        {
            if (!Settings.Ssl.Enable)
            {
                _TcpServer = new CavemanTcpServer(hostname, port);

                _Header = "[WatsonWebserver.Lite http://" + hostname + ":" + port + "] ";
            }
            else
            {
                _TcpServer = new CavemanTcpServer(
                    hostname,
                    port,
                    Settings.Ssl.SslCertificate);

                _Header = "[WatsonWebserver.Lite " + Settings.Prefix + "] ";
            }

            _TcpServer.Settings.MonitorClientConnections = false;
            _TcpServer.Events.ClientConnected += ClientConnected;
            _TcpServer.Events.ClientDisconnected += ClientDisconnected;

            _TcpServer.Settings.MutuallyAuthenticate = Settings.Ssl.MutuallyAuthenticate;
            _TcpServer.Settings.AcceptInvalidCertificates = Settings.Ssl.AcceptInvalidAcertificates;
        }

        private async void ClientConnected(object sender, ClientConnectedEventArgs args)
        {
            #region Check-Max-Requests

            if (_RequestCount >= Settings.IO.MaxRequests)
            {
                _TcpServer.DisconnectClient(args.Client.Guid);
                return;
            }

            Interlocked.Increment(ref _RequestCount);

            #endregion

            #region Parse-IP-Port

            string ipPort = args.Client.IpPort;
            string ip = null;
            int port = 0;
            ParseIpPort(ipPort, out ip, out port);
            HttpContext ctx = null;

            Events.HandleConnectionReceived(this, new ConnectionEventArgs(ip, port));

            #endregion

            #region Process

            try
            {
                #region Retrieve-Headers

                StringBuilder sb = new StringBuilder();

                //                           123456789012345 6 7 8
                // minimum request 16 bytes: GET / HTTP/1.1\r\n\r\n
                int preReadLen = 18;
                ReadResult preReadResult = await _TcpServer.ReadWithTimeoutAsync(
                    Settings.IO.ReadTimeoutMs,
                    args.Client.Guid,
                    preReadLen,
                    _Token).ConfigureAwait(false);

                if (preReadResult.Status != ReadResultStatus.Success
                    || preReadResult.BytesRead != preReadLen
                    || preReadResult.Data == null
                    || preReadResult.Data.Length != preReadLen) return;

                sb.Append(Encoding.ASCII.GetString(preReadResult.Data));

                bool retrievingHeaders = true;
                while (retrievingHeaders)
                {
                    if (sb.ToString().EndsWith("\r\n\r\n"))
                    {
                        retrievingHeaders = false;
                    }
                    else
                    {
                        if (sb.Length >= Settings.IO.MaxIncomingHeadersSize)
                        {
                            Events.HandleConnectionDenied(this, new ConnectionEventArgs(ip, port));
                            Events.Logger?.Invoke(_Header + "failed to read headers from " + ip + ":" + port + " within " + Settings.IO.MaxIncomingHeadersSize + " bytes, closing connection");
                            return;
                        }

                        ReadResult addlReadResult = await _TcpServer.ReadWithTimeoutAsync(
                            Settings.IO.ReadTimeoutMs,
                            args.Client.Guid,
                            1,
                            _Token).ConfigureAwait(false);

                        if (addlReadResult.Status == ReadResultStatus.Success)
                        {
                            sb.Append(Encoding.ASCII.GetString(addlReadResult.Data));
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                #endregion

                #region Build-Context

                ctx = new HttpContext(
                    ipPort,
                    _TcpServer.GetStream(args.Client.Guid),
                    sb.ToString(),
                    Events,
                    Settings.Headers,
                    Settings.IO.StreamBufferSize);

                Statistics.IncrementRequestCounter(ctx.Request.Method);
                Statistics.IncrementReceivedPayloadBytes(ctx.Request.ContentLength);

                Events.HandleRequestReceived(this, new RequestEventArgs(ctx));

                if (Settings.Debug.Requests)
                {
                    Events.Logger?.Invoke(
                        _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                Func<HttpContext, Task> handler = null;

                #endregion

                #region Check-Access-Control

                if (!Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                {
                    Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                    if (Settings.Debug.AccessControl)
                    {
                        Events.Logger?.Invoke(_Header + "request from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                    }

                    return;
                }

                #endregion

                #region Preflight-Handler

                if (ctx.Request.Method == HttpMethod.OPTIONS)
                {
                    if (Routes.Preflight != null)
                    {
                        if (Settings.Debug.Routing)
                        {
                            Events.Logger?.Invoke(
                                _Header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        await Routes.Preflight(ctx).ConfigureAwait(false);
                        if (!ctx.Response.ResponseSent)
                            throw new InvalidOperationException("Preflight route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                        return;
                    }
                }

                #endregion

                #region Pre-Routing-Handler

                if (Routes.PreRouting != null)
                {
                    await Routes.PreRouting(ctx).ConfigureAwait(false);
                    if (ctx.Response.ResponseSent)
                    {
                        if (Settings.Debug.Routing)
                        {
                            Events.Logger?.Invoke(
                                _Header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        return;
                    }
                    else
                    {
                        // allow the connection to continue
                    }
                }

                #endregion

                #region Pre-Authentication

                if (Routes.PreAuthentication != null)
                {
                    #region Static-Routes

                    if (Routes.PreAuthentication.Static != null)
                    {
                        handler = Routes.PreAuthentication.Static.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out StaticRoute sr);
                        if (handler != null)
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "pre-auth static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Static;
                            ctx.Route = sr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Pre-authentication static route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Content-Routes

                    if (Routes.PreAuthentication.Content != null &&
                        (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD))
                    {
                        if (Routes.PreAuthentication.Content.Match(ctx.Request.Url.RawWithoutQuery, out ContentRoute cr))
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "pre-auth content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Content;
                            ctx.Route = cr;
                            await Routes.PreAuthentication.Content.Handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Pre-authentication content route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Parameter-Routes

                    if (Routes.PreAuthentication.Parameter != null)
                    {
                        handler = Routes.PreAuthentication.Parameter.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out NameValueCollection parameters, out ParameterRoute pr);
                        if (handler != null)
                        {
                            ctx.Request.Url.Parameters = parameters;

                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "pre-auth parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Parameter;
                            ctx.Route = pr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Pre-authentication parameter route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Dynamic-Routes

                    if (Routes.PreAuthentication.Dynamic != null)
                    {
                        handler = Routes.PreAuthentication.Dynamic.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out DynamicRoute dr);
                        if (handler != null)
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "pre-auth dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Dynamic;
                            ctx.Route = dr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Pre-authentication dynamic route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion
                }

                #endregion

                #region Authentication

                if (Routes.AuthenticateRequest != null)
                {
                    await Routes.AuthenticateRequest(ctx);
                    if (ctx.Response.ResponseSent)
                    {
                        if (Settings.Debug.Routing)
                        {
                            Events.Logger?.Invoke(_Header + "response sent during authentication for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        return;
                    }
                    else
                    {
                        // allow the connection to continue
                    }
                }

                #endregion

                #region Post-Authentication

                if (Routes.PostAuthentication != null)
                {
                    #region Static-Routes

                    if (Routes.PostAuthentication.Static != null)
                    {
                        handler = Routes.PostAuthentication.Static.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out StaticRoute sr);
                        if (handler != null)
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "post-auth static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Static;
                            ctx.Route = sr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Post-authentication static route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Content-Routes

                    if (Routes.PostAuthentication.Content != null &&
                        (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD))
                    {
                        if (Routes.PostAuthentication.Content.Match(ctx.Request.Url.RawWithoutQuery, out ContentRoute cr))
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "post-auth content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Content;
                            ctx.Route = cr;
                            await Routes.PostAuthentication.Content.Handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Post-authentication content route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Parameter-Routes

                    if (Routes.PostAuthentication.Parameter != null)
                    {
                        handler = Routes.PostAuthentication.Parameter.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out NameValueCollection parameters, out ParameterRoute pr);
                        if (handler != null)
                        {
                            ctx.Request.Url.Parameters = parameters;

                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "post-auth parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Parameter;
                            ctx.Route = pr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Post-authentication parameter route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion

                    #region Dynamic-Routes

                    if (Routes.PostAuthentication.Dynamic != null)
                    {
                        handler = Routes.PostAuthentication.Dynamic.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out DynamicRoute dr);
                        if (handler != null)
                        {
                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "post-auth dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            ctx.RouteType = RouteTypeEnum.Dynamic;
                            ctx.Route = dr;
                            await handler(ctx).ConfigureAwait(false);
                            if (!ctx.Response.ResponseSent)
                                throw new InvalidOperationException("Post-authentication dynamic route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                            return;
                        }
                    }

                    #endregion
                }

                #endregion

                #region Default-Route

                if (Settings.Debug.Routing)
                {
                    Events.Logger?.Invoke(
                        _Header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                if (Routes.Default != null)
                {
                    ctx.RouteType = RouteTypeEnum.Default;
                    await Routes.Default(ctx).ConfigureAwait(false);
                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException("Default route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                    return;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.ContentType = DefaultPages.Pages[404].ContentType;
                    if (ctx.Response.ChunkedTransfer)
                        await ctx.Response.SendFinalChunk(Encoding.UTF8.GetBytes(DefaultPages.Pages[404].Content), _Token).ConfigureAwait(false);
                    else
                        await ctx.Response.Send(DefaultPages.Pages[404].Content, _Token).ConfigureAwait(false);
                    return;
                }

                #endregion
            }
            catch (Exception e)
            {
                if (ctx != null)
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;

                    try
                    {
                        if (ctx.Response.ChunkedTransfer)
                            await ctx.Response.SendFinalChunk(Encoding.UTF8.GetBytes(DefaultPages.Pages[500].Content), _Token).ConfigureAwait(false);
                        else
                            await ctx.Response.Send(DefaultPages.Pages[500].Content, _Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // ignored, exception here is due to disconnected client
                        // this we cannot send the error page
                    }

                    Events.HandleExceptionEncountered(this, new WatsonWebserver.Core.ExceptionEventArgs(ctx, e));
                }

                return;
            }
            finally
            {
                _TcpServer.DisconnectClient(args.Client.Guid);
                Interlocked.Decrement(ref _RequestCount);

                if (ctx != null)
                {
                    if (!ctx.Response.ResponseSent)
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                        if (ctx.Response.ChunkedTransfer)
                            await ctx.Response.SendFinalChunk(Encoding.UTF8.GetBytes(DefaultPages.Pages[500].Content)).ConfigureAwait(false);
                        else
                            await ctx.Response.Send(DefaultPages.Pages[500].Content).ConfigureAwait(false);
                    }

                    ctx.Timestamp.End = DateTime.UtcNow;

                    Events.HandleResponseSent(this, new ResponseEventArgs(ctx, ctx.Timestamp.TotalMs.Value));

                    if (Settings.Debug.Responses)
                    {
                        Events.Logger?.Invoke(
                            _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full + ": " +
                            ctx.Response.StatusCode + " [" + ctx.Timestamp.TotalMs.Value + "ms]");
                    }

                    if (ctx.Response.ContentLength > 0) Statistics.IncrementSentPayloadBytes(Convert.ToInt64(ctx.Response.ContentLength));
                    Routes.PostRouting?.Invoke(ctx).ConfigureAwait(false);
                }
            }

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs args)
        {

        }

        private void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        #endregion
    }
}
