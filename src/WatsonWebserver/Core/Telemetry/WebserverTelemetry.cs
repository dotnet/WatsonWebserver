namespace WatsonWebserver.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Reflection;
    using WatsonWebserver.Core.Routing;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Owns the Watson <see cref="Meter"/> and <see cref="ActivitySource"/> and emits standardized
    /// metrics and traces. Metrics that mirror the existing statistics and events surface are wired to
    /// that surface directly, so the transport hot paths are untouched. The per-request duration,
    /// active-request, and body-size instruments plus the server span are recorded from the shared
    /// request pipeline.
    /// </summary>
    /// <remarks>
    /// Emission is a near-zero-cost no-op when nothing is subscribed. This object is owned by the
    /// server and disposed with it. Configure <see cref="TelemetrySettings"/> before constructing the
    /// server.
    /// </remarks>
    public sealed class WebserverTelemetry : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether telemetry is enabled at the master level.
        /// </summary>
        public bool Enabled
        {
            get
            {
                return _Settings.Enable;
            }
        }

        /// <summary>
        /// Indicates whether the in-process Prometheus scrape endpoint is enabled.
        /// </summary>
        public bool PrometheusEnabled
        {
            get
            {
                return _Settings.Enable && _Settings.Prometheus.Enable && _Collector != null;
            }
        }

        /// <summary>
        /// Path at which the Prometheus scrape endpoint is served.
        /// </summary>
        public string PrometheusPath
        {
            get
            {
                return _Settings.Prometheus.Path;
            }
        }

        #endregion

        #region Private-Members

        private readonly TelemetrySettings _Settings;
        private readonly Func<WebserverStatistics> _StatisticsAccessor;
        private readonly WebserverEvents _Events;

        private readonly Meter _Meter;
        private readonly ActivitySource _ActivitySource;
        private readonly PrometheusScrapeCollector _Collector;

        private readonly Histogram<double> _RequestDuration;
        private readonly UpDownCounter<long> _ActiveRequests;
        private readonly Histogram<double> _RequestBodySize;
        private readonly Histogram<double> _ResponseBodySize;
        private readonly Counter<long> _ConnectionsTotal;
        private readonly Counter<long> _ConnectionsDenied;
        private readonly Counter<long> _RequestsAborted;
        private readonly Counter<long> _RequestsDisconnected;
        private readonly Counter<long> _ServerExceptions;
        private readonly Counter<long> _RouteMatches;
        private readonly Counter<long> _RouteUnmatched;
        private readonly Counter<long> _AuthRequests;
        private readonly UpDownCounter<long> _WsSessionsActive;
        private readonly Counter<long> _WsSessionsTotal;
        private readonly Counter<long> _WsHandshakeFailures;

        private long _Up = 0;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the telemetry object.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="events">Server event hub to bridge into metrics.</param>
        /// <param name="statisticsAccessor">Accessor returning the current statistics object.</param>
        public WebserverTelemetry(TelemetrySettings settings, WebserverEvents events, Func<WebserverStatistics> statisticsAccessor)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (statisticsAccessor == null) throw new ArgumentNullException(nameof(statisticsAccessor));

            _Settings = settings;
            _Events = events;
            _StatisticsAccessor = statisticsAccessor;

            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

            _Meter = new Meter(settings.MeterName, version);
            _ActivitySource = new ActivitySource(settings.ActivitySourceName, version);

            _RequestDuration = _Meter.CreateHistogram<double>(WatsonTelemetryNames.HttpServerRequestDuration, "s", "Duration of HTTP server requests.");
            _ActiveRequests = _Meter.CreateUpDownCounter<long>(WatsonTelemetryNames.HttpServerActiveRequests, "{request}", "In-flight HTTP server requests.");
            _RequestBodySize = _Meter.CreateHistogram<double>(WatsonTelemetryNames.HttpServerRequestBodySize, "By", "Size of HTTP server request bodies.");
            _ResponseBodySize = _Meter.CreateHistogram<double>(WatsonTelemetryNames.HttpServerResponseBodySize, "By", "Size of HTTP server response bodies.");

            _ConnectionsTotal = _Meter.CreateCounter<long>(WatsonTelemetryNames.ServerConnectionsTotal, "{connection}", "Accepted connections.");
            _ConnectionsDenied = _Meter.CreateCounter<long>(WatsonTelemetryNames.ServerConnectionsDenied, "{connection}", "Connections denied by access control.");
            _RequestsAborted = _Meter.CreateCounter<long>(WatsonTelemetryNames.ServerRequestsAborted, "{request}", "Requests aborted before completion.");
            _RequestsDisconnected = _Meter.CreateCounter<long>(WatsonTelemetryNames.ServerRequestsDisconnected, "{request}", "Requests terminated by requestor disconnect.");
            _ServerExceptions = _Meter.CreateCounter<long>(WatsonTelemetryNames.ServerExceptions, "{exception}", "Server-level exceptions.");
            _RouteMatches = _Meter.CreateCounter<long>(WatsonTelemetryNames.RouteMatches, "{match}", "Route matches.");
            _RouteUnmatched = _Meter.CreateCounter<long>(WatsonTelemetryNames.RouteUnmatched, "{request}", "Requests that matched no route.");
            _AuthRequests = _Meter.CreateCounter<long>(WatsonTelemetryNames.AuthRequests, "{request}", "Authentication decisions.");
            _WsSessionsActive = _Meter.CreateUpDownCounter<long>(WatsonTelemetryNames.WebSocketSessionsActive, "{session}", "Active WebSocket sessions.");
            _WsSessionsTotal = _Meter.CreateCounter<long>(WatsonTelemetryNames.WebSocketSessionsTotal, "{session}", "WebSocket sessions started.");
            _WsHandshakeFailures = _Meter.CreateCounter<long>(WatsonTelemetryNames.WebSocketHandshakeFailures, "{handshake}", "Failed WebSocket handshakes.");

            _Meter.CreateObservableGauge<long>(WatsonTelemetryNames.ServerConnectionsActive, ObserveActiveConnections, "{connection}", "Active connections.");
            _Meter.CreateObservableGauge<long>(WatsonTelemetryNames.Http2StreamsActive, ObserveHttp2Streams, "{stream}", "Active HTTP/2 streams.");
            _Meter.CreateObservableGauge<long>(WatsonTelemetryNames.Http3StreamsActive, ObserveHttp3Streams, "{stream}", "Active HTTP/3 streams.");
            _Meter.CreateObservableCounter<long>(WatsonTelemetryNames.ServerReceivedBytes, ObserveReceivedBytes, "By", "Received payload bytes.");
            _Meter.CreateObservableCounter<long>(WatsonTelemetryNames.ServerSentBytes, ObserveSentBytes, "By", "Sent payload bytes.");
            _Meter.CreateObservableGauge<double>(WatsonTelemetryNames.ServerUptime, ObserveUptime, "s", "Seconds the server has been running.");
            _Meter.CreateObservableGauge<long>(WatsonTelemetryNames.ServerUp, ObserveUp, "1", "1 while the server is listening.");

            SubscribeEvents();

            if (_Settings.Prometheus.Enable)
            {
                _Collector = new PrometheusScrapeCollector(settings.MeterName);
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Increment the active-request counter for a starting request.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        public void OnRequestStarted(HttpContextBase ctx)
        {
            if (!MetricsOn() || ctx == null) return;

            TagList tags = new TagList
            {
                { WatsonTelemetryNames.AttributeHttpRequestMethod, ctx.Request.Method.ToString() },
                { WatsonTelemetryNames.AttributeUrlScheme, Scheme(ctx) }
            };
            _ActiveRequests.Add(1, tags);
        }

        /// <summary>
        /// Record the terminal request metrics and finalize the server span.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="span">Server span, or null.</param>
        /// <param name="durationMs">Elapsed request time in milliseconds.</param>
        public void OnRequestFinished(HttpContextBase ctx, Activity span, double durationMs)
        {
            if (ctx == null) return;

            string method = ctx.Request.Method.ToString();
            int statusCode = ctx.Response != null ? ctx.Response.StatusCode : 0;
            string routeTemplate = ResolveRouteTemplate(ctx);

            if (MetricsOn())
            {
                TagList activeTags = new TagList
                {
                    { WatsonTelemetryNames.AttributeHttpRequestMethod, method },
                    { WatsonTelemetryNames.AttributeUrlScheme, Scheme(ctx) }
                };
                _ActiveRequests.Add(-1, activeTags);

                TagList durationTags = new TagList
                {
                    { WatsonTelemetryNames.AttributeHttpRequestMethod, method },
                    { WatsonTelemetryNames.AttributeHttpResponseStatusCode, statusCode }
                };
                if (routeTemplate != null) durationTags.Add(WatsonTelemetryNames.AttributeHttpRoute, routeTemplate);
                _RequestDuration.Record(durationMs / 1000.0, durationTags);

                if (_Settings.CaptureRequestBodySize)
                {
                    TagList reqTags = new TagList { { WatsonTelemetryNames.AttributeHttpRequestMethod, method } };
                    _RequestBodySize.Record(ctx.Request.ContentLength, reqTags);
                }

                if (_Settings.CaptureResponseBodySize && ctx.Response != null)
                {
                    TagList respTags = new TagList
                    {
                        { WatsonTelemetryNames.AttributeHttpRequestMethod, method },
                        { WatsonTelemetryNames.AttributeHttpResponseStatusCode, statusCode }
                    };
                    _ResponseBodySize.Record(ctx.Response.ContentLength, respTags);
                }
            }

            FinalizeSpan(ctx, span, method, statusCode, routeTemplate);
        }

        /// <summary>
        /// Start a server span for a request, adopting an inbound trace context when configured.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>Activity, or null when tracing is disabled or nothing is listening.</returns>
        public Activity StartServerSpan(HttpContextBase ctx)
        {
            if (!TracesOn() || ctx == null) return null;
            if (!_ActivitySource.HasListeners()) return null;

            Activity activity;
            if (_Settings.PropagateContext && TryExtractParent(ctx, out ActivityContext parent))
            {
                activity = _ActivitySource.StartActivity(ctx.Request.Method.ToString(), ActivityKind.Server, parent);
            }
            else
            {
                activity = _ActivitySource.StartActivity(ctx.Request.Method.ToString(), ActivityKind.Server);
            }

            if (activity == null) return null;

            activity.SetTag(WatsonTelemetryNames.AttributeHttpRequestMethod, ctx.Request.Method.ToString());
            activity.SetTag(WatsonTelemetryNames.AttributeUrlScheme, Scheme(ctx));
            activity.SetTag(WatsonTelemetryNames.AttributeUrlPath, SafePath(ctx));
            activity.SetTag(WatsonTelemetryNames.AttributeNetworkProtocolName, "http");
            activity.SetTag(WatsonTelemetryNames.AttributeNetworkProtocolVersion, ProtocolVersion(ctx.Protocol));

            string peer = ctx.Request?.Source?.IpAddress;
            if (!String.IsNullOrEmpty(peer))
            {
                activity.SetTag(WatsonTelemetryNames.AttributeNetworkPeerAddress, peer);
                activity.SetTag(WatsonTelemetryNames.AttributeNetworkPeerPort, ctx.Request.Source.Port);
            }

            string client = ForwardedHeaderResolver.ResolveClientAddress(ctx, _Settings, out bool fromHeader);
            if (!String.IsNullOrEmpty(client))
            {
                activity.SetTag(WatsonTelemetryNames.AttributeClientAddress, client);
                if (!fromHeader && ctx.Request?.Source != null)
                {
                    activity.SetTag(WatsonTelemetryNames.AttributeClientPort, ctx.Request.Source.Port);
                }
            }

            if (!String.IsNullOrEmpty(ctx.Request.Useragent))
            {
                activity.SetTag(WatsonTelemetryNames.AttributeUserAgentOriginal, ctx.Request.Useragent);
            }

            if (_Settings.CaptureRequestBodySize)
            {
                activity.SetTag(WatsonTelemetryNames.AttributeHttpRequestBodySize, ctx.Request.ContentLength);
            }

            if (_Settings.CaptureContentType && !String.IsNullOrEmpty(ctx.Request.ContentType))
            {
                activity.SetTag(WatsonTelemetryNames.AttributeHttpRequestContentType, NormalizeMediaType(ctx.Request.ContentType));
            }

            return activity;
        }

        /// <summary>
        /// Record a route match.
        /// </summary>
        /// <param name="routeType">Matched route type.</param>
        /// <param name="route">Matched route object.</param>
        public void RecordRouteMatch(RouteTypeEnum routeType, object route)
        {
            if (!MetricsOn()) return;

            TagList tags = new TagList { { WatsonTelemetryNames.AttributeRouteType, routeType.ToString() } };
            string template = ResolveRouteTemplate(routeType, route);
            if (template != null) tags.Add(WatsonTelemetryNames.AttributeHttpRoute, template);
            _RouteMatches.Add(1, tags);
        }

        /// <summary>
        /// Record a request that matched no route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        public void RecordRouteUnmatched(HttpMethod method)
        {
            if (!MetricsOn()) return;
            TagList tags = new TagList { { WatsonTelemetryNames.AttributeHttpRequestMethod, method.ToString() } };
            _RouteUnmatched.Add(1, tags);
        }

        /// <summary>
        /// Record an authentication decision.
        /// </summary>
        /// <param name="mode">Authentication mode (api or legacy).</param>
        /// <param name="result">Authentication result, or null for a legacy denial.</param>
        public void RecordAuth(string mode, AuthResult result)
        {
            if (!MetricsOn()) return;

            TagList tags = new TagList { { WatsonTelemetryNames.AttributeAuthMode, mode } };
            if (result != null)
            {
                tags.Add(WatsonTelemetryNames.AttributeAuthAuthn, result.AuthenticationResult.ToString());
                tags.Add(WatsonTelemetryNames.AttributeAuthAuthz, result.AuthorizationResult.ToString());
            }
            _AuthRequests.Add(1, tags);
        }

        /// <summary>
        /// Record an exception onto the server span.
        /// </summary>
        /// <param name="span">Server span, or null.</param>
        /// <param name="e">Exception.</param>
        public void RecordSpanException(Activity span, Exception e)
        {
            if (span == null || e == null) return;

            span.SetStatus(ActivityStatusCode.Error, e.Message);
            span.SetTag(WatsonTelemetryNames.AttributeErrorType, e.GetType().FullName);

            if (_Settings.RecordExceptionEvents)
            {
                ActivityTagsCollection eventTags = new ActivityTagsCollection
                {
                    { "exception.type", e.GetType().FullName },
                    { "exception.message", e.Message },
                    { "exception.stacktrace", e.ToString() }
                };
                span.AddEvent(new ActivityEvent("exception", tags: eventTags));
            }
        }

        /// <summary>
        /// Render the current metrics in Prometheus exposition format.
        /// </summary>
        /// <returns>Prometheus exposition text, or an empty string when the collector is not active.</returns>
        public string RenderPrometheus()
        {
            if (_Collector == null) return "";
            return _Collector.Render();
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            UnsubscribeEvents();
            try { _Collector?.Dispose(); } catch (Exception) { }
            try { _Meter.Dispose(); } catch (Exception) { }
            try { _ActivitySource.Dispose(); } catch (Exception) { }
        }

        #endregion

        #region Private-Methods

        private bool MetricsOn()
        {
            return _Settings.Enable && _Settings.EnableMetrics;
        }

        private bool TracesOn()
        {
            return _Settings.Enable && _Settings.EnableTraces;
        }

        private void FinalizeSpan(HttpContextBase ctx, Activity span, string method, int statusCode, string routeTemplate)
        {
            if (span == null) return;

            if (routeTemplate != null)
            {
                span.SetTag(WatsonTelemetryNames.AttributeHttpRoute, routeTemplate);
                span.DisplayName = method + " " + routeTemplate;
            }

            span.SetTag(WatsonTelemetryNames.AttributeHttpResponseStatusCode, statusCode);

            if (_Settings.CaptureResponseBodySize && ctx.Response != null)
            {
                span.SetTag(WatsonTelemetryNames.AttributeHttpResponseBodySize, ctx.Response.ContentLength);
            }

            if (_Settings.CaptureContentType && ctx.Response != null && !String.IsNullOrEmpty(ctx.Response.ContentType))
            {
                span.SetTag(WatsonTelemetryNames.AttributeHttpResponseContentType, NormalizeMediaType(ctx.Response.ContentType));
            }

            if (span.Status == ActivityStatusCode.Unset)
            {
                if (statusCode >= 500) span.SetStatus(ActivityStatusCode.Error);
                else span.SetStatus(ActivityStatusCode.Ok);
            }
        }

        private bool TryExtractParent(HttpContextBase ctx, out ActivityContext parent)
        {
            parent = default;
            string traceparent = ctx.Request?.RetrieveHeaderValue("traceparent");
            if (String.IsNullOrEmpty(traceparent)) return false;

            string tracestate = ctx.Request?.RetrieveHeaderValue("tracestate");
            return ActivityContext.TryParse(traceparent, tracestate, out parent);
        }

        private void SubscribeEvents()
        {
            _Events.ConnectionReceived += OnConnectionReceived;
            _Events.ConnectionDenied += OnConnectionDenied;
            _Events.RequestAborted += OnRequestAborted;
            _Events.RequestorDisconnected += OnRequestorDisconnected;
            _Events.ExceptionEncountered += OnExceptionEncountered;
            _Events.ServerStarted += OnServerStarted;
            _Events.ServerStopped += OnServerStopped;

            if (_Settings.CaptureWebSocketMetrics)
            {
                _Events.WebSocketSessionStarted += OnWebSocketSessionStarted;
                _Events.WebSocketSessionEnded += OnWebSocketSessionEnded;
                _Events.WebSocketHandshakeFailed += OnWebSocketHandshakeFailed;
            }
        }

        private void UnsubscribeEvents()
        {
            _Events.ConnectionReceived -= OnConnectionReceived;
            _Events.ConnectionDenied -= OnConnectionDenied;
            _Events.RequestAborted -= OnRequestAborted;
            _Events.RequestorDisconnected -= OnRequestorDisconnected;
            _Events.ExceptionEncountered -= OnExceptionEncountered;
            _Events.ServerStarted -= OnServerStarted;
            _Events.ServerStopped -= OnServerStopped;

            if (_Settings.CaptureWebSocketMetrics)
            {
                _Events.WebSocketSessionStarted -= OnWebSocketSessionStarted;
                _Events.WebSocketSessionEnded -= OnWebSocketSessionEnded;
                _Events.WebSocketHandshakeFailed -= OnWebSocketHandshakeFailed;
            }
        }

        private void OnConnectionReceived(object sender, ConnectionEventArgs e)
        {
            if (!MetricsOn()) return;
            _ConnectionsTotal.Add(1, new KeyValuePair<string, object>(WatsonTelemetryNames.AttributeNetworkProtocolVersion, ProtocolVersion(e.Protocol)));
        }

        private void OnConnectionDenied(object sender, ConnectionEventArgs e)
        {
            if (!MetricsOn()) return;
            _ConnectionsDenied.Add(1, new KeyValuePair<string, object>(WatsonTelemetryNames.AttributeNetworkProtocolVersion, ProtocolVersion(e.Protocol)));
        }

        private void OnRequestAborted(object sender, RequestEventArgs e)
        {
            if (!MetricsOn()) return;
            _RequestsAborted.Add(1);
        }

        private void OnRequestorDisconnected(object sender, RequestEventArgs e)
        {
            if (!MetricsOn()) return;
            _RequestsDisconnected.Add(1);
        }

        private void OnExceptionEncountered(object sender, ExceptionEventArgs e)
        {
            if (!MetricsOn()) return;
            string errorType = e.Exception != null ? e.Exception.GetType().FullName : "unknown";
            TagList tags = new TagList
            {
                { WatsonTelemetryNames.AttributeErrorType, errorType },
                { WatsonTelemetryNames.AttributeNetworkProtocolVersion, ProtocolVersion(e.Protocol) }
            };
            _ServerExceptions.Add(1, tags);
        }

        private void OnServerStarted(object sender, EventArgs e)
        {
            System.Threading.Interlocked.Exchange(ref _Up, 1);
        }

        private void OnServerStopped(object sender, EventArgs e)
        {
            System.Threading.Interlocked.Exchange(ref _Up, 0);
        }

        private void OnWebSocketSessionStarted(object sender, WebSocketSessionEventArgs e)
        {
            if (!MetricsOn()) return;
            _WsSessionsActive.Add(1);
            _WsSessionsTotal.Add(1);
        }

        private void OnWebSocketSessionEnded(object sender, WebSocketSessionEventArgs e)
        {
            if (!MetricsOn()) return;
            _WsSessionsActive.Add(-1);
        }

        private void OnWebSocketHandshakeFailed(object sender, WebSocketHandshakeFailureEventArgs e)
        {
            if (!MetricsOn()) return;
            _WsHandshakeFailures.Add(1);
        }

        private IEnumerable<Measurement<long>> ObserveActiveConnections()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            yield return new Measurement<long>(stats != null ? stats.ActiveConnectionCount : 0);
        }

        private IEnumerable<Measurement<long>> ObserveHttp2Streams()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            yield return new Measurement<long>(stats != null ? stats.ActiveHttp2StreamCount : 0);
        }

        private IEnumerable<Measurement<long>> ObserveHttp3Streams()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            yield return new Measurement<long>(stats != null ? stats.ActiveHttp3StreamCount : 0);
        }

        private IEnumerable<Measurement<long>> ObserveReceivedBytes()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            yield return new Measurement<long>(stats != null ? stats.ReceivedPayloadBytes : 0);
        }

        private IEnumerable<Measurement<long>> ObserveSentBytes()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            yield return new Measurement<long>(stats != null ? stats.SentPayloadBytes : 0);
        }

        private IEnumerable<Measurement<double>> ObserveUptime()
        {
            WebserverStatistics stats = _StatisticsAccessor();
            double seconds = stats != null ? stats.UpTime.TotalSeconds : 0;
            yield return new Measurement<double>(seconds);
        }

        private IEnumerable<Measurement<long>> ObserveUp()
        {
            yield return new Measurement<long>(System.Threading.Interlocked.Read(ref _Up));
        }

        private string Scheme(HttpContextBase ctx)
        {
            string defaultScheme = "http";
            return ForwardedHeaderResolver.ResolveScheme(ctx, _Settings, defaultScheme);
        }

        private static string SafePath(HttpContextBase ctx)
        {
            try
            {
                return ctx.Request?.Url?.RawWithoutQuery;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ProtocolVersion(HttpProtocol protocol)
        {
            switch (protocol)
            {
                case HttpProtocol.Http1: return "1.1";
                case HttpProtocol.Http2: return "2";
                case HttpProtocol.Http3: return "3";
                default: return "1.1";
            }
        }

        private static string NormalizeMediaType(string contentType)
        {
            if (String.IsNullOrEmpty(contentType)) return contentType;
            int semicolon = contentType.IndexOf(';');
            string mediaType = semicolon >= 0 ? contentType.Substring(0, semicolon) : contentType;
            return mediaType.Trim().ToLowerInvariant();
        }

        private static string ResolveRouteTemplate(HttpContextBase ctx)
        {
            if (ctx == null) return null;
            return ResolveRouteTemplate(ctx.RouteType, ctx.Route);
        }

        private static string ResolveRouteTemplate(RouteTypeEnum routeType, object route)
        {
            switch (routeType)
            {
                case RouteTypeEnum.Static:
                    return (route as StaticRoute)?.Path;
                case RouteTypeEnum.Content:
                    return (route as ContentRoute)?.Path;
                case RouteTypeEnum.Parameter:
                    return (route as ParameterRoute)?.Path;
                case RouteTypeEnum.Dynamic:
                    return (route as DynamicRoute)?.Path?.ToString();
                default:
                    return null;
            }
        }

        #endregion
    }
}
