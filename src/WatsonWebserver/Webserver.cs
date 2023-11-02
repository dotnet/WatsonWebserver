using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using WatsonWebserver.Core;
using System.Runtime.InteropServices;

namespace WatsonWebserver
{
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
                return (_HttpListener != null) ? _HttpListener.IsListening : false;
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

        #endregion

        #region Private-Members

        private readonly string _Header = "[Webserver] ";
        private HttpListener _HttpListener = new HttpListener();
        private int _RequestCount = 0;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private Task _AcceptConnections = null;

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
            WebserverConstants.HeaderHost = settings.Hostname + ":" + settings.Port;
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
            if (_HttpListener != null && _HttpListener.IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");

            Statistics = new WebserverStatistics();

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;

            _HttpListener = new HttpListener();
            _HttpListener.Prefixes.Add(Settings.Prefix);
            _HttpListener.Start();

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
            if (_HttpListener != null && _HttpListener.IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");

            Statistics = new WebserverStatistics();

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;

            _HttpListener = new HttpListener();
            _HttpListener.Prefixes.Add(Settings.Prefix);
            _HttpListener.Start();

            _AcceptConnections = Task.Run(() => AcceptConnections(_Token), _Token);

            Events.HandleServerStarted(this, EventArgs.Empty);

            return _AcceptConnections;
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public override void Stop()
        {
            if (!_HttpListener.IsListening) throw new InvalidOperationException("WatsonWebserver is already stopped.");

            if (_HttpListener != null && _HttpListener.IsListening)
            {
                _HttpListener.Stop();
            }

            if (_TokenSource != null && !_TokenSource.IsCancellationRequested)
            {
                _TokenSource.Cancel();
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use this object after disposal.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_HttpListener != null && _HttpListener.IsListening)
                {
                    Stop();

                    _HttpListener.Close();
                }

                Events.HandleServerDisposing(this, EventArgs.Empty);

                _HttpListener = null;
                Settings = null;
                Routes = null;
                _TokenSource = null;
                _AcceptConnections = null;

                Events = null;
                Statistics = null;
            }
        }

        private async Task AcceptConnections(CancellationToken token)
        {
            try
            {
                #region Process-Requests

                while (_HttpListener.IsListening)
                {
                    if (_RequestCount >= Settings.IO.MaxRequests)
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                        continue;
                    }

                    HttpListenerContext listenerCtx = await _HttpListener.GetContextAsync().ConfigureAwait(false);
                    listenerCtx.Response.KeepAlive = Settings.IO.EnableKeepAlive;

                    Interlocked.Increment(ref _RequestCount);
                    HttpContext ctx = null;
                    Func<HttpContext, Task> handler = null;

                    Task unawaited = Task.Run(async () =>
                    {
                        try
                        {
                            #region Build-Context

                            Events.HandleConnectionReceived(this, new ConnectionEventArgs(
                                listenerCtx.Request.RemoteEndPoint.Address.ToString(),
                                listenerCtx.Request.RemoteEndPoint.Port));

                            ctx = new HttpContext(listenerCtx, Settings, Events, Serializer);

                            Events.HandleRequestReceived(this, new RequestEventArgs(ctx));

                            if (Settings.Debug.Requests)
                            {
                                Events.Logger?.Invoke(
                                    _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            Statistics.IncrementRequestCounter(ctx.Request.Method);
                            Statistics.IncrementReceivedPayloadBytes(ctx.Request.ContentLength);

                            #endregion

                            #region Check-Access-Control

                            if (!Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                            {
                                Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                                if (Settings.Debug.AccessControl)
                                {
                                    Events.Logger?.Invoke(_Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                                }

                                listenerCtx.Response.StatusCode = 403;
                                listenerCtx.Response.Close();
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
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
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
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
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
                                        await Routes.PreAuthentication.ContentHandler.Process(ctx).ConfigureAwait(false);
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

                                        return;
                                    }
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
                                        await Routes.PostAuthentication.ContentHandler.Process(ctx).ConfigureAwait(false);
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
                                await ctx.Response.Send(DefaultPages.Pages[404].Content).ConfigureAwait(false);
                                return;
                            }

                            #endregion
                        }
                        catch (Exception eInner)
                        {
                            ctx.Response.StatusCode = 500;
                            ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                            await ctx.Response.Send(DefaultPages.Pages[500].Content).ConfigureAwait(false);
                            Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, eInner));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _RequestCount);

                            if (ctx != null)
                            {
                                if (!ctx.Response.ResponseSent)
                                {
                                    ctx.Response.StatusCode = 500;
                                    ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
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

                    }, token);
                }

                #endregion
            }
            catch (Exception e)
            {
                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, e));
            }
            finally
            {
                Events.HandleServerStopped(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}
