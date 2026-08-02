namespace WatsonWebserver.Core.Settings
{
    using System;
    using IpMatcher;
    using WatsonWebserver.Core.Telemetry;

    /// <summary>
    /// Telemetry and instrumentation settings.
    /// Watson emits metrics through a <see cref="System.Diagnostics.Metrics.Meter"/> and traces through
    /// a <see cref="System.Diagnostics.ActivitySource"/>, both using OpenTelemetry-shaped names. A host
    /// subscribes to the meter and activity source by name; Watson itself takes no dependency on any
    /// collector. Emission is nearly free when nothing is listening.
    /// </summary>
    /// <remarks>
    /// These settings are read when the server is constructed. Configure them before constructing the
    /// server rather than mutating them while requests are in flight.
    /// </remarks>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for telemetry. Default is true.
        /// When false, Watson skips all metric and trace emission before performing any tag work.
        /// Even when true, emission is a near-zero-cost no-op if no listener is subscribed.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Meter name used for metric emission. Default is "Watson".
        /// This is the string a metrics host subscribes to. Treat it as a stable contract.
        /// </summary>
        public string MeterName
        {
            get
            {
                return _MeterName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(MeterName));
                _MeterName = value;
            }
        }

        /// <summary>
        /// Activity source name used for trace emission. Default is "Watson".
        /// This is the string a trace host subscribes to. Treat it as a stable contract.
        /// </summary>
        public string ActivitySourceName
        {
            get
            {
                return _ActivitySourceName;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ActivitySourceName));
                _ActivitySourceName = value;
            }
        }

        /// <summary>
        /// Enable or disable metric instruments. Default is true.
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Enable or disable the per-request server span. Default is true.
        /// </summary>
        public bool EnableTraces { get; set; } = true;

        /// <summary>
        /// Adopt an inbound W3C trace context (traceparent, tracestate, baggage) as the parent of the
        /// server span. Default is true.
        /// </summary>
        public bool PropagateContext { get; set; } = true;

        /// <summary>
        /// Record the request body size metric and span attribute. Default is true.
        /// </summary>
        public bool CaptureRequestBodySize { get; set; } = true;

        /// <summary>
        /// Record the response body size metric and span attribute. Default is true.
        /// </summary>
        public bool CaptureResponseBodySize { get; set; } = true;

        /// <summary>
        /// Stamp the request and response content type (normalized media type) on the server span.
        /// Default is true.
        /// </summary>
        public bool CaptureContentType { get; set; } = true;

        /// <summary>
        /// Record WebSocket session and handshake metrics. Default is true.
        /// </summary>
        public bool CaptureWebSocketMetrics { get; set; } = true;

        /// <summary>
        /// Attach an exception span event to the server span on failure. Default is true.
        /// </summary>
        public bool RecordExceptionEvents { get; set; } = true;

        /// <summary>
        /// Resolve the span's client.address from a forwarded header instead of the raw socket peer.
        /// Default is false. Leave disabled on internet-facing listeners; enable only behind a known
        /// proxy and declare that proxy in <see cref="TrustedProxies"/>. A spoofed header is never
        /// trusted from an untrusted peer.
        /// </summary>
        public bool TrustForwardedHeaders { get; set; } = false;

        /// <summary>
        /// Header carrying the client-chain IP list. Default is "X-Forwarded-For".
        /// </summary>
        public string ForwardedForHeader
        {
            get
            {
                return _ForwardedForHeader;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ForwardedForHeader));
                _ForwardedForHeader = value;
            }
        }

        /// <summary>
        /// Header carrying the client-visible scheme, used to override url.scheme when the request is
        /// trusted. Default is "X-Forwarded-Proto".
        /// </summary>
        public string ForwardedProtoHeader
        {
            get
            {
                return _ForwardedProtoHeader;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(ForwardedProtoHeader));
                _ForwardedProtoHeader = value;
            }
        }

        /// <summary>
        /// Allow-list of proxy addresses permitted to set forwarded headers. Only meaningful when
        /// <see cref="TrustForwardedHeaders"/> is true. When empty, only the immediate socket peer is
        /// trusted (the safe single-proxy default).
        /// </summary>
        public Matcher TrustedProxies
        {
            get
            {
                return _TrustedProxies;
            }
            set
            {
                if (value == null) value = new Matcher();
                _TrustedProxies = value;
            }
        }

        /// <summary>
        /// Maximum number of trusted proxy hops to walk when resolving the client address.
        /// Default is 1. Clamped to a minimum of 0.
        /// </summary>
        public int ForwardLimit
        {
            get
            {
                return _ForwardLimit;
            }
            set
            {
                if (value < 0) value = 0;
                _ForwardLimit = value;
            }
        }

        /// <summary>
        /// Settings for the optional in-process Prometheus scrape endpoint.
        /// </summary>
        public TelemetryPrometheusSettings Prometheus
        {
            get
            {
                return _Prometheus;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Prometheus));
                _Prometheus = value;
            }
        }

        #endregion

        #region Private-Members

        private string _MeterName = WatsonTelemetryNames.MeterName;
        private string _ActivitySourceName = WatsonTelemetryNames.ActivitySourceName;
        private string _ForwardedForHeader = "X-Forwarded-For";
        private string _ForwardedProtoHeader = "X-Forwarded-Proto";
        private Matcher _TrustedProxies = new Matcher();
        private int _ForwardLimit = 1;
        private TelemetryPrometheusSettings _Prometheus = new TelemetryPrometheusSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with defaults.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion
    }
}
