namespace WatsonWebserver.Core
{
    using WatsonWebserver.Core.Routing;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Webserver base.
    /// </summary>
    public abstract class WebserverBase
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public abstract bool IsListening { get; }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public abstract int RequestCount { get; }

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                if (value == null) _Settings = new WebserverSettings();
                else _Settings = value;
            }
        }

        /// <summary>
        /// Webserver routes.
        /// </summary>
        public WebserverRoutes Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Routes));
                _Routes = value;
            }
        }

        /// <summary>
        /// Webserver statistics.
        /// </summary>
        public WebserverStatistics Statistics
        {
            get
            {
                return _Statistics;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Statistics));
                _Statistics = value;
            }
        }

        /// <summary>
        /// Set specific actions/callbacks to use when events are raised.
        /// </summary>
        public WebserverEvents Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Events));
                _Events = value;
            }
        }

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WebserverPages DefaultPages
        {
            get
            {
                return _DefaultPages;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(DefaultPages));
                _DefaultPages = value;
            }
        }

        /// <summary>
        /// JSON serialization helper.
        /// </summary>
        [JsonIgnore]
        public ISerializationHelper Serializer
        {
            get
            {
                return _Serializer;
            }
            set
            {
                _Serializer = value ?? throw new ArgumentNullException(nameof(Serializer));
            }
        }

        #endregion

        #region Private-Members

        private WebserverEvents _Events = new WebserverEvents();
        private WebserverPages _DefaultPages = new WebserverPages();
        private WebserverSettings _Settings = new WebserverSettings();
        private WebserverStatistics _Statistics = new WebserverStatistics();
        private WebserverRoutes _Routes = new WebserverRoutes();
        private ISerializationHelper _Serializer = new DefaultSerializationHelper();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Creates a new instance of the webserver.
        /// If you do not provide a settings object, default settings will be used, which will cause the webserver to listen on http://127.0.0.1:8000, and send events to the console.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public WebserverBase(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute)
        {
            if (settings == null) settings = new WebserverSettings();

            _Settings = settings;
            _Routes = new WebserverRoutes(_Settings, defaultRoute);
        }

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public WebserverBase(string hostname, int port, bool ssl, Func<HttpContextBase, Task> defaultRoute)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));

            _Settings = new WebserverSettings(hostname, port, ssl);
            _Routes = new WebserverRoutes(_Settings, defaultRoute);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        public abstract void Start(CancellationToken token = default);

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        /// <returns>Task.</returns>
        public abstract Task StartAsync(CancellationToken token = default);

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public abstract void Stop();

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Indicates whether the current transport supports HTTP/2.
        /// </summary>
        protected virtual bool SupportsHttp2
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates whether the current transport supports HTTP/3.
        /// </summary>
        protected virtual bool SupportsHttp3
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Validate the current settings against the transport capability matrix.
        /// </summary>
        protected void ValidateSettings()
        {
            WebserverSettingsValidator.Validate(Settings, SupportsHttp2, SupportsHttp3);
        }

        /// <summary>
        /// Execute the shared request pipeline.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="header">Log prefix.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        protected async Task ProcessHttpContextAsync(HttpContextBase ctx, string header, CancellationToken token = default)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (String.IsNullOrEmpty(header)) throw new ArgumentNullException(nameof(header));

            string requestPath = ctx.Request.Url.RawWithoutQuery;
            string normalizedRequestPath = ctx.Request.Url.NormalizedRawWithoutQuery;

            if (Events.HasRequestReceivedHandlers)
            {
                Events.HandleRequestReceived(this, new RequestEventArgs(ctx));
            }

            if (Settings.Debug.Requests)
            {
                    Events.Logger?.Invoke(
                        header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + requestPath);
                }

            Statistics.IncrementActiveConnectionCount(ctx.Protocol);
            Statistics.IncrementRequestCounter(ctx.Request.Method);
            Statistics.IncrementReceivedPayloadBytes(ctx.Request.ContentLength);

            try
            {
                if (!Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                {
                    if (Events.HasRequestDeniedHandlers)
                    {
                        Events.HandleRequestDenied(this, new RequestEventArgs(ctx));
                    }

                    if (Settings.Debug.AccessControl)
                    {
                        Events.Logger?.Invoke(header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                    }

                    ctx.Response.StatusCode = 403;
                    await SendDefaultResponseAsync(ctx, token).ConfigureAwait(false);
                    return;
                }

                if (ctx.Request.Method == HttpMethod.OPTIONS && Routes.Preflight != null)
                {
                    if (Settings.Debug.Routing)
                    {
                        Events.Logger?.Invoke(
                            header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + requestPath);
                    }

                    await Routes.Preflight(ctx).ConfigureAwait(false);
                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException("Preflight route for " + ctx.Request.Method.ToString() + " " + requestPath + " did not send a response to the HTTP request.");
                    return;
                }

                if (Routes.PreRouting != null)
                {
                    await Routes.PreRouting(ctx).ConfigureAwait(false);
                    if (ctx.Response.ResponseSent)
                    {
                        if (Settings.Debug.Routing)
                        {
                            Events.Logger?.Invoke(
                                header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + requestPath);
                        }

                        return;
                    }
                }

                if (Routes.PreAuthentication != null)
                {
                    if (await ProcessRoutingGroupAsync(ctx, Routes.PreAuthentication, "pre-auth", header).ConfigureAwait(false))
                        return;
                }

                if (Routes.AuthenticateRequest != null)
                {
                    await Routes.AuthenticateRequest(ctx).ConfigureAwait(false);
                    if (ctx.Response.ResponseSent)
                    {
                        if (Settings.Debug.Routing)
                        {
                            Events.Logger?.Invoke(
                                header + "response sent during authentication for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        return;
                    }
                }

                if (Routes.PostAuthentication != null)
                {
                    if (await ProcessRoutingGroupAsync(ctx, Routes.PostAuthentication, "post-auth", header).ConfigureAwait(false))
                        return;
                }

                if (Settings.Debug.Routing)
                {
                    Events.Logger?.Invoke(
                        header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
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

                ctx.Response.StatusCode = 404;
                ctx.Response.ContentType = DefaultPages.Pages[404].ContentType;
                await SendDefaultResponseAsync(ctx, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                ctx.RequestAborted = true;
                if (Events.HasRequestAbortedHandlers)
                {
                    Events.HandleRequestAborted(this, new RequestEventArgs(ctx));
                }
                throw;
            }
            catch (MalformedHttpRequestException e)
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = DefaultPages.Pages[400].ContentType;
                await SendDefaultResponseAsync(ctx, token).ConfigureAwait(false);

                if (Events.HasExceptionEncounteredHandlers)
                {
                    Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, e));
                }
            }
            catch (Exception e)
            {
                if (Routes.Exception != null)
                {
                    await Routes.Exception(ctx, e).ConfigureAwait(false);
                }
                else
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                    await SendDefaultResponseAsync(ctx, token).ConfigureAwait(false);
                }

                if (Events.HasExceptionEncounteredHandlers)
                {
                    Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, e));
                }
            }
            finally
            {
                try
                {
                    if (!ctx.Response.ResponseSent)
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                        await SendDefaultResponseAsync(ctx, token).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                }

                ctx.Timestamp.End = DateTime.UtcNow;

                bool emitResponseStarting = ctx.Response.ResponseStarted && Events.HasResponseStartingHandlers;
                bool emitResponseSent = Events.HasResponseSentHandlers;
                bool emitResponseCompleted = ctx.Response.ResponseCompleted && Events.HasResponseCompletedHandlers;

                if (emitResponseStarting || emitResponseSent || emitResponseCompleted)
                {
                    ResponseEventArgs responseArgs = new ResponseEventArgs(ctx, ctx.Timestamp.TotalMs.Value);
                    if (emitResponseStarting) Events.HandleResponseStarting(this, responseArgs);
                    if (emitResponseSent) Events.HandleResponseSent(this, responseArgs);
                    if (emitResponseCompleted) Events.HandleResponseCompleted(this, responseArgs);
                }

                if (Settings.Debug.Responses)
                {
                    Events.Logger?.Invoke(
                        header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full + ": " +
                        ctx.Response.StatusCode + " [" + ctx.Timestamp.TotalMs.Value + "ms]");
                }

                if (ctx.Response.ContentLength > 0) Statistics.IncrementSentPayloadBytes(Convert.ToInt64(ctx.Response.ContentLength));

                if (Routes.PostRouting != null)
                {
                    try
                    {
                        await Routes.PostRouting(ctx).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                }

                Statistics.DecrementActiveConnectionCount(ctx.Protocol);
            }
        }

        #endregion

        #region Private-Members

        private async Task SendDefaultResponseAsync(HttpContextBase ctx, CancellationToken token)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            string content = DefaultPages.Pages[ctx.Response.StatusCode].Content;

            if (ctx.Response.ChunkedTransfer)
            {
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes(content), true, token).ConfigureAwait(false);
            }
            else
            {
                await ctx.Response.Send(content, token).ConfigureAwait(false);
            }
        }

        private async Task<bool> ProcessRoutingGroupAsync(HttpContextBase ctx, RoutingGroup group, string authPhase, string header)
        {
            Func<HttpContextBase, Task> handler = null;
            string requestPath = ctx.Request.Url.RawWithoutQuery;
            string normalizedRequestPath = ctx.Request.Url.NormalizedRawWithoutQuery;

            if (group.Static != null)
            {
                handler = group.Static.MatchNormalized(ctx.Request.Method, normalizedRequestPath, out StaticRoute staticRoute);
                if (handler != null)
                {
                    if (Settings.Debug.Routing)
                    {
                        Events.Logger?.Invoke(
                            header + authPhase + " static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + requestPath);
                    }

                    ctx.RouteType = RouteTypeEnum.Static;
                    ctx.Route = staticRoute;

                    try
                    {
                        await handler(ctx).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (staticRoute.ExceptionHandler != null) await staticRoute.ExceptionHandler(ctx, e).ConfigureAwait(false);
                        else throw;
                    }

                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException(authPhase + " static route for " + ctx.Request.Method.ToString() + " " + requestPath + " did not send a response to the HTTP request.");
                    return true;
                }
            }

            if (group.Content != null && (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD))
            {
                if (group.Content.Match(requestPath, out ContentRoute contentRoute))
                {
                    if (Settings.Debug.Routing)
                    {
                        Events.Logger?.Invoke(
                            header + authPhase + " content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + requestPath);
                    }

                    ctx.RouteType = RouteTypeEnum.Content;
                    ctx.Route = contentRoute;

                    try
                    {
                        await group.Content.Handler(ctx).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (contentRoute.ExceptionHandler != null) await contentRoute.ExceptionHandler(ctx, e).ConfigureAwait(false);
                        else throw;
                    }

                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException(authPhase + " content route for " + ctx.Request.Method.ToString() + " " + requestPath + " did not send a response to the HTTP request.");
                    return true;
                }
            }

            if (group.Parameter != null)
            {
                handler = group.Parameter.Match(ctx.Request.Method, requestPath, out NameValueCollection parameters, out ParameterRoute parameterRoute);
                if (handler != null)
                {
                    ctx.Request.Url.Parameters = parameters;

                    if (Settings.Debug.Routing)
                    {
                        Events.Logger?.Invoke(
                            header + authPhase + " parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + requestPath);
                    }

                    ctx.RouteType = RouteTypeEnum.Parameter;
                    ctx.Route = parameterRoute;

                    try
                    {
                        await handler(ctx).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (parameterRoute.ExceptionHandler != null) await parameterRoute.ExceptionHandler(ctx, e).ConfigureAwait(false);
                        else throw;
                    }

                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException(authPhase + " parameter route for " + ctx.Request.Method.ToString() + " " + requestPath + " did not send a response to the HTTP request.");
                    return true;
                }
            }

            if (group.Dynamic != null)
            {
                handler = group.Dynamic.Match(ctx.Request.Method, requestPath, out DynamicRoute dynamicRoute);
                if (handler != null)
                {
                    if (Settings.Debug.Routing)
                    {
                        Events.Logger?.Invoke(
                            header + authPhase + " dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + requestPath);
                    }

                    ctx.RouteType = RouteTypeEnum.Dynamic;
                    ctx.Route = dynamicRoute;

                    try
                    {
                        await handler(ctx).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        if (dynamicRoute.ExceptionHandler != null) await dynamicRoute.ExceptionHandler(ctx, e).ConfigureAwait(false);
                        else throw;
                    }

                    if (!ctx.Response.ResponseSent)
                        throw new InvalidOperationException(authPhase + " dynamic route for " + ctx.Request.Method.ToString() + " " + requestPath + " did not send a response to the HTTP request.");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Robustly retrieves and sanitizes a valid hostname for the local machine
        /// by trying several strategies in order of preference.
        /// This method is safe to use on all platforms, including iOS and Android.
        /// </summary>
        /// <returns>An RFC-compliant valid hostname, with a final fallback to "localhost".</returns>
        protected static string GetBestLocalHostName()
        {
            string sanitizedHostName;

            // Attempt 1: Dns.GetHostName() - The best choice for network identity.
            try
            {
                sanitizedHostName = SanitizeHostName(Dns.GetHostName());
                if (!string.IsNullOrEmpty(sanitizedHostName))
                {
                    return sanitizedHostName;
                }
            }
            catch { /* Ignore and move to fallback */ }

            // Attempt 2: Environment.MachineName - A solid backup that returns the device name.
            try
            {
                sanitizedHostName = SanitizeHostName(Environment.MachineName);
                if (!string.IsNullOrEmpty(sanitizedHostName))
                {
                    return sanitizedHostName;
                }
            }
            catch { /* Ignore and move to fallback */ }

            return "localhost";
        }

        /// <summary>
        /// Converts a string into a valid RFC 1123 hostname.
        /// It removes invalid characters, converts to lowercase, and handles hyphens.
        /// </summary>
        /// <param name="potentialName">The name to sanitize.</param>
        /// <returns>A valid hostname, or null if the string contains no valid characters.</returns>
        protected static string SanitizeHostName(string potentialName)
        {
            if (string.IsNullOrWhiteSpace(potentialName))
            {
                return null;
            }

            // 1. Convert to lowercase for consistency.
            string sanitized = potentialName.ToLowerInvariant();

            // 2. Replace spaces and other common separators with a hyphen.
            sanitized = Regex.Replace(sanitized, @"[\s_.]+", "-");

            // 3. Remove all characters that are not letters, numbers, or hyphens.
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9-]", "");

            // 4. Remove any leading or trailing hyphens.
            sanitized = sanitized.Trim('-');

            // 5. If nothing is left after cleaning, the name is invalid.
            if (string.IsNullOrEmpty(sanitized))
            {
                return null;
            }

            return sanitized;
        }
        #endregion
    }
}
