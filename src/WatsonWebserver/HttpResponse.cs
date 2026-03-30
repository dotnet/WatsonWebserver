namespace WatsonWebserver
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using WatsonWebserver.Core;

    /// <summary>
    /// HTTP response.
    /// </summary>
    public class HttpResponse : HttpResponseBase
    {
        #region Public-Members

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
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
                if (_Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(_Data);
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
        /// Retrieve the response body sent using a Send() or SendAsync() method.
        /// </summary>
        [JsonIgnore]
        public override byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (_Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(_Data);
                    return _DataAsBytes;
                }
                return null;
            }
        }

        /// <summary>
        /// Response data stream sent to the requestor.
        /// </summary>
        [JsonIgnore]
        public override MemoryStream Data
        {
            get
            {
                if (_Data == null && _DataAsBytes != null)
                {
                    _Data = new MemoryStream(_DataAsBytes, false);
                }

                return _Data;
            }
        }

        #endregion

        #region Private-Members

        private HttpRequestBase _Request = null;
        private HttpListenerContext _Context = null;
        private HttpListenerResponse _Response = null;
        private Stream _OutputStream = null;
        private Stream _Stream = null;
        private bool _HeadersSet = false;
        private bool _HeadersSent = false;

        private WebserverSettings _Settings = new WebserverSettings();
        private WebserverEvents _Events = new WebserverEvents();
        private int _StreamBufferSize = 65536;

        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        private byte[] _DataAsBytes = null;
        private string _DataAsString = null;
        private MemoryStream _Data = null;
        private ISerializationHelper _Serializer = null;
        private bool _DirectRequestBodyPassthrough = false;
        private static readonly ConcurrentDictionary<string, byte[]> _SimpleHeaderTemplateCache = new ConcurrentDictionary<string, byte[]>();
        private static readonly ConcurrentDictionary<string, byte[]> _StatusLineCache = new ConcurrentDictionary<string, byte[]>();
        private static readonly object _SimpleHeaderTemplateCacheSync = new object();
        private static readonly object _StatusLineCacheSync = new object();
        private static readonly byte[] _ColonSpaceBytes = Encoding.ASCII.GetBytes(": ");
        private static readonly byte[] _CrlfBytes = Encoding.ASCII.GetBytes("\r\n");
        private static readonly byte[] _ChunkedTransferEncodingBytes = Encoding.ASCII.GetBytes("chunked");
        private static readonly byte[] _HeaderContentTypeBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderContentType + ": ");
        private static readonly byte[] _HeaderContentLengthBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderContentLength + ": ");
        private static readonly byte[] _HeaderTransferEncodingBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderTransferEncoding + ": ");
        private static readonly byte[] _HeaderConnectionBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderConnection + ": ");
        private static readonly byte[] _HeaderCacheControlBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderCacheControl + ": ");
        private static readonly byte[] _HeaderAltSvcBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderAltSvc + ": ");
        private static readonly byte[] _HeaderDateBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderDate + ": ");
        private static readonly object _DateHeaderSync = new object();
        private static long _CachedDateHeaderSecond = -1;
        private static string _CachedDateHeaderValue = null;
        private static byte[] _CachedDateHeaderBytes = null;
        private const int SmallResponseFirstWriteLimit = 16 * 1024;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(
            HttpRequestBase req, 
            HttpListenerContext ctx, 
            WebserverSettings settings, 
            WebserverEvents events,
            ISerializationHelper serializer)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            Initialize(req, ctx, settings, events, serializer);
        }

        internal HttpResponse(
            HttpRequestBase req,
            WebserverSettings settings,
            WebserverEvents events,
            Stream stream,
            int bufferSize)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new IOException("Cannot write to supplied stream.");
            if (bufferSize < 1) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            Initialize(req, settings, events, stream, bufferSize);
        }

        #endregion

        #region Public-Methods
         
        /// <inheritdoc />
        public override async Task<bool> Send(CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");
            return await SendInternalAsync(0, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(long contentLength, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");
            ContentLength = contentLength;
            return await SendInternalAsync(0, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(string data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");
            if (String.IsNullOrEmpty(data))
                return await SendInternalAsync(0, null, token).ConfigureAwait(false);

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            SetCachedResponseData(bytes);
            return await SendPayloadAsync(bytes, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(byte[] data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");
            if (data == null || data.Length < 1)
                    return await SendInternalAsync(0, null, token).ConfigureAwait(false);

            SetCachedResponseData(data);
            return await SendPayloadAsync(data, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");
            return await SendInternalAsync(contentLength, stream, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> SendChunk(byte[] chunk, bool isFinal, CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSet) SendHeaders();
            MarkResponseStarted();

            if (chunk != null && chunk.Length > 0)
                ContentLength += chunk.Length;

            try
            {
                if (_Response == null && !_HeadersSent)
                {
                    byte[] headers = GetHeaderBytes();
                    await _OutputStream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                    _HeadersSent = true;
                }

                // When SendChunked = true, http.sys expects us to write raw chunk data
                // and it will handle the chunked encoding format automatically
                if (_Response == null)
                {
                    if ((chunk == null || chunk.Length < 1) && !isFinal)
                    {
                        return true;
                    }

                    if (chunk == null || chunk.Length < 1) chunk = Array.Empty<byte>();

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        byte[] header = Encoding.UTF8.GetBytes(chunk.Length.ToString("X") + "\r\n");
                        memoryStream.Write(header, 0, header.Length);
                        if (chunk.Length > 0) memoryStream.Write(chunk, 0, chunk.Length);
                        byte[] crlf = Encoding.UTF8.GetBytes("\r\n");
                        memoryStream.Write(crlf, 0, crlf.Length);
                        if (isFinal)
                        {
                            byte[] finalChunk = Encoding.UTF8.GetBytes("0\r\n\r\n");
                            memoryStream.Write(finalChunk, 0, finalChunk.Length);
                        }

                        byte[] payload = memoryStream.ToArray();
                        await _OutputStream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
                    }
                }
                else if (chunk != null && chunk.Length > 0)
                {
                    await _OutputStream.WriteAsync(chunk, 0, chunk.Length, token).ConfigureAwait(false);
                }

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);

                if (isFinal)
                {
                    if (!ShouldKeepConnectionOpen())
                    {
                        // For http.sys, we need to close the stream to signal the final chunk
                        // http.sys will automatically send the "0\r\n\r\n" final chunk marker
                        _OutputStream.Close();
                        if (_Response != null) _Response.Close();
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (isFinal) ResponseSent = true;
            }
        }

        /// <inheritdoc />
        public override async Task<bool> SendEvent(ServerSentEvent sse, bool isFinal, CancellationToken token = default)
        {
            if (!ServerSentEvents) throw new IOException("Response is not configured to use server-sent events.  Set ServerSentEvents to true first, otherwise use Send().");
            if (!_HeadersSet) SendHeaders();
            if (sse == null) throw new ArgumentNullException(nameof(sse));
            MarkResponseStarted();
            
            string msg = sse.ToEventString();
            if (String.IsNullOrEmpty(msg)) throw new ArgumentException("A null or unpopulated server-sent event object was supplied.");

            try
            {
                if (_Response == null && !_HeadersSent)
                {
                    byte[] headers = GetHeaderBytes();
                    await _OutputStream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                    _HeadersSent = true;
                }

                byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
                await _OutputStream.WriteAsync(msgBytes, 0, msgBytes.Length, token).ConfigureAwait(false);
                await _OutputStream.FlushAsync(token).ConfigureAwait(false);

                if (isFinal)
                {
                    if (!ShouldKeepConnectionOpen())
                    {
                        _OutputStream.Close();
                        if (_Response != null) _Response.Close();
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (isFinal) ResponseSent = true;
            }
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_Data != null)
                {
                    try { _Data.Dispose(); } catch { }
                    _Data = null;
                }
            }

            base.Dispose(disposing);
        }

        internal void Initialize(
            HttpRequestBase req,
            HttpListenerContext ctx,
            WebserverSettings settings,
            WebserverEvents events,
            ISerializationHelper serializer)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            _Serializer = serializer;
            _Request = req;
            _Context = ctx;
            _Response = _Context.Response;
            _Settings = settings;
            _Events = events;
            Protocol = req.Protocol;
            ProtocolVersion = req.ProtocolVersion;
            _OutputStream = _Response.OutputStream;
        }

        internal void Initialize(
            HttpRequestBase req,
            WebserverSettings settings,
            WebserverEvents events,
            Stream stream,
            int bufferSize)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new IOException("Cannot write to supplied stream.");
            if (bufferSize < 1) throw new ArgumentOutOfRangeException(nameof(bufferSize));

            _Request = req;
            _Settings = settings;
            _Events = events;
            _Stream = stream;
            _OutputStream = stream;
            _StreamBufferSize = bufferSize;
            Protocol = req.Protocol;
            ProtocolVersion = req.ProtocolVersion;
        }

        internal void ReturnToPool()
        {
            ResetForReuse();
        }

        /// <summary>
        /// Reset the HTTP/1.1 response before returning it to the pool.
        /// </summary>
        protected override void ResetForReuse()
        {
            if (_Data != null)
            {
                try { _Data.Dispose(); } catch { }
                _Data = null;
            }

            _Request = null;
            _Context = null;
            _Response = null;
            _OutputStream = null;
            _Stream = null;
            _HeadersSet = false;
            _HeadersSent = false;
            _Settings = new WebserverSettings();
            _Events = new WebserverEvents();
            _StreamBufferSize = 65536;
            _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            _DataAsBytes = null;
            _DataAsString = null;
            _Serializer = null;
            _DirectRequestBodyPassthrough = false;
            base.ResetForReuse();
        }

        #endregion

        #region Private-Methods

        private string GetStatusDescription(int statusCode)
        {
            return GetStatusDescriptionStatic(statusCode);
        }

        private void SendHeaders()
        {
            if (_HeadersSet) throw new IOException("Headers already sent.");

            if (_Response == null)
            {
                SetDefaultHeaders();
                _HeadersSet = true;
                return;
            }

            _Response.ProtocolVersion = new Version(1, 1);
            _Response.ContentLength64 = ContentLength;
            _Response.StatusCode = StatusCode;
            _Response.StatusDescription = GetStatusDescription(StatusCode);
            _Response.SendChunked = (ChunkedTransfer || ServerSentEvents);
            _Response.ContentType = ContentType;
            _Response.KeepAlive = ShouldKeepConnectionOpen();

            if (ServerSentEvents)
            {
                _Response.ContentType = "text/event-stream; charset=utf-8";
                _Response.Headers.Add("Cache-Control", "no-cache");
                _Response.Headers.Add("Connection", "keep-alive");
            }

            if (Headers != null && Headers.Count > 0)
            {
                for (int i = 0; i < Headers.Count; i++)
                {
                    string key = Headers.GetKey(i);
                    string[] vals = Headers.GetValues(i);

                    if (vals == null || vals.Length < 1)
                    {
                        _Response.AddHeader(key, null);
                    }
                    else
                    {
                        for (int j = 0; j < vals.Length; j++)
                        {
                            _Response.AddHeader(key, vals[j]);
                        }
                    }
                }
            }

            if (_Settings.Headers.DefaultHeaders != null && _Settings.Headers.DefaultHeaders.Count > 0)
            {
                foreach (KeyValuePair<string, string> header in _Settings.Headers.DefaultHeaders)
                {
                    if (!HasHeader(header.Key))
                    {
                        _Response.AddHeader(header.Key, header.Value);
                    }
                }
            }

            _HeadersSet = true;
        }

        private byte[] ReadStreamFully(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
              using (MemoryStream ms = new MemoryStream())
              {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                byte[] ret = ms.ToArray();
                return ret;
            }
        }

        private async Task<bool> SendInternalAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and to finalize the chunk response SendChunk(..., isFinal: true).");

            if (ContentLength == 0 && contentLength > 0) ContentLength = contentLength;
            _DirectRequestBodyPassthrough = IsDirectRequestBodyPassthrough(stream, contentLength);

            if (!_HeadersSet) SendHeaders();

            try
            {
                MarkResponseStarted();
                if (_Response == null && !_HeadersSent)
                {
                    byte[] headers = GetHeaderBytes();
                    await _OutputStream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                    _HeadersSent = true;
                }

                if (_Request.Method != HttpMethod.HEAD)
                {
                    if (stream != null && stream.CanRead && contentLength > 0)
                    {
                        long bytesRemaining = contentLength;
                        byte[] buffer = ArrayPool<byte>.Shared.Rent(_StreamBufferSize);
                        try
                        {
                            while (bytesRemaining > 0)
                            {
                                int bytesToRead = buffer.Length;
                                if (bytesRemaining < bytesToRead)
                                {
                                    bytesToRead = Convert.ToInt32(bytesRemaining);
                                }

                                int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, token).ConfigureAwait(false);
                                if (bytesRead < 1) break;
                                await _OutputStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                                bytesRemaining -= bytesRead;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }

                        if (!ReferenceEquals(stream, _OutputStream))
                        {
                            stream.Close();
                            stream.Dispose();
                        }

                        if (_DirectRequestBodyPassthrough && _Request is HttpRequest httpRequest)
                        {
                            httpRequest.MarkBodyComplete();
                        }
                    }
                }

                bool keepConnectionOpen = ShouldKeepConnectionOpen();
                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                if (!keepConnectionOpen)
                {
                    _OutputStream.Close();
                }

                if (_Response != null && !keepConnectionOpen) _Response.Close();

                MarkResponseCompleted();
                ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                if (_Data != null)
                {
                    try { _Data.Dispose(); } catch { }
                    _Data = null;
                }

                return false;
            }
        }

        private async Task<bool> SendPayloadAsync(byte[] payload, CancellationToken token)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length < 1) return await SendInternalAsync(0, null, token).ConfigureAwait(false);

            ContentLength = payload.Length;

            if (!_HeadersSet) SendHeaders();

            try
            {
                MarkResponseStarted();
                bool payloadWrittenWithHeaders = false;
                if (_Response == null && !_HeadersSent)
                {
                    byte[] headers = GetHeaderBytes();

                    if (_Request.Method != HttpMethod.HEAD
                        && (headers.Length + payload.Length) <= SmallResponseFirstWriteLimit)
                    {
                        await WriteSmallResponseAsync(headers, payload, token).ConfigureAwait(false);
                        payloadWrittenWithHeaders = true;
                    }
                    else
                    {
                        await _OutputStream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                        _HeadersSent = true;
                    }
                }

                if (_Request.Method != HttpMethod.HEAD && !payloadWrittenWithHeaders)
                {
                    await _OutputStream.WriteAsync(payload, 0, payload.Length, token).ConfigureAwait(false);
                }

                bool keepConnectionOpen = ShouldKeepConnectionOpen();
                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                if (!keepConnectionOpen)
                {
                    _OutputStream.Close();
                }

                if (_Response != null && !keepConnectionOpen) _Response.Close();

                MarkResponseCompleted();
                ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task WriteSmallResponseAsync(byte[] headers, byte[] payload, CancellationToken token)
        {
            int totalLength = headers.Length + payload.Length;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);

            try
            {
                Buffer.BlockCopy(headers, 0, buffer, 0, headers.Length);
                Buffer.BlockCopy(payload, 0, buffer, headers.Length, payload.Length);
                await _OutputStream.WriteAsync(buffer, 0, totalLength, token).ConfigureAwait(false);
                _HeadersSent = true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private byte[] GetHeaderBytes()
        {
            if (IsSimpleResponseHeaderPath())
            {
                return GetSimpleHeaderBytes();
            }

            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>(256);
            WriteBytes(writer, GetStatusLineBytes(ProtocolVersion, StatusCode));

            bool contentTypeSet = false;
            if (!String.IsNullOrEmpty(ContentType))
            {
                WriteHeader(writer, WebserverConstants.HeaderContentType, ContentType);
                contentTypeSet = true;
            }

            bool contentLengthSet = false;
            if (!ChunkedTransfer && !ServerSentEvents && ContentLength >= 0)
            {
                WriteHeaderName(writer, WebserverConstants.HeaderContentLength);
                WriteInt64(writer, ContentLength);
                WriteCrlf(writer);
                contentLengthSet = true;
            }

            bool transferEncodingSet = false;
            if (ChunkedTransfer)
            {
                WriteHeaderName(writer, WebserverConstants.HeaderTransferEncoding);
                WriteBytes(writer, _ChunkedTransferEncodingBytes);
                WriteCrlf(writer);
                transferEncodingSet = true;
            }

            WriteBytes(writer, GetCurrentDateHeaderBytes());

            for (int i = 0; i < Headers.Count; i++)
            {
                string header = Headers.GetKey(i);
                if (String.IsNullOrEmpty(header)) continue;
                if (contentTypeSet && String.Equals(header, WebserverConstants.HeaderContentType, StringComparison.InvariantCultureIgnoreCase)) continue;
                if (contentLengthSet && String.Equals(header, WebserverConstants.HeaderContentLength, StringComparison.InvariantCultureIgnoreCase)) continue;
                if (transferEncodingSet && String.Equals(header, WebserverConstants.HeaderTransferEncoding, StringComparison.InvariantCultureIgnoreCase)) continue;
                if (String.Equals(header, WebserverConstants.HeaderDate, StringComparison.InvariantCultureIgnoreCase)) continue;

                string[] vals = Headers.GetValues(i);
                if (vals != null && vals.Length > 0)
                {
                    foreach (string val in vals)
                    {
                        WriteHeader(writer, header, val);
                    }
                }
            }

            WriteCrlf(writer);
            return writer.WrittenMemory.ToArray();
        }

        private byte[] GetSimpleHeaderBytes()
        {
            byte[] templatePrefix = GetSimpleHeaderTemplatePrefix();
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>(templatePrefix.Length + 96);
            WriteBytes(writer, templatePrefix);
            WriteHeaderName(writer, WebserverConstants.HeaderContentLength);
            WriteInt64(writer, ContentLength);
            WriteCrlf(writer);
            WriteBytes(writer, GetCurrentDateHeaderBytes());
            WriteCrlf(writer);
            return writer.WrittenMemory.ToArray();
        }

        private byte[] GetSimpleHeaderTemplatePrefix()
        {
            string protocolVersion = ProtocolVersion ?? String.Empty;
            int statusCode = StatusCode;
            string contentType = ContentType ?? String.Empty;
            string cacheKey = protocolVersion + "|" + statusCode.ToString() + "|" + contentType;
            int cacheLimit = GetResponseHeaderTemplateCacheLimit();
            if (cacheLimit < 1)
            {
                return BuildSimpleHeaderTemplatePrefix(protocolVersion, statusCode, contentType);
            }

            if (_SimpleHeaderTemplateCache.TryGetValue(cacheKey, out byte[] cachedTemplatePrefix))
            {
                return cachedTemplatePrefix;
            }

            TrimBoundedCache(_SimpleHeaderTemplateCache, _SimpleHeaderTemplateCacheSync, cacheLimit);
            return _SimpleHeaderTemplateCache.GetOrAdd(cacheKey, key =>
            {
                return BuildSimpleHeaderTemplatePrefix(protocolVersion, statusCode, contentType);
            });
        }

        private byte[] GetStatusLineBytes(string protocolVersion, int statusCode)
        {
            string cacheKey = (protocolVersion ?? String.Empty) + "|" + statusCode.ToString();
            int cacheLimit = GetStatusLineCacheLimit();
            if (cacheLimit < 1)
            {
                return BuildStatusLineBytes(protocolVersion, statusCode);
            }

            if (_StatusLineCache.TryGetValue(cacheKey, out byte[] cachedStatusLine))
            {
                return cachedStatusLine;
            }

            TrimBoundedCache(_StatusLineCache, _StatusLineCacheSync, cacheLimit);
            return _StatusLineCache.GetOrAdd(cacheKey, key =>
            {
                return BuildStatusLineBytes(protocolVersion, statusCode);
            });
        }

        private byte[] BuildSimpleHeaderTemplatePrefix(string protocolVersion, int statusCode, string contentType)
        {
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>(128);
            WriteBytes(writer, GetStatusLineBytes(protocolVersion, statusCode));

            if (!String.IsNullOrEmpty(contentType))
            {
                WriteHeader(writer, WebserverConstants.HeaderContentType, contentType);
            }

            return writer.WrittenMemory.ToArray();
        }

        private byte[] BuildStatusLineBytes(string protocolVersion, int statusCode)
        {
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>(64);
            WriteAscii(writer, protocolVersion);
            WriteByte(writer, (byte)' ');
            WriteInt64(writer, statusCode);
            WriteByte(writer, (byte)' ');
            WriteAscii(writer, GetStatusDescriptionStatic(statusCode));
            WriteCrlf(writer);
            return writer.WrittenMemory.ToArray();
        }

        private int GetResponseHeaderTemplateCacheLimit()
        {
            if (_Settings?.IO?.Http1 == null) return 256;
            return _Settings.IO.Http1.ResponseHeaderTemplateCacheSize;
        }

        private int GetStatusLineCacheLimit()
        {
            if (_Settings?.IO?.Http1 == null) return 64;
            return _Settings.IO.Http1.StatusLineCacheSize;
        }

        private static void TrimBoundedCache(ConcurrentDictionary<string, byte[]> cache, object sync, int limit)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (sync == null) throw new ArgumentNullException(nameof(sync));
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            if (cache.Count < limit) return;

            lock (sync)
            {
                if (cache.Count >= limit)
                {
                    cache.Clear();
                }
            }
        }

        private static string GetStatusDescriptionStatic(int statusCode)
        {
            switch (statusCode)
            {
                case 100: return "Continue";
                case 101: return "Switching Protocols";
                case 102: return "Processing";
                case 103: return "Early Hints";
                case 200: return "OK";
                case 201: return "Created";
                case 202: return "Accepted";
                case 203: return "Non-Authoritative Information";
                case 204: return "No Content";
                case 205: return "Reset Content";
                case 206: return "Partial Content";
                case 207: return "Multi-Status";
                case 208: return "Already Reported";
                case 226: return "IM Used";
                case 300: return "Multiple Choices";
                case 301: return "Moved Permanently";
                case 302: return "Moved Temporarily";
                case 303: return "See Other";
                case 304: return "Not Modified";
                case 305: return "Use Proxy";
                case 306: return "Switch Proxy";
                case 307: return "Temporary Redirect";
                case 308: return "Permanent Redirect";
                case 400: return "Bad Request";
                case 401: return "Unauthorized";
                case 402: return "Payment Required";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 406: return "Not Acceptable";
                case 407: return "Proxy Authentication Required";
                case 408: return "Request Timeout";
                case 409: return "Conflict";
                case 410: return "Gone";
                case 411: return "Length Required";
                case 412: return "Precondition Failed";
                case 413: return "Payload too Large";
                case 414: return "URI Too Long";
                case 415: return "Unsupported Media Type";
                case 416: return "Range Not Satisfiable";
                case 417: return "Expectation Failed";
                case 418: return "I'm a teapot";
                case 421: return "Misdirected Request";
                case 422: return "Unprocessable Content";
                case 423: return "Locked";
                case 424: return "Failed Dependency";
                case 425: return "Too Early";
                case 426: return "Upgrade Required";
                case 428: return "Precondition Required";
                case 429: return "Too Many Requests";
                case 431: return "Request Header Fields Too Large";
                case 451: return "Unavailable For Legal Reasons";
                case 500: return "Internal Server Error";
                case 501: return "Not Implemented";
                case 502: return "Bad Gateway";
                case 503: return "Service Unavailable";
                case 504: return "Gateway Timeout";
                case 505: return "HTTP Version Not Supported";
                case 506: return "Variant Also Negotiates";
                case 507: return "Insufficient Storage";
                case 508: return "Loop Detected";
                case 510: return "Not Extended";
                case 511: return "Network Authentication Required";
                default: return "Unknown";
            }
        }

        private static void WriteAscii(IBufferWriter<byte> writer, string value)
        {
            if (String.IsNullOrEmpty(value)) return;

            int byteCount = Encoding.ASCII.GetByteCount(value);
            Span<byte> span = writer.GetSpan(byteCount);
            Encoding.ASCII.GetBytes(value, span);
            writer.Advance(byteCount);
        }

        private static void WriteBytes(IBufferWriter<byte> writer, byte[] value)
        {
            if (value == null || value.Length < 1) return;

            Span<byte> span = writer.GetSpan(value.Length);
            value.AsSpan().CopyTo(span);
            writer.Advance(value.Length);
        }

        private static void WriteByte(IBufferWriter<byte> writer, byte value)
        {
            Span<byte> span = writer.GetSpan(1);
            span[0] = value;
            writer.Advance(1);
        }

        private static void WriteCrlf(IBufferWriter<byte> writer)
        {
            WriteBytes(writer, _CrlfBytes);
        }

        private static void WriteHeader(IBufferWriter<byte> writer, string key, string value)
        {
            WriteHeaderName(writer, key);
            WriteAscii(writer, value);
            WriteCrlf(writer);
        }

        private static void WriteHeaderName(IBufferWriter<byte> writer, string key)
        {
            WriteBytes(writer, GetHeaderNameBytes(key));
        }

        private static void WriteInt64(IBufferWriter<byte> writer, long value)
        {
            Span<byte> span = writer.GetSpan(32);

            if (!Utf8Formatter.TryFormat(value, span, out int bytesWritten))
            {
                throw new InvalidOperationException("Unable to format integer value.");
            }

            writer.Advance(bytesWritten);
        }


        private static string GetCurrentDateHeaderValue()
        {
            DateTime utcNow = DateTime.UtcNow;
            long currentSecond = utcNow.Ticks / TimeSpan.TicksPerSecond;

            if (_CachedDateHeaderSecond == currentSecond && !String.IsNullOrEmpty(_CachedDateHeaderValue))
            {
                return _CachedDateHeaderValue;
            }

            lock (_DateHeaderSync)
            {
                if (_CachedDateHeaderSecond != currentSecond || String.IsNullOrEmpty(_CachedDateHeaderValue))
                {
                    _CachedDateHeaderValue = utcNow.ToString(WebserverConstants.HeaderDateValueFormat);
                    _CachedDateHeaderBytes = null;
                    _CachedDateHeaderSecond = currentSecond;
                }

                return _CachedDateHeaderValue;
            }
        }

        private static byte[] GetCurrentDateHeaderBytes()
        {
            DateTime utcNow = DateTime.UtcNow;
            long currentSecond = utcNow.Ticks / TimeSpan.TicksPerSecond;

            if (_CachedDateHeaderSecond == currentSecond && _CachedDateHeaderBytes != null)
            {
                return _CachedDateHeaderBytes;
            }

            lock (_DateHeaderSync)
            {
                if (_CachedDateHeaderSecond != currentSecond || _CachedDateHeaderBytes == null)
                {
                    string currentDateHeaderValue = utcNow.ToString(WebserverConstants.HeaderDateValueFormat);
                    _CachedDateHeaderValue = currentDateHeaderValue;
                    _CachedDateHeaderSecond = currentSecond;
                    _CachedDateHeaderBytes = Encoding.ASCII.GetBytes(WebserverConstants.HeaderDate + ": " + currentDateHeaderValue + "\r\n");
                }

                return _CachedDateHeaderBytes;
            }
        }

        private static byte[] GetHeaderNameBytes(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (String.Equals(key, WebserverConstants.HeaderContentType, StringComparison.InvariantCultureIgnoreCase)) return _HeaderContentTypeBytes;
            if (String.Equals(key, WebserverConstants.HeaderContentLength, StringComparison.InvariantCultureIgnoreCase)) return _HeaderContentLengthBytes;
            if (String.Equals(key, WebserverConstants.HeaderTransferEncoding, StringComparison.InvariantCultureIgnoreCase)) return _HeaderTransferEncodingBytes;
            if (String.Equals(key, WebserverConstants.HeaderConnection, StringComparison.InvariantCultureIgnoreCase)) return _HeaderConnectionBytes;
            if (String.Equals(key, WebserverConstants.HeaderCacheControl, StringComparison.InvariantCultureIgnoreCase)) return _HeaderCacheControlBytes;
            if (String.Equals(key, WebserverConstants.HeaderAltSvc, StringComparison.InvariantCultureIgnoreCase)) return _HeaderAltSvcBytes;
            if (String.Equals(key, WebserverConstants.HeaderDate, StringComparison.InvariantCultureIgnoreCase)) return _HeaderDateBytes;

            return Encoding.ASCII.GetBytes(key + ": ");
        }

        private void SetCachedResponseData(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            _DataAsBytes = payload;
            _DataAsString = null;

            if (_Data != null)
            {
                try { _Data.Dispose(); } catch { }
            }

            _Data = null;
        }

        private void SetDefaultHeaders()
        {
            bool hasConnectionHeader = false;
            bool hasContentTypeHeader = false;
            bool hasCacheControlHeader = false;
            bool hasTransferEncodingHeader = false;
            bool hasAltSvcHeader = false;

            if (Headers != null && Headers.Count > 0)
            {
                string[] keys = Headers.AllKeys;
                if (keys != null)
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        string key = keys[i];
                        if (String.IsNullOrEmpty(key)) continue;

                        if (!hasConnectionHeader
                            && String.Equals(key, WebserverConstants.HeaderConnection, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasConnectionHeader = true;
                            continue;
                        }

                        if (!hasContentTypeHeader
                            && String.Equals(key, WebserverConstants.HeaderContentType, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasContentTypeHeader = true;
                            continue;
                        }

                        if (!hasCacheControlHeader
                            && String.Equals(key, WebserverConstants.HeaderCacheControl, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasCacheControlHeader = true;
                            continue;
                        }

                        if (!hasTransferEncodingHeader
                            && String.Equals(key, WebserverConstants.HeaderTransferEncoding, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasTransferEncodingHeader = true;
                            continue;
                        }

                        if (!hasAltSvcHeader
                            && String.Equals(key, WebserverConstants.HeaderAltSvc, StringComparison.InvariantCultureIgnoreCase))
                        {
                            hasAltSvcHeader = true;
                        }
                    }
                }
            }

            if (ChunkedTransfer)
            {
                ProtocolVersion = "HTTP/1.1";
                if (!hasTransferEncodingHeader)
                {
                    Headers.Add(WebserverConstants.HeaderTransferEncoding, "chunked");
                    hasTransferEncodingHeader = true;
                }
            }
            else if (ServerSentEvents)
            {
                if (!hasContentTypeHeader)
                {
                    Headers.Add(WebserverConstants.HeaderContentType, "text/event-stream; charset=utf-8");
                    hasContentTypeHeader = true;
                }

                if (!hasCacheControlHeader)
                {
                    Headers.Add(WebserverConstants.HeaderCacheControl, "no-cache");
                    hasCacheControlHeader = true;
                }

                if (!hasConnectionHeader)
                {
                    Headers.Add(WebserverConstants.HeaderConnection, "keep-alive");
                    hasConnectionHeader = true;
                }
            }
            else if (!hasConnectionHeader)
            {
                Headers.Add(WebserverConstants.HeaderConnection, ShouldKeepConnectionOpen() ? "keep-alive" : "close");
                hasConnectionHeader = true;
            }

            if (_Settings.Headers.DefaultHeaders != null)
            {
                foreach (KeyValuePair<string, string> defaultHeader in _Settings.Headers.DefaultHeaders)
                {
                    if (!HasHeader(defaultHeader.Key))
                    {
                        Headers.Add(defaultHeader.Key, defaultHeader.Value);
                    }
                }
            }

            string altSvcHeaderValue = AltSvcHeaderBuilder.Build(_Settings);
            if (!String.IsNullOrEmpty(altSvcHeaderValue) && !hasAltSvcHeader)
            {
                Headers.Add(WebserverConstants.HeaderAltSvc, altSvcHeaderValue);
            }
        }

        private bool HasHeader(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Headers == null) return false;
            if (Headers.Get(key) != null) return true;

            string[] keys = Headers.AllKeys;
            if (keys == null || keys.Length < 1) return false;

            for (int i = 0; i < keys.Length; i++)
            {
                if (!String.IsNullOrEmpty(keys[i])
                    && String.Equals(keys[i], key, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSimpleResponseHeaderPath()
        {
            if (ServerSentEvents) return false;
            if (ChunkedTransfer) return false;
            if (ContentLength < 0) return false;
            if (Headers != null && Headers.Count > 0) return false;
            if (_Settings.Headers.DefaultHeaders != null && _Settings.Headers.DefaultHeaders.Count > 0) return false;

            string altSvcHeaderValue = AltSvcHeaderBuilder.Build(_Settings);
            if (!String.IsNullOrEmpty(altSvcHeaderValue)) return false;

            return true;
        }

        private bool ShouldKeepConnectionOpen()
        {
            if (_Response != null) return false;
            if (ServerSentEvents) return false;
            if (!_Settings.IO.EnableKeepAlive) return false;
            if (_Request == null || !_Request.Keepalive) return false;

            string connectionHeader = Headers?.Get(WebserverConstants.HeaderConnection);
            if (!String.IsNullOrEmpty(connectionHeader))
            {
                if (connectionHeader.IndexOf("close", StringComparison.InvariantCultureIgnoreCase) >= 0) return false;
                if (connectionHeader.IndexOf("keep-alive", StringComparison.InvariantCultureIgnoreCase) >= 0) return true;
            }

            HttpRequest request = _Request as HttpRequest;
            if (request == null) return false;

            if (_DirectRequestBodyPassthrough) return true;
            return request.IsRequestBodyComplete;
        }

        private bool IsDirectRequestBodyPassthrough(Stream stream, long contentLength)
        {
            if (stream == null) return false;
            if (contentLength < 1) return false;

            HttpRequest request = _Request as HttpRequest;
            if (request == null) return false;
            if (!ReferenceEquals(stream, request.Data)) return false;
            if (request.ChunkedTransfer) return false;
            if (request.ContentLength < 1) return false;
            if (contentLength != request.ContentLength) return false;
            return true;
        }

        #endregion
    }
}
