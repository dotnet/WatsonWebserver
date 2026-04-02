namespace WatsonWebserver.Core
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Timestamps;

    /// <summary>
    /// HTTP request.
    /// </summary>
    public abstract class HttpRequestBase : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request object was received.
        /// </summary>
        [JsonPropertyOrder(-11)]
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
        /// Globally-unique identifier for the request.
        /// </summary>
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
        /// The HTTP protocol in use for the current request.
        /// </summary>
        [JsonPropertyOrder(-10)]
        public HttpProtocol Protocol { get; set; } = HttpProtocol.Http1;

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public int ThreadId { get; set; } = 0;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public string ProtocolVersion { get; set; } = "HTTP/1.1";

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public SourceDetails Source
        {
            get
            {
                if (_Source == null) _Source = new SourceDetails();
                return _Source;
            }
            set
            {
                if (value == null) value = new SourceDetails();
                _Source = value;
            }
        }

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public DestinationDetails Destination
        {
            get
            {
                if (_Destination == null) _Destination = new DestinationDetails();
                return _Destination;
            }
            set
            {
                if (value == null) value = new DestinationDetails();
                _Destination = value;
            }
        }

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonPropertyOrder(-6)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The string version of the HTTP method, useful if Method is UNKNOWN.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public string MethodRaw { get; set; } = null;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public UrlDetails Url
        {
            get
            {
                if (_Url == null)
                {
                    if (_UrlFactory != null)
                    {
                        _Url = _UrlFactory() ?? new UrlDetails();
                        _UrlFactory = null;
                    }
                    else
                    {
                        _Url = new UrlDetails();
                    }
                }

                return _Url;
            }
            set
            {
                if (value == null) value = new UrlDetails();
                _Url = value;
                _UrlFactory = null;
            }
        }

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public QueryDetails Query
        {
            get
            {
                if (_Query == null)
                {
                    if (_QueryFactory != null)
                    {
                        _Query = _QueryFactory() ?? new QueryDetails();
                        _QueryFactory = null;
                    }
                    else
                    {
                        _Query = new QueryDetails();
                    }
                }

                return _Query;
            }
            set
            {
                if (value == null) value = new QueryDetails();
                _Query = value;
                _QueryFactory = null;
            }
        }

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public NameValueCollection Headers
        {
            get
            {
                if (_Headers == null)
                {
                    if (_HeadersFactory != null)
                    {
                        _Headers = _HeadersFactory() ?? new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                        _HeadersFactory = null;
                    }
                    else
                    {
                        _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    }
                }

                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Headers = value;
                _HeadersFactory = null;
            }
        }

        /// <summary>
        /// Authorization details.
        /// </summary>
        public AuthorizationDetails Authorization
        {
            get
            {
                NameValueCollection headers = Headers;
                if (headers != null && headers.AllKeys.Contains("Authorization"))
                {
                    return new AuthorizationDetails(headers.Get("Authorization"));
                }

                return new AuthorizationDetails();
            }
        }

        /// <summary>
        /// Request trailers supplied by the client when the protocol permits them.
        /// </summary>
        public NameValueCollection Trailers
        {
            get
            {
                if (_Trailers == null) _Trailers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                return _Trailers;
            }
            set
            {
                if (value == null) _Trailers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Trailers = value;
            }
        }

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// This property is specific to HTTP/1.1.  HTTP/2 and HTTP/3 use their own framing mechanisms.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; set; } = false;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public abstract Stream Data { get; set; }

        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public abstract byte[] DataAsBytes { get; }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public abstract string DataAsString { get; }

        /// <summary>
        /// Indicates whether the request owns the underlying data stream lifetime.
        /// </summary>
        protected internal bool OwnsDataStream { get; set; } = true;

        #endregion

        #region Private-Members

        private Timestamp _Timestamp = null;
        private Guid _Guid = Guid.Empty;
        private SourceDetails _Source = null;
        private DestinationDetails _Destination = null;
        private UrlDetails _Url = null;
        private QueryDetails _Query = null;
        private NameValueCollection _Headers = null;
        private NameValueCollection _Trailers = null;
        private Func<UrlDetails> _UrlFactory = null;
        private Func<QueryDetails> _QueryFactory = null;
        private Func<NameValueCollection> _HeadersFactory = null;
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
                    if (OwnsDataStream && Data != null)
                    {
                        try { Data.Dispose(); } catch { }
                        Data = null;
                    }
                }

                _Disposed = true;
            }
        }

        /// <summary>
        /// Reset the request so it can be safely reused by an object pool.
        /// </summary>
        protected internal virtual void ResetForReuse()
        {
            if (OwnsDataStream && Data != null)
            {
                try { Data.Dispose(); } catch { }
            }

            Data = null;
            OwnsDataStream = true;
            _Timestamp = null;
            _Guid = Guid.Empty;
            Protocol = HttpProtocol.Http1;
            ThreadId = 0;
            ProtocolVersion = "HTTP/1.1";
            _Source = null;
            _Destination = null;
            Method = HttpMethod.GET;
            MethodRaw = null;
            _Url = null;
            _Query = null;
            _Headers = null;
            _Trailers = null;
            _UrlFactory = null;
            _QueryFactory = null;
            _HeadersFactory = null;
            Keepalive = false;
            ChunkedTransfer = false;
            Gzip = false;
            Deflate = false;
            Useragent = null;
            ContentType = null;
            ContentLength = 0;
            _Disposed = false;
        }

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// This method is specific to HTTP/1.1 chunked transfer encoding.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public abstract Task<Chunk> ReadChunk(CancellationToken token = default);

        /// <summary>
        /// Asynchronously read the entire request body.
        /// After calling this method, DataAsBytes and DataAsString return cached data with zero blocking.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request body as a byte array, or null if no body is present.</returns>
        public virtual Task<byte[]> ReadBodyAsync(CancellationToken token = default)
        {
            return Task.FromResult(DataAsBytes);
        }

        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool HeaderExists(string key);

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool QuerystringExists(string key);

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public abstract string RetrieveHeaderValue(string key);

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public abstract string RetrieveQueryValue(string key);

        #endregion

        #region Private-Methods

        /// <summary>
        /// Set a deferred URL factory.
        /// </summary>
        /// <param name="factory">Factory.</param>
        protected internal void SetUrlFactory(Func<UrlDetails> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _UrlFactory = factory;
            _Url = null;
        }

        /// <summary>
        /// Set a deferred query factory.
        /// </summary>
        /// <param name="factory">Factory.</param>
        protected internal void SetQueryFactory(Func<QueryDetails> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _QueryFactory = factory;
            _Query = null;
        }

        /// <summary>
        /// Set a deferred header collection factory.
        /// </summary>
        /// <param name="factory">Factory.</param>
        protected internal void SetHeadersFactory(Func<NameValueCollection> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _HeadersFactory = factory;
            _Headers = null;
        }

        #endregion
    }
}
