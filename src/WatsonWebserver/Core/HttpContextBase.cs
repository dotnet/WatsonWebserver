namespace WatsonWebserver.Core
{
    using WatsonWebserver.Core.Routing;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using Timestamps;

    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContextBase : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the context object was created.
        /// </summary>
        [JsonPropertyOrder(0)]
        public Timestamp Timestamp
        {
            get
            {
                if (_Timestamp == null) _Timestamp = new Timestamp();
                return _Timestamp;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Timestamp));
                _Timestamp = value;
            }
        }

        /// <summary>
        /// The negotiated HTTP protocol.
        /// </summary>
        [JsonPropertyOrder(1)]
        public HttpProtocol Protocol { get; set; } = HttpProtocol.Http1;

        /// <summary>
        /// Connection metadata for the current request lifecycle.
        /// </summary>
        [JsonPropertyOrder(2)]
        public ConnectionMetadata Connection
        {
            get
            {
                if (_Connection == null)
                {
                    if (_ConnectionFactory != null)
                    {
                        _Connection = _ConnectionFactory();
                        _ConnectionFactory = null;
                    }
                    else
                    {
                        _Connection = new ConnectionMetadata();
                    }
                }

                return _Connection;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Connection));
                _Connection = value;
                _ConnectionFactory = null;
            }
        }

        /// <summary>
        /// Stream metadata for the current request lifecycle.
        /// </summary>
        [JsonPropertyOrder(3)]
        public StreamMetadata Stream
        {
            get
            {
                if (_Stream == null)
                {
                    if (_StreamFactory != null)
                    {
                        _Stream = _StreamFactory();
                        _StreamFactory = null;
                    }
                    else
                    {
                        _Stream = new StreamMetadata();
                    }
                }

                return _Stream;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Stream));
                _Stream = value;
                _StreamFactory = null;
            }
        }

        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        [JsonPropertyOrder(4)]
        public HttpRequestBase Request { get; set; } = null;

        /// <summary>
        /// Type of route.
        /// </summary>
        [JsonPropertyOrder(5)]
        public RouteTypeEnum RouteType { get; set; } = RouteTypeEnum.Default;

        /// <summary>
        /// Matched route.
        /// </summary>
        [JsonPropertyOrder(6)]
        public object Route { get; set; } = null;

        /// <summary>
        /// Globally-unique identifier for the context.
        /// </summary>
        [JsonPropertyOrder(7)]
        public Guid Guid
        {
            get
            {
                if (_Guid == Guid.Empty) _Guid = Guid.NewGuid();
                return _Guid;
            }
            set
            {
                if (value == Guid.Empty) throw new ArgumentException("Guid cannot be empty.", nameof(Guid));
                _Guid = value;
            }
        }

        /// <summary>
        /// Cancellation token source.
        /// </summary>
        [JsonPropertyOrder(8)]
        [JsonIgnore]
        public CancellationTokenSource TokenSource
        {
            get
            {
                if (_TokenSource == null) _TokenSource = new CancellationTokenSource();
                return _TokenSource;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(TokenSource));
                _TokenSource = value;
            }
        }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        [JsonPropertyOrder(9)]
        [JsonIgnore]
        public CancellationToken Token
        {
            get
            {
                return _TokenSource != null ? _TokenSource.Token : CancellationToken.None;
            }
        }

        /// <summary>
        /// Indicates whether request processing was aborted before completion.
        /// </summary>
        [JsonPropertyOrder(10)]
        public bool RequestAborted { get; set; } = false;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        [JsonPropertyOrder(998)]
        public HttpResponseBase Response { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        private Timestamp _Timestamp = null;
        private CancellationTokenSource _TokenSource = null;
        private ConnectionMetadata _Connection = null;
        private StreamMetadata _Stream = null;
        private Func<ConnectionMetadata> _ConnectionFactory = null;
        private Func<StreamMetadata> _StreamFactory = null;
        private Guid _Guid = Guid.Empty;
        private DateTime _TimingStartUtc = DateTime.MinValue;
        private long _TimingStartTicks = 0;
        private bool _TimingStarted = false;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    if (Response != null)
                    {
                        try
                        {
                            Response.Dispose();
                        }
                        catch
                        {
                        }

                        Response = null;
                    }

                    if (Request != null)
                    {
                        try
                        {
                            Request.Dispose();
                        }
                        catch
                        {
                        }

                        Request = null;
                    }

                    if (_TokenSource != null)
                    {
                        _TokenSource.Cancel();
                        _TokenSource.Dispose();
                        _TokenSource = null;
                    }
                }

                _Disposed = true;
            }
        }

        /// <summary>
        /// Reset the context so it can be safely reused by an object pool.
        /// </summary>
        protected internal virtual void ResetForReuse()
        {
            if (_TokenSource != null)
            {
                _TokenSource.Cancel();
                _TokenSource.Dispose();
                _TokenSource = null;
            }

            _Timestamp = null;
            Protocol = HttpProtocol.Http1;
            _Connection = null;
            _Stream = null;
            _ConnectionFactory = null;
            _StreamFactory = null;
            Request = null;
            RouteType = RouteTypeEnum.Default;
            Route = null;
            _Guid = Guid.Empty;
            _TimingStartUtc = DateTime.MinValue;
            _TimingStartTicks = 0;
            _TimingStarted = false;
            RequestAborted = false;
            Response = null;
            Metadata = null;
            _Disposed = false;
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Set a deferred connection metadata factory.
        /// </summary>
        /// <param name="factory">Factory.</param>
        protected internal void SetConnectionFactory(Func<ConnectionMetadata> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _ConnectionFactory = factory;
            _Connection = null;
        }

        /// <summary>
        /// Set a deferred stream metadata factory.
        /// </summary>
        /// <param name="factory">Factory.</param>
        protected internal void SetStreamFactory(Func<StreamMetadata> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _StreamFactory = factory;
            _Stream = null;
        }

        /// <summary>
        /// Start request timing using a monotonic clock for elapsed time calculations.
        /// </summary>
        protected internal void StartTiming()
        {
            if (_TimingStarted) return;

            _TimingStartUtc = DateTime.UtcNow;
            _TimingStartTicks = Stopwatch.GetTimestamp();
            _TimingStarted = true;
            Timestamp.Start = _TimingStartUtc;
        }

        /// <summary>
        /// Complete request timing using the monotonic elapsed time.
        /// </summary>
        protected internal void CompleteTiming()
        {
            if (!_TimingStarted) StartTiming();

            Timestamp.End = _TimingStartUtc.AddMilliseconds(GetElapsedMilliseconds(_TimingStartTicks, Stopwatch.GetTimestamp()));
        }

        private static double GetElapsedMilliseconds(long startTicks, long endTicks)
        {
            return (endTicks - startTicks) * 1000d / Stopwatch.Frequency;
        }

        #endregion
    }
}
