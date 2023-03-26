using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Collections.Specialized;

namespace WatsonWebserver
{
    /// <summary>
    /// Watson webserver.
    /// </summary>
    public class Server : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (_HttpListener != null) ? _HttpListener.IsListening : false;
            }
        }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public int RequestCount
        {
            get
            {
                return _RequestCount;
            }
        }

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        public WatsonWebserverSettings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                if (value == null) _Settings = new WatsonWebserverSettings();
                else _Settings = value;
            }
        }

        /// <summary>
        /// Watson webserver routes.
        /// </summary>
        public WatsonWebserverRoutes Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                if (value == null) _Routes = new WatsonWebserverRoutes();
                else _Routes = value;
            }
        }

        /// <summary>
        /// Watson webserver statistics.
        /// </summary>
        public WatsonWebserverStatistics Statistics { get; private set; } = new WatsonWebserverStatistics();

        /// <summary>
        /// Set specific actions/callbacks to use when events are raised.
        /// </summary>
        public WatsonWebserverEvents Events { get; private set; } = new WatsonWebserverEvents();

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WatsonWebserverPages Pages { get; private set; } = new WatsonWebserverPages();

        /// <summary>
        /// JSON serialization helper.
        /// </summary>
        [JsonIgnore]
        public ISerializationHelper SerializationHelper
        {
            get
            {
                return _Serializer;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(SerializationHelper));
                _Serializer = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[Watson] ";
        private Assembly _Assembly = Assembly.GetCallingAssembly();
        private WatsonWebserverSettings _Settings = new WatsonWebserverSettings();
        private WatsonWebserverRoutes _Routes = new WatsonWebserverRoutes();
        private HttpListener _HttpListener = new HttpListener();
        private int _RequestCount = 0;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private Task _AcceptConnections = null;
        private ISerializationHelper _Serializer = new DefaultSerializationHelper();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// If you do not provide a settings object, default settings will be used, which will cause Watson Webserver to listen on http://127.0.0.1:8000, and send events to the console.
        /// </summary>
        /// <param name="settings">Waton webserver settings.</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Server(WatsonWebserverSettings settings = null, Func<HttpContext, Task> defaultRoute = null)
        {
            if (settings == null)
            {
                settings = new WatsonWebserverSettings();
                settings.Prefixes.Add("http://127.0.0.1:8000/");
                Events.Logger = Console.WriteLine;
            }

            _Settings = settings;
            _Routes.Default = defaultRoute;
        }

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Server(string hostname, int port, bool ssl = false, Func<HttpContext, Task> defaultRoute = null)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));

            _Settings = new WatsonWebserverSettings(hostname, port, ssl);
            _Routes.Default = defaultRoute;
        }

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// </summary>
        /// <param name="hostnames">Hostnames or IP addresses on which to listen.  Note: multiple listener endpoints are not supported on all platforms.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Server(List<string> hostnames, int port, bool ssl = false, Func<HttpContext, Task> defaultRoute = null)
        {
            if (hostnames == null || hostnames.Count < 1) hostnames = new List<string> { "localhost" };
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));

            _Settings = new WatsonWebserverSettings(hostnames, port, ssl);
            _Routes.Default = defaultRoute;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use this object after disposal.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        public void Start(CancellationToken token = default)
        {
            if (_HttpListener != null && _HttpListener.IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");

            _HttpListener = new HttpListener();

            LoadRoutes();
            Statistics = new WatsonWebserverStatistics();

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;

            foreach (string prefix in _Settings.Prefixes)
            {
                _HttpListener.Prefixes.Add(prefix);
            }

            _HttpListener.Start();
            _AcceptConnections = Task.Run(() => AcceptConnections(_Token), _Token);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token = default)
        {
            if (_HttpListener != null && _HttpListener.IsListening) throw new InvalidOperationException("WatsonWebserver is already listening.");

            _HttpListener = new HttpListener();

            LoadRoutes();
            Statistics = new WatsonWebserverStatistics();

            _TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            _Token = token;

            foreach (string prefix in _Settings.Prefixes)
            {
                _HttpListener.Prefixes.Add(prefix);
            }

            _HttpListener.Start();
            _AcceptConnections = Task.Run(() => AcceptConnections(_Token), _Token);
            return _AcceptConnections;
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
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
                _Settings = null;
                _Routes = null;
                _TokenSource = null;
                _AcceptConnections = null;

                Events = null;
                Statistics = null;
            }
        }

        private void LoadRoutes()
        {
            var staticRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsStaticRoute); // Only select methods that are valid routes

            var parameterRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsParameterRoute); // Only select methods that are valid routes

            var dynamicRoutes = _Assembly
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsDynamicRoute); // Only select methods that are valid routes

            foreach (var staticRoute in staticRoutes)
            {
                var attribute = staticRoute.GetCustomAttributes().OfType<StaticRouteAttribute>().First();
                if (!_Routes.Static.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding static route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Static.Add(attribute.Method, attribute.Path, ToRouteMethod(staticRoute), attribute.GUID, attribute.Metadata);
                }
            }

            foreach (var parameterRoute in parameterRoutes)
            {
                var attribute = parameterRoute.GetCustomAttributes().OfType<ParameterRouteAttribute>().First();
                if (!_Routes.Parameter.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding parameter route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Parameter.Add(attribute.Method, attribute.Path, ToRouteMethod(parameterRoute), attribute.GUID, attribute.Metadata);
                }
            }

            foreach (var dynamicRoute in dynamicRoutes)
            {
                var attribute = dynamicRoute.GetCustomAttributes().OfType<DynamicRouteAttribute>().First();
                if (!_Routes.Dynamic.Exists(attribute.Method, attribute.Path))
                {
                    Events.Logger?.Invoke(_Header + "adding dynamic route " + attribute.Method.ToString() + " " + attribute.Path);
                    _Routes.Dynamic.Add(attribute.Method, attribute.Path, ToRouteMethod(dynamicRoute), attribute.GUID, attribute.Metadata);
                }
            }
        }

        private bool IsStaticRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<StaticRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private bool IsParameterRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<ParameterRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private bool IsDynamicRoute(MethodInfo method)
        {
            return method.GetCustomAttributes().OfType<DynamicRouteAttribute>().Any()
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
        }

        private Func<HttpContext, Task> ToRouteMethod(MethodInfo method)
        {
            if (method.IsStatic)
            {
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), method);
            }
            else
            {
                object instance = Activator.CreateInstance(method.DeclaringType ?? throw new Exception("Declaring class is null"));
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), instance, method);
            }
        }

        private async Task AcceptConnections(CancellationToken token)
        {
            try
            {
                #region Process-Requests

                while (_HttpListener.IsListening)
                {
                    if (_RequestCount >= _Settings.IO.MaxRequests)
                    {
                        await Task.Delay(100, token).ConfigureAwait(false);
                        continue;
                    }

                    HttpListenerContext listenerCtx = await _HttpListener.GetContextAsync().ConfigureAwait(false);
                    Interlocked.Increment(ref _RequestCount);
                    HttpContext ctx = null;

                    Task unawaited = Task.Run(async () =>
                    {
                        DateTime startTime = DateTime.Now;

                        try
                        {
                            #region Build-Context

                            Events.HandleConnectionReceived(this, new ConnectionEventArgs(
                                listenerCtx.Request.RemoteEndPoint.Address.ToString(),
                                listenerCtx.Request.RemoteEndPoint.Port));

                            ctx = new HttpContext(listenerCtx, _Settings, Events, _Serializer);

                            Events.HandleRequestReceived(this, new RequestEventArgs(ctx));

                            if (_Settings.Debug.Requests)
                            {
                                Events.Logger?.Invoke(
                                    _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            Statistics.IncrementRequestCounter(ctx.Request.Method);
                            Statistics.IncrementReceivedPayloadBytes(ctx.Request.ContentLength);

                            #endregion

                            #region Check-Access-Control

                            if (!_Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                            {
                                Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                                if (_Settings.Debug.AccessControl)
                                {
                                    Events.Logger?.Invoke(_Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                                }

                                listenerCtx.Response.StatusCode = 403;
                                listenerCtx.Response.Close();
                                return;
                            }

                            #endregion

                            #region Process-Preflight-Requests

                            if (ctx.Request.Method == HttpMethod.OPTIONS)
                            {
                                if (_Routes.Preflight != null)
                                {
                                    if (_Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(
                                            _Header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                    }

                                    await _Routes.Preflight(ctx).ConfigureAwait(false);
                                    return;
                                }
                            }

                            #endregion

                            #region Pre-Routing-Handler

                            bool terminate = false;
                            if (_Routes.PreRouting != null)
                            {
                                terminate = await _Routes.PreRouting(ctx).ConfigureAwait(false);
                                if (terminate)
                                {
                                    if (_Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(
                                            _Header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                    }

                                    return;
                                }
                            }

                            #endregion

                            #region Content-Routes

                            if (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD)
                            {
                                ContentRoute cr = null;
                                if (_Routes.Content.Match(ctx.Request.Url.RawWithoutQuery, out cr))
                                {
                                    if (_Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(
                                            _Header + "content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                    }

                                    ctx.RouteType = RouteTypeEnum.Content;
                                    ctx.Route = cr;
                                    await _Routes.ContentHandler.Process(ctx, token).ConfigureAwait(false);
                                    return;
                                }
                            }

                            #endregion

                            #region Static-Routes

                            StaticRoute sr = null;
                            Func<HttpContext, Task> handler = _Routes.Static.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out sr);
                            if (handler != null)
                            {
                                if (_Settings.Debug.Routing)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + "static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                }

                                ctx.RouteType = RouteTypeEnum.Static;
                                ctx.Route = sr;
                                await handler(ctx).ConfigureAwait(false);
                                return;
                            }

                            #endregion

                            #region Parameter-Routes

                            ParameterRoute pr = null;
                            NameValueCollection parameters = null;
                            handler = _Routes.Parameter.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out parameters, out pr);
                            if (handler != null)
                            {
                                ctx.Request.Url.Parameters = parameters;

                                if (_Settings.Debug.Routing)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + "parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                }

                                ctx.RouteType = RouteTypeEnum.Parameter;
                                ctx.Route = pr;
                                await handler(ctx).ConfigureAwait(false);
                                return;
                            }

                            #endregion

                            #region Dynamic-Routes

                            DynamicRoute dr = null;
                            handler = _Routes.Dynamic.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out dr);
                            if (handler != null)
                            {
                                if (_Settings.Debug.Routing)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + "dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                }

                                ctx.RouteType = RouteTypeEnum.Dynamic;
                                ctx.Route = dr;
                                await handler(ctx).ConfigureAwait(false);
                                return;
                            }

                            #endregion

                            #region Default-Route

                            if (_Routes.Default != null)
                            {
                                if (_Settings.Debug.Routing)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                }

                                ctx.RouteType = RouteTypeEnum.Default;
                                await _Routes.Default(ctx).ConfigureAwait(false);
                                return;
                            }
                            else
                            {
                                if (_Settings.Debug.Routing)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + "default route not found for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                }

                                ctx.Response.StatusCode = 404;
                                ctx.Response.ContentType = Pages.Default404Page.ContentType;
                                await ctx.Response.Send(Pages.Default404Page.Content).ConfigureAwait(false);
                                return;
                            }

                            #endregion
                        }
                        catch (Exception eInner)
                        {
                            ctx.Response.StatusCode = 500;
                            ctx.Response.ContentType = Pages.Default500Page.ContentType;
                            await ctx.Response.Send(Pages.Default500Page.Content).ConfigureAwait(false);
                            Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, eInner));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _RequestCount);

                            if (ctx != null && ctx.Response != null && ctx.Response.ResponseSent)
                            {
                                Events.HandleResponseSent(this, new ResponseEventArgs(ctx, TotalMsFrom(startTime)));
                                Statistics.IncrementSentPayloadBytes(ctx.Response.ContentLength);
                            }
                        }

                    }, token);
                }

                #endregion
            }
            catch (TaskCanceledException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (HttpListenerException)
            {
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

        private double TotalMsFrom(DateTime startTime)
        {
            try
            {
                DateTime endTime = DateTime.Now;
                TimeSpan totalTime = (endTime - startTime);
                return totalTime.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        #endregion
    }
}
