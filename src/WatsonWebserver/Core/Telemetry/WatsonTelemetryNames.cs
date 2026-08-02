namespace WatsonWebserver.Core.Telemetry
{
    /// <summary>
    /// Stable telemetry contract strings emitted by Watson.
    /// These values are treated as public API: a telemetry consumer hard-codes them when subscribing
    /// to the meter and activity source or when querying the exported series. They are stable across
    /// patch and minor releases and change only with a major version bump.
    /// </summary>
    public static class WatsonTelemetryNames
    {
        #region Source-Names

        /// <summary>
        /// Default meter name. This is the string a host passes to a metrics subscriber (for example
        /// Radiant's <c>Sources.AddMeter</c> or the OpenTelemetry SDK's <c>AddMeter</c>).
        /// Overridable via <c>Settings.Telemetry.MeterName</c>.
        /// </summary>
        public const string MeterName = "Watson";

        /// <summary>
        /// Default activity source name. This is the string a host passes to a trace subscriber (for
        /// example Radiant's <c>Sources.AddActivitySource</c> or the OpenTelemetry SDK's <c>AddSource</c>).
        /// Overridable via <c>Settings.Telemetry.ActivitySourceName</c>.
        /// </summary>
        public const string ActivitySourceName = "Watson";

        #endregion

        #region Metric-Instrument-Names

        /// <summary>
        /// Histogram of server request duration in seconds. OpenTelemetry HTTP semantic convention.
        /// </summary>
        public const string HttpServerRequestDuration = "http.server.request.duration";

        /// <summary>
        /// Up-down counter of in-flight server requests. OpenTelemetry HTTP semantic convention.
        /// </summary>
        public const string HttpServerActiveRequests = "http.server.active_requests";

        /// <summary>
        /// Histogram of request body size in bytes. OpenTelemetry HTTP semantic convention.
        /// </summary>
        public const string HttpServerRequestBodySize = "http.server.request.body.size";

        /// <summary>
        /// Histogram of response body size in bytes. OpenTelemetry HTTP semantic convention.
        /// </summary>
        public const string HttpServerResponseBodySize = "http.server.response.body.size";

        /// <summary>
        /// Observable gauge of active connections, broken out by protocol version.
        /// </summary>
        public const string ServerConnectionsActive = "watson.server.connections.active";

        /// <summary>
        /// Counter of accepted connections, broken out by protocol version.
        /// </summary>
        public const string ServerConnectionsTotal = "watson.server.connections.total";

        /// <summary>
        /// Counter of connections denied by access control.
        /// </summary>
        public const string ServerConnectionsDenied = "watson.server.connections.denied";

        /// <summary>
        /// Counter of requests aborted before completion.
        /// </summary>
        public const string ServerRequestsAborted = "watson.server.requests.aborted";

        /// <summary>
        /// Counter of requests terminated by an unexpected requestor disconnect.
        /// </summary>
        public const string ServerRequestsDisconnected = "watson.server.requests.disconnected";

        /// <summary>
        /// Counter of server-level exceptions, broken out by exception type and protocol version.
        /// </summary>
        public const string ServerExceptions = "watson.server.exceptions";

        /// <summary>
        /// Observable counter of received payload bytes.
        /// </summary>
        public const string ServerReceivedBytes = "watson.server.received.bytes";

        /// <summary>
        /// Observable counter of sent payload bytes.
        /// </summary>
        public const string ServerSentBytes = "watson.server.sent.bytes";

        /// <summary>
        /// Observable gauge of seconds the server has been running.
        /// </summary>
        public const string ServerUptime = "watson.server.uptime";

        /// <summary>
        /// Observable gauge that reports 1 while the server is listening and 0 otherwise.
        /// </summary>
        public const string ServerUp = "watson.server.up";

        /// <summary>
        /// Observable gauge of active HTTP/2 streams.
        /// </summary>
        public const string Http2StreamsActive = "watson.http2.streams.active";

        /// <summary>
        /// Observable gauge of active HTTP/3 streams.
        /// </summary>
        public const string Http3StreamsActive = "watson.http3.streams.active";

        /// <summary>
        /// Counter of route matches, broken out by route type and route template.
        /// </summary>
        public const string RouteMatches = "watson.route.matches";

        /// <summary>
        /// Counter of requests that matched no route.
        /// </summary>
        public const string RouteUnmatched = "watson.route.unmatched";

        /// <summary>
        /// Counter of authentication decisions, broken out by mode and result.
        /// </summary>
        public const string AuthRequests = "watson.auth.requests";

        /// <summary>
        /// Up-down counter of active WebSocket sessions.
        /// </summary>
        public const string WebSocketSessionsActive = "watson.websocket.sessions.active";

        /// <summary>
        /// Counter of WebSocket sessions started.
        /// </summary>
        public const string WebSocketSessionsTotal = "watson.websocket.sessions.total";

        /// <summary>
        /// Counter of failed WebSocket handshakes.
        /// </summary>
        public const string WebSocketHandshakeFailures = "watson.websocket.handshake.failures";

        #endregion

        #region Attribute-Keys

        /// <summary>
        /// HTTP request method attribute key (for example GET, POST).
        /// </summary>
        public const string AttributeHttpRequestMethod = "http.request.method";

        /// <summary>
        /// HTTP response status code attribute key.
        /// </summary>
        public const string AttributeHttpResponseStatusCode = "http.response.status_code";

        /// <summary>
        /// HTTP route template attribute key. Always the bounded route template, never the raw path.
        /// </summary>
        public const string AttributeHttpRoute = "http.route";

        /// <summary>
        /// URL scheme attribute key (http or https).
        /// </summary>
        public const string AttributeUrlScheme = "url.scheme";

        /// <summary>
        /// URL path attribute key (span only; unbounded).
        /// </summary>
        public const string AttributeUrlPath = "url.path";

        /// <summary>
        /// Network protocol version attribute key (1.1, 2, or 3).
        /// </summary>
        public const string AttributeNetworkProtocolVersion = "network.protocol.version";

        /// <summary>
        /// Network protocol name attribute key (http).
        /// </summary>
        public const string AttributeNetworkProtocolName = "network.protocol.name";

        /// <summary>
        /// Resolved client address attribute key (span only; unbounded).
        /// </summary>
        public const string AttributeClientAddress = "client.address";

        /// <summary>
        /// Resolved client port attribute key (span only).
        /// </summary>
        public const string AttributeClientPort = "client.port";

        /// <summary>
        /// Raw socket peer address attribute key (span only; unbounded).
        /// </summary>
        public const string AttributeNetworkPeerAddress = "network.peer.address";

        /// <summary>
        /// Raw socket peer port attribute key (span only).
        /// </summary>
        public const string AttributeNetworkPeerPort = "network.peer.port";

        /// <summary>
        /// User agent attribute key (span only; unbounded).
        /// </summary>
        public const string AttributeUserAgentOriginal = "user_agent.original";

        /// <summary>
        /// Request body size attribute key.
        /// </summary>
        public const string AttributeHttpRequestBodySize = "http.request.body.size";

        /// <summary>
        /// Response body size attribute key.
        /// </summary>
        public const string AttributeHttpResponseBodySize = "http.response.body.size";

        /// <summary>
        /// Request content type attribute key (normalized media type).
        /// </summary>
        public const string AttributeHttpRequestContentType = "http.request.header.content-type";

        /// <summary>
        /// Response content type attribute key (normalized media type).
        /// </summary>
        public const string AttributeHttpResponseContentType = "http.response.header.content-type";

        /// <summary>
        /// Error type attribute key (exception type name).
        /// </summary>
        public const string AttributeErrorType = "error.type";

        /// <summary>
        /// Route type attribute key (Static, Content, Parameter, Dynamic).
        /// </summary>
        public const string AttributeRouteType = "watson.route.type";

        /// <summary>
        /// Direction attribute key for bidirectional counters.
        /// </summary>
        public const string AttributeDirection = "watson.direction";

        /// <summary>
        /// Authentication mode attribute key (api or legacy).
        /// </summary>
        public const string AttributeAuthMode = "watson.auth.mode";

        /// <summary>
        /// Authentication result attribute key.
        /// </summary>
        public const string AttributeAuthAuthn = "watson.auth.authn";

        /// <summary>
        /// Authorization result attribute key.
        /// </summary>
        public const string AttributeAuthAuthz = "watson.auth.authz";

        /// <summary>
        /// Reason attribute key for denied/rejected counters.
        /// </summary>
        public const string AttributeReason = "watson.reason";

        #endregion
    }
}
