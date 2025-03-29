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
    public abstract class HttpRequestBase
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request object was received.
        /// </summary>
        [JsonPropertyOrder(-11)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// Globally-unique identifier for the request.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public int ThreadId { get; set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public string ProtocolVersion { get; set; } = "HTTP/1.1";

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public SourceDetails Source { get; set; } = new SourceDetails();

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public DestinationDetails Destination { get; set; } = new DestinationDetails();

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
        public UrlDetails Url { get; set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public QueryDetails Query { get; set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public NameValueCollection Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Headers = value;
            }
        }

        /// <summary>
        /// Authorization details.
        /// </summary>
        public AuthorizationDetails Authorization
        {
            get
            {
                if (_Headers != null && _Headers.AllKeys.Contains("Authorization"))
                {
                    return new AuthorizationDetails(_Headers.Get("Authorization"));
                }

                return new AuthorizationDetails();
            }
        }

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
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

        #endregion

        #region Private-Members

        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public abstract Task<Chunk> ReadChunk(CancellationToken token = default);

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

        #endregion
    }
}
