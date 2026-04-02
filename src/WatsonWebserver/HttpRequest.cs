namespace WatsonWebserver
{
    using System;
    using System.Buffers;
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
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http1;

    /// <summary>
    /// HTTP request.
    /// </summary>
    public class HttpRequest : HttpRequestBase
    {
        #region Public-Members

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public override Stream Data { get; set; } = null;
         
        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public override byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (Data != null)
                {
                    if (ContentLength > 0) _DataAsBytes = ReadStreamFully(Data, ContentLength);
                    else if (ChunkedTransfer) _DataAsBytes = ReadChunkedBodyAsync(CancellationToken.None).GetAwaiter().GetResult();
                    else
                    {
                        _DataAsBytes = Array.Empty<byte>();
                        _BodyComplete = true;
                    }
                    return _DataAsBytes;
                }
                return null;
            }
        }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public override string DataAsString
        {
            get
            {
                if (_DataAsString != null) return _DataAsString;
                if (_DataAsBytes != null)
                {
                    _DataAsString = Encoding.UTF8.GetString(_DataAsBytes);
                    return _DataAsString;
                }
                if (Data != null)
                {
                    if (ContentLength > 0) _DataAsBytes = ReadStreamFully(Data, ContentLength);
                    else if (ChunkedTransfer) _DataAsBytes = ReadChunkedBodyAsync(CancellationToken.None).GetAwaiter().GetResult();
                    else
                    {
                        _DataAsBytes = Array.Empty<byte>();
                        _BodyComplete = true;
                    }
                    if (_DataAsBytes != null)
                    {
                        _DataAsString = Encoding.UTF8.GetString(_DataAsBytes);
                        return _DataAsString;
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// The original HttpListenerContext from which the HttpRequest was constructed.
        /// </summary>
        [JsonIgnore]
        public HttpListenerContext ListenerContext { get; set; }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private Uri _Uri = null;
        private byte[] _DataAsBytes = null;
        private string _DataAsString = null;
        private ISerializationHelper _Serializer = null;
        private WebserverSettings _Settings = null;
        private bool _BodyComplete = false;
        private Http1RequestMetadata _Http1Metadata = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// HTTP request.
        /// </summary>
        public HttpRequest()
        { 
        }

        /// <summary>
        /// HTTP request.
        /// Instantiate the object using an HttpListenerContext.
        /// </summary>
        /// <param name="ctx">HttpListenerContext.</param>
        /// <param name="serializer">Serialization helper.</param>
        public HttpRequest(HttpListenerContext ctx, ISerializationHelper serializer)
        { 
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            Initialize(ctx, serializer);
        }

        /// <summary>
        /// HTTP request from a raw HTTP/1.1 stream.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="stream">Readable request stream.</param>
        /// <param name="metadata">Parsed request metadata.</param>
        public HttpRequest(WebserverSettings settings, Stream stream, Http1RequestMetadata metadata)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            Initialize(settings, stream, metadata);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public override async Task<Chunk> ReadChunk(CancellationToken token = default)
        {
            Chunk chunk = await Http1ChunkReader.ReadAsync(Data, _StreamBufferSize, token).ConfigureAwait(false);
            if (chunk != null && chunk.IsFinal) _BodyComplete = true;
            return chunk;
        }
         
        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if exists.</returns>
        public override bool HeaderExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (_Http1Metadata != null)
            {
                return _Http1Metadata.HeaderExists(key);
            }

            if (Headers != null)
            {
                return Headers.AllKeys.Any(k => !String.IsNullOrEmpty(k) && String.Equals(k, key, StringComparison.InvariantCultureIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public override bool QuerystringExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                return Query.Elements.AllKeys.Any(k => !String.IsNullOrEmpty(k) && String.Equals(k, key, StringComparison.InvariantCultureIgnoreCase));
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public override string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (_Http1Metadata != null)
            {
                return _Http1Metadata.RetrieveHeaderValue(key);
            }

            if (Headers != null)
            { 
                return Headers.Get(key);
            }

            return null;
        }

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public override string RetrieveQueryValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                string val = Query.Elements.Get(key);
                if (!String.IsNullOrEmpty(val))
                {
                    val = WebUtility.UrlDecode(val);
                }

                return val;
            }

            return null;
        }

        /// <summary>
        /// Asynchronously read the entire request body.
        /// After calling this method, DataAsBytes and DataAsString return cached data with zero blocking.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The request body as a byte array, or null if no body is present.</returns>
        public override async Task<byte[]> ReadBodyAsync(CancellationToken token = default)
        {
            if (_DataAsBytes != null) return _DataAsBytes;
            if (Data == null) return null;

            if (ContentLength > 0)
            {
                _DataAsBytes = await ReadStreamExactAsync(Data, ContentLength, token).ConfigureAwait(false);
            }
            else if (ChunkedTransfer)
            {
                _DataAsBytes = await ReadChunkedBodyAsync(token).ConfigureAwait(false);
            }
            else
            {
                _DataAsBytes = Array.Empty<byte>();
                _BodyComplete = true;
            }

            return _DataAsBytes;
        }

        internal bool IsRequestBodyComplete
        {
            get
            {
                if (_BodyComplete) return true;
                if (ChunkedTransfer) return false;
                return ContentLength <= 0;
            }
        }

        internal void MarkBodyComplete()
        {
            _BodyComplete = true;
        }

        internal void Initialize(HttpListenerContext ctx, ISerializationHelper serializer)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            _Serializer = serializer;
            ListenerContext = ctx;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            Keepalive = ctx.Request.KeepAlive;
            ContentLength = ctx.Request.ContentLength64;
            Useragent = ctx.Request.UserAgent;
            ContentType = ctx.Request.ContentType;

            _Uri = new Uri(ctx.Request.Url.ToString().Trim());
            ProtocolVersion = "HTTP/" + ctx.Request.ProtocolVersion.ToString();
            string sourceIp = ctx.Request.RemoteEndPoint.Address.ToString();
            int sourcePort = ctx.Request.RemoteEndPoint.Port;
            string destinationIp = ctx.Request.LocalEndPoint.Address.ToString();
            int destinationPort = ctx.Request.LocalEndPoint.Port;
            string hostname = _Uri.Host;
            Source = new SourceDetails(sourceIp, sourcePort);
            Destination = new DestinationDetails(destinationIp, destinationPort, hostname);
            Url = new UrlDetails(ctx.Request.Url.ToString().Trim(), ctx.Request.RawUrl.ToString().Trim());
            if (ctx.Request.RawUrl != null
                && ctx.Request.RawUrl.IndexOf("?", StringComparison.Ordinal) >= 0)
            {
                Query = new QueryDetails(ctx.Request.RawUrl.ToString().Trim());
            }

            MethodRaw = ctx.Request.HttpMethod;
            try
            {
                Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), ctx.Request.HttpMethod, true);
            }
            catch (Exception)
            {
                Method = HttpMethod.UNKNOWN;
            }

            Headers = ctx.Request.Headers;
            for (int i = 0; i < Headers.Count; i++)
            {
                string key = Headers.GetKey(i);
                string[] vals = Headers.GetValues(i);

                if (String.IsNullOrEmpty(key)) continue;
                if (vals == null || vals.Length < 1) continue;

                if (String.Equals(key, "transfer-encoding", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (vals.Contains("chunked", StringComparer.InvariantCultureIgnoreCase)) ChunkedTransfer = true;
                    if (vals.Contains("gzip", StringComparer.InvariantCultureIgnoreCase)) Gzip = true;
                    if (vals.Contains("deflate", StringComparer.InvariantCultureIgnoreCase)) Deflate = true;
                }
                else if (String.Equals(key, "x-amz-content-sha256", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (vals.Contains("streaming", StringComparer.InvariantCultureIgnoreCase)) ChunkedTransfer = true;
                }
            }

            Data = ctx.Request.InputStream;
        }

        internal void Initialize(WebserverSettings settings, Stream stream, Http1RequestMetadata metadata)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            _Settings = settings;
            OwnsDataStream = false;
            Data = stream;
            BuildRawRequest(metadata);
        }

        internal void ReturnToPool()
        {
            ResetForReuse();
        }

        /// <summary>
        /// Reset the HTTP/1.1 request before returning it to the pool.
        /// </summary>
        protected internal override void ResetForReuse()
        {
            ListenerContext = null;
            _StreamBufferSize = 65536;
            _Uri = null;
            _DataAsBytes = null;
            _DataAsString = null;
            _Serializer = null;
            _Settings = null;
            _BodyComplete = false;
            _Http1Metadata = null;
            base.ResetForReuse();
        }

        #endregion

        #region Private-Methods

        private byte[] ReadStreamFully(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = ArrayPool<byte>.Shared.Rent(_StreamBufferSize);
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    int read = 0;

                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        memoryStream.Write(buffer, 0, read);
                    }

                    byte[] response = memoryStream.ToArray();
                    _BodyComplete = true;
                    return response;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private byte[] ReadStreamFully(Stream input, long contentLength)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");
            if (contentLength < 1)
            {
                _BodyComplete = true;
                return Array.Empty<byte>();
            }

            byte[] response = new byte[GetFixedLengthBodySize(contentLength)];
            int offset = 0;

            while (offset < response.Length)
            {
                int bytesRead = input.Read(response, offset, response.Length - offset);
                if (bytesRead < 1)
                {
                    throw new MalformedHttpRequestException("Unexpected end of stream while reading the request body.");
                }

                offset += bytesRead;
            }

            _BodyComplete = true;
            return response;
        }

        private async Task<byte[]> ReadStreamExactAsync(Stream input, long contentLength, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");
            if (contentLength < 1)
            {
                _BodyComplete = true;
                return Array.Empty<byte>();
            }

            byte[] response = new byte[GetFixedLengthBodySize(contentLength)];
            int offset = 0;

            while (offset < response.Length)
            {
                int bytesRead = await input.ReadAsync(response, offset, response.Length - offset, token).ConfigureAwait(false);
                if (bytesRead < 1)
                {
                    throw new MalformedHttpRequestException("Unexpected end of stream while reading the request body.");
                }

                offset += bytesRead;
            }

            _BodyComplete = true;
            return response;
        }

        private async Task<byte[]> ReadStreamFullyAsync(Stream input, CancellationToken token = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = ArrayPool<byte>.Shared.Rent(_StreamBufferSize);
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    int read = 0;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                    {
                        memoryStream.Write(buffer, 0, read);
                    }

                    _BodyComplete = true;
                    return memoryStream.ToArray();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<byte[]> ReadChunkedBodyAsync(CancellationToken token)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                while (true)
                {
                    Chunk chunk = await ReadChunk(token).ConfigureAwait(false);
                    if (chunk.Data != null && chunk.Data.Length > 0)
                    {
                        memoryStream.Write(chunk.Data, 0, chunk.Data.Length);
                    }

                    if (chunk.IsFinal) break;
                }

                _BodyComplete = true;
                return memoryStream.ToArray();
            }
        }

        private void BuildRawRequest(Http1RequestMetadata metadata)
        {
            _Http1Metadata = metadata;
            Source = metadata.Source;
            Destination = metadata.Destination;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            Method = metadata.Method;
            MethodRaw = metadata.MethodRaw;
            SetUrlFactory(() => metadata.Url);
            SetQueryFactory(() => metadata.Query);
            SetHeadersFactory(() => metadata.Headers);
            Protocol = HttpProtocol.Http1;
            ProtocolVersion = metadata.ProtocolVersion;
            Keepalive = metadata.Keepalive;
            ChunkedTransfer = metadata.ChunkedTransfer;
            Gzip = metadata.Gzip;
            Deflate = metadata.Deflate;
            Useragent = metadata.Useragent;
            ContentType = metadata.ContentType;
            ContentLength = metadata.ContentLength;
        }

        private int GetFixedLengthBodySize(long contentLength)
        {
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            if (contentLength > Int32.MaxValue) throw new IOException("Request body exceeds the maximum supported in-memory size.");
            return Convert.ToInt32(contentLength, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
