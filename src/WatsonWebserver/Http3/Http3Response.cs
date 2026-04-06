#if NET8_0_OR_GREATER
namespace WatsonWebserver.Http3
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net.Quic;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// HTTP/3 response.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public class Http3Response : HttpResponseBase
    {
        private const int ContiguousFrameWriteLimit = 16 * 1024;

        /// <summary>
        /// Response body as string.
        /// </summary>
        [JsonIgnore]
        public override string DataAsString
        {
            get
            {
                if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                if (_Data == null || _Data.Length < 1) return String.Empty;
                _DataAsBytes = _Data.ToArray();
                return Encoding.UTF8.GetString(_DataAsBytes);
            }
        }

        /// <summary>
        /// Response body as bytes.
        /// </summary>
        [JsonIgnore]
        public override byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (_Data == null) return Array.Empty<byte>();
                _DataAsBytes = _Data.ToArray();
                return _DataAsBytes;
            }
        }

        /// <summary>
        /// Response body stream.
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

        private readonly HttpRequestBase _Request;
        private readonly WebserverSettings _Settings;
        private readonly QuicStream _Stream;
        private MemoryStream _Data = null;
        private readonly SemaphoreSlim _WriteLock = new SemaphoreSlim(1, 1);
        private byte[] _DataAsBytes = null;
        private bool _HeadersSent = false;
        private bool _WritesCompleted = false;
        private static readonly object _DateHeaderSync = new object();
        private static long _CachedDateHeaderSecond = -1;
        private static string _CachedDateHeaderValue = null;
        private static readonly object _SimpleHeaderBlockSync = new object();
        private static long _CachedSimpleHeaderBlockSecond = -1;
        private static int _CachedSimpleHeaderBlockStatusCode = 0;
        private static long _CachedSimpleHeaderBlockContentLength = -1;
        private static string _CachedSimpleHeaderBlockContentType = null;
        private static byte[] _CachedSimpleHeaderBlockBytes = null;

        /// <summary>
        /// Instantiate the response.
        /// </summary>
        /// <param name="request">Associated request.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="stream">HTTP/3 request stream.</param>
        public Http3Response(HttpRequestBase request, WebserverSettings settings, QuicStream stream)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _Request = request;
            _Settings = settings;
            _Stream = stream;
            Protocol = HttpProtocol.Http3;
            ProtocolVersion = "HTTP/3";
        }

        /// <inheritdoc />
        public override Task<bool> Send(CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer semantics. Use SendChunk() instead.");
            return SendInternalAsync(Array.Empty<byte>(), true, token);
        }

        /// <inheritdoc />
        public override Task<bool> Send(long contentLength, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer semantics. Use SendChunk() instead.");
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            ContentLength = contentLength;
            return SendInternalAsync(Array.Empty<byte>(), true, token);
        }

        /// <inheritdoc />
        public override Task<bool> Send(string data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer semantics. Use SendChunk() instead.");
            byte[] payload = String.IsNullOrEmpty(data) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(data);
            return SendInternalAsync(payload, true, token);
        }

        /// <inheritdoc />
        public override Task<bool> Send(byte[] data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer semantics. Use SendChunk() instead.");
            byte[] payload = data ?? Array.Empty<byte>();
            return SendInternalAsync(payload, true, token);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer semantics. Use SendChunk() instead.");
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            if (CanStreamPayload(stream, contentLength))
            {
                return await SendStreamInternalAsync(contentLength, stream, token).ConfigureAwait(false);
            }

            byte[] payload = new byte[contentLength];
            int offset = 0;

            while (offset < payload.Length)
            {
                int bytesRead = await stream.ReadAsync(payload, offset, payload.Length - offset, token).ConfigureAwait(false);
                if (bytesRead < 1) break;
                offset += bytesRead;
            }

            if (offset != payload.Length)
            {
                byte[] truncatedPayload = new byte[offset];
                if (offset > 0) Buffer.BlockCopy(payload, 0, truncatedPayload, 0, offset);
                payload = truncatedPayload;
            }

            return await SendInternalAsync(payload, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> SendChunk(byte[] chunk, bool isFinal, CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer semantics. Set ChunkedTransfer to true first.");

            byte[] payload = chunk ?? Array.Empty<byte>();
            bool sendTrailers = isFinal && HasTrailers();

            if (!_HeadersSent)
            {
                await WriteHeadersAsync(false, token).ConfigureAwait(false);
            }

            if (payload.Length > 0)
            {
                await AppendDataAsync(payload, token).ConfigureAwait(false);
                await WriteDataFrameAsync(payload, false, token).ConfigureAwait(false);
            }

            if (isFinal && sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }
            else if (isFinal)
            {
                await CompleteWritesAsync(token).ConfigureAwait(false);
            }

            if (isFinal)
            {
                MarkResponseCompleted();
                ResponseSent = true;
            }

            return true;
        }

        /// <inheritdoc />
        public override async Task<bool> SendEvent(ServerSentEvent sse, bool isFinal, CancellationToken token = default)
        {
            if (!ServerSentEvents) throw new IOException("Response is not configured to use server-sent events. Set ServerSentEvents to true first.");
            if (sse == null) throw new ArgumentNullException(nameof(sse));

            string eventText = sse.ToEventString();
            if (String.IsNullOrEmpty(eventText)) throw new ArgumentException("A populated server-sent event is required.", nameof(sse));

            bool sendTrailers = isFinal && HasTrailers();
            if (!_HeadersSent)
            {
                await WriteHeadersAsync(false, token).ConfigureAwait(false);
            }

            byte[] payload = Encoding.UTF8.GetBytes(eventText);
            await AppendDataAsync(payload, token).ConfigureAwait(false);
            await WriteDataFrameAsync(payload, false, token).ConfigureAwait(false);

            if (isFinal && sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }
            else if (isFinal)
            {
                await CompleteWritesAsync(token).ConfigureAwait(false);
            }

            if (isFinal)
            {
                MarkResponseCompleted();
                ResponseSent = true;
            }

            return true;
        }

        /// <summary>
        /// Dispose of response resources.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_Data != null)
                {
                    _Data.Dispose();
                    _Data = null;
                }

                _WriteLock.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool HasTrailers()
        {
            return Trailers != null && Trailers.Count > 0;
        }

        private async Task<bool> SendInternalAsync(byte[] payload, bool endStream, CancellationToken token)
        {
            byte[] responsePayload = payload ?? Array.Empty<byte>();
            bool sendBody = !HttpMethod.HEAD.Equals(_Request.Method) && responsePayload.Length > 0;
            bool sendTrailers = HasTrailers();

            ContentLength = responsePayload.Length;
            if (responsePayload.Length > 0)
            {
                SetCachedResponseData(responsePayload);
            }

            await WriteHeadersAsync(!sendBody && endStream && !sendTrailers, token).ConfigureAwait(false);

            if (sendBody)
            {
                await WriteDataFrameAsync(responsePayload, endStream && !sendTrailers, token).ConfigureAwait(false);
            }

            if (sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }

            if (!sendBody && !sendTrailers && endStream)
            {
                await CompleteWritesAsync(token).ConfigureAwait(false);
            }

            MarkResponseCompleted();
            ResponseSent = true;
            return true;
        }

        private async Task<bool> SendStreamInternalAsync(long contentLength, Stream stream, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            bool sendBody = !HttpMethod.HEAD.Equals(_Request.Method) && contentLength > 0;
            bool sendTrailers = HasTrailers();
            ContentLength = contentLength;
            EnsureBufferedResponseCapacity(contentLength);

            await WriteHeadersAsync(!sendBody && !sendTrailers, token).ConfigureAwait(false);

            if (sendBody)
            {
                await WriteDataFramesFromStreamAsync(stream, contentLength, !sendTrailers, token).ConfigureAwait(false);
            }

            if (sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }
            else if (!sendBody)
            {
                await CompleteWritesAsync(token).ConfigureAwait(false);
            }

            MarkResponseCompleted();
            ResponseSent = true;
            return true;
        }

        private async Task AppendDataAsync(byte[] payload, CancellationToken token)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (payload.Length < 1) return;

            if (_Data == null)
            {
                _Data = new MemoryStream();
            }

            _DataAsBytes = null;
            _Data.Write(payload, 0, payload.Length);
        }

        private void EnsureBufferedResponseCapacity(long contentLength)
        {
            if (contentLength < 1) return;
            if (contentLength > Int32.MaxValue) return;
            if (_Data == null)
            {
                _Data = new MemoryStream();
            }

            if (_Data.Length > 0) return;

            int desiredCapacity = (int)contentLength;
            if (_Data.Capacity < desiredCapacity)
            {
                _Data.Capacity = desiredCapacity;
            }
        }

        private void SetCachedResponseData(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            _DataAsBytes = payload;
            _Data = null;
        }

        private async Task WriteHeadersAsync(bool completeWrites, CancellationToken token)
        {
            if (_HeadersSent) return;

            byte[] encodedHeaders = BuildEncodedResponseHeaders();
            byte[] frameBytes = Http3FrameSerializer.SerializeFrame(new Http3Frame
            {
                Header = new Http3FrameHeader { Type = (long)Http3FrameType.Headers, Length = encodedHeaders.Length },
                Payload = encodedHeaders
            });
            MarkResponseStarted();
            await WriteFrameBytesAsync(frameBytes, completeWrites, token).ConfigureAwait(false);
            _HeadersSent = true;
        }

        private byte[] BuildEncodedResponseHeaders()
        {
            if (IsSimpleResponseHeaderPath())
            {
                return GetEncodedSimpleResponseHeaders();
            }

            List<Http3HeaderField> responseHeaders = BuildResponseHeaders();
            return Http3HeaderCodec.Encode(responseHeaders);
        }

        private async Task WriteDataFrameAsync(byte[] payload, bool completeWrites, CancellationToken token)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            await WriteFrameAsync((long)Http3FrameType.Data, payload, 0, payload.Length, completeWrites, token).ConfigureAwait(false);
        }

        private async Task WriteDataFramesFromStreamAsync(Stream stream, long contentLength, bool completeWrites, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));

            byte[] buffer = ArrayPool<byte>.Shared.Rent(65536);
            long bytesRemaining = contentLength;

            try
            {
                while (bytesRemaining > 0)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                    int bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, token).ConfigureAwait(false);
                    if (bytesRead < 1)
                    {
                        throw new IOException("Unexpected end of stream while reading HTTP/3 response payload.");
                    }

                    _DataAsBytes = null;
                    _Data.Write(buffer, 0, bytesRead);

                    bytesRemaining -= bytesRead;
                    await WriteFrameAsync((long)Http3FrameType.Data, buffer, 0, bytesRead, completeWrites && bytesRemaining == 0, token).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task WriteTrailersAsync(CancellationToken token)
        {
            List<Http3HeaderField> trailerHeaders = BuildTrailerHeaders();
            byte[] encodedHeaders = Http3HeaderCodec.Encode(trailerHeaders);
            byte[] frameBytes = Http3FrameSerializer.SerializeFrame(new Http3Frame
            {
                Header = new Http3FrameHeader { Type = (long)Http3FrameType.Headers, Length = encodedHeaders.Length },
                Payload = encodedHeaders
            });
            await WriteFrameBytesAsync(frameBytes, true, token).ConfigureAwait(false);
        }

        private async Task CompleteWritesAsync(CancellationToken token)
        {
            if (_WritesCompleted) return;

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_WritesCompleted) return;
                _Stream.CompleteWrites();
                _WritesCompleted = true;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        private async Task WriteFrameBytesAsync(byte[] frameBytes, bool completeWrites, CancellationToken token)
        {
            if (frameBytes == null) throw new ArgumentNullException(nameof(frameBytes));

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_WritesCompleted) throw new IOException("HTTP/3 response stream has already completed writes.");
                await _Stream.WriteAsync(frameBytes, completeWrites, token).ConfigureAwait(false);
                if (completeWrites)
                {
                    _WritesCompleted = true;
                }
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        private async Task WriteFrameAsync(long frameType, byte[] payload, int offset, int count, bool completeWrites, CancellationToken token)
        {
            if (payload == null && count > 0) throw new ArgumentNullException(nameof(payload));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (payload != null && (offset + count) > payload.Length) throw new ArgumentOutOfRangeException(nameof(count));

            byte[] frameHeaderBytes = Http3FrameSerializer.SerializeFrameHeader(frameType, count);

            await _WriteLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_WritesCompleted) throw new IOException("HTTP/3 response stream has already completed writes.");
                if ((frameHeaderBytes.Length + count) <= ContiguousFrameWriteLimit)
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(frameHeaderBytes.Length + count);
                    try
                    {
                        Buffer.BlockCopy(frameHeaderBytes, 0, buffer, 0, frameHeaderBytes.Length);
                        if (count > 0)
                        {
                            Buffer.BlockCopy(payload, offset, buffer, frameHeaderBytes.Length, count);
                        }

                        await _Stream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, frameHeaderBytes.Length + count), completeWrites, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                else
                {
                    await _Stream.WriteAsync(frameHeaderBytes, false, token).ConfigureAwait(false);
                    if (count > 0)
                    {
                        await _Stream.WriteAsync(new ReadOnlyMemory<byte>(payload, offset, count), completeWrites, token).ConfigureAwait(false);
                    }
                    else if (completeWrites)
                    {
                        _Stream.CompleteWrites();
                    }
                }

                if (completeWrites)
                {
                    _WritesCompleted = true;
                }
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        private List<Http3HeaderField> BuildResponseHeaders()
        {
            if (IsSimpleResponseHeaderPath())
            {
                return BuildSimpleResponseHeaders();
            }

            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            HashSet<string> emittedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            headers.Add(new Http3HeaderField { Name = ":status", Value = StatusCode.ToString() });
            emittedHeaders.Add(":status");

            if (ServerSentEvents)
            {
                if (String.IsNullOrEmpty(ContentType)) ContentType = "text/event-stream; charset=utf-8";
            }

            if (!String.IsNullOrEmpty(ContentType))
            {
                headers.Add(new Http3HeaderField { Name = "content-type", Value = ContentType });
                emittedHeaders.Add("content-type");
            }

            if (!ServerSentEvents && !ChunkedTransfer)
            {
                headers.Add(new Http3HeaderField { Name = "content-length", Value = ContentLength.ToString() });
                emittedHeaders.Add("content-length");
            }

            if (ServerSentEvents && !ContainsHeader(Headers, "cache-control") && !emittedHeaders.Contains("cache-control"))
            {
                headers.Add(new Http3HeaderField { Name = "cache-control", Value = "no-cache" });
                emittedHeaders.Add("cache-control");
            }

            string altSvcHeaderValue = AltSvcHeaderBuilder.Build(_Settings);
            if (!String.IsNullOrEmpty(altSvcHeaderValue) && !ContainsHeader(Headers, "alt-svc") && !emittedHeaders.Contains("alt-svc"))
            {
                headers.Add(new Http3HeaderField { Name = "alt-svc", Value = altSvcHeaderValue });
                emittedHeaders.Add("alt-svc");
            }

            headers.Add(new Http3HeaderField { Name = "date", Value = GetCurrentDateHeaderValue() });
            emittedHeaders.Add("date");
            AddConfiguredHeaders(headers, Headers, false, emittedHeaders);

            if (_Settings.Headers.DefaultHeaders != null)
            {
                AddConfiguredHeaders(headers, _Settings.Headers.DefaultHeaders, false, emittedHeaders);
            }

            return headers;
        }

        private List<Http3HeaderField> BuildSimpleResponseHeaders()
        {
            List<Http3HeaderField> headers = new List<Http3HeaderField>(4);
            headers.Add(new Http3HeaderField { Name = ":status", Value = StatusCode.ToString() });

            if (!String.IsNullOrEmpty(ContentType))
            {
                headers.Add(new Http3HeaderField { Name = "content-type", Value = ContentType });
            }

            headers.Add(new Http3HeaderField { Name = "content-length", Value = ContentLength.ToString() });
            headers.Add(new Http3HeaderField { Name = "date", Value = GetCurrentDateHeaderValue() });
            return headers;
        }

        private bool IsSimpleResponseHeaderPath()
        {
            if (ServerSentEvents) return false;
            if (ChunkedTransfer) return false;
            if (Headers != null && Headers.Count > 0) return false;
            if (_Settings.Headers.DefaultHeaders != null && _Settings.Headers.DefaultHeaders.Count > 0) return false;
            if (!String.IsNullOrEmpty(AltSvcHeaderBuilder.Build(_Settings))) return false;
            return true;
        }

        private byte[] GetEncodedSimpleResponseHeaders()
        {
            DateTime utcNow = DateTime.UtcNow;
            long currentSecond = utcNow.Ticks / TimeSpan.TicksPerSecond;
            string contentType = ContentType;
            long contentLength = ContentLength;
            int statusCode = StatusCode;

            if (_CachedSimpleHeaderBlockSecond == currentSecond
                && _CachedSimpleHeaderBlockStatusCode == statusCode
                && _CachedSimpleHeaderBlockContentLength == contentLength
                && String.Equals(_CachedSimpleHeaderBlockContentType, contentType, StringComparison.Ordinal)
                && _CachedSimpleHeaderBlockBytes != null)
            {
                return _CachedSimpleHeaderBlockBytes;
            }

            lock (_SimpleHeaderBlockSync)
            {
                if (_CachedSimpleHeaderBlockSecond != currentSecond
                    || _CachedSimpleHeaderBlockStatusCode != statusCode
                    || _CachedSimpleHeaderBlockContentLength != contentLength
                    || !String.Equals(_CachedSimpleHeaderBlockContentType, contentType, StringComparison.Ordinal)
                    || _CachedSimpleHeaderBlockBytes == null)
                {
                    List<Http3HeaderField> headers = BuildSimpleResponseHeaders();
                    _CachedSimpleHeaderBlockBytes = Http3HeaderCodec.Encode(headers);
                    _CachedSimpleHeaderBlockSecond = currentSecond;
                    _CachedSimpleHeaderBlockStatusCode = statusCode;
                    _CachedSimpleHeaderBlockContentLength = contentLength;
                    _CachedSimpleHeaderBlockContentType = contentType;
                }

                return _CachedSimpleHeaderBlockBytes;
            }
        }


        private List<Http3HeaderField> BuildTrailerHeaders()
        {
            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            HashSet<string> emittedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddConfiguredHeaders(headers, Trailers, true, emittedHeaders);
            return headers;
        }

        private void AddConfiguredHeaders(List<Http3HeaderField> headers, NameValueCollection source, bool trailersOnly, HashSet<string> emittedHeaders)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (emittedHeaders == null) throw new ArgumentNullException(nameof(emittedHeaders));
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                string name = source.GetKey(i);
                if (String.IsNullOrEmpty(name)) continue;

                string lowerName = NormalizeHeaderName(name);
                if (IsDisallowedHttp3Header(lowerName, trailersOnly)) continue;
                if (emittedHeaders.Contains(lowerName)) continue;

                string[] values = source.GetValues(i);
                if (values == null || values.Length < 1) continue;

                for (int j = 0; j < values.Length; j++)
                {
                    if (values[j] == null) continue;
                    headers.Add(new Http3HeaderField { Name = lowerName, Value = values[j] });
                }

                emittedHeaders.Add(lowerName);
            }
        }

        private void AddConfiguredHeaders(List<Http3HeaderField> headers, Dictionary<string, string> source, bool trailersOnly, HashSet<string> emittedHeaders)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (emittedHeaders == null) throw new ArgumentNullException(nameof(emittedHeaders));
            if (source == null) return;

            foreach (KeyValuePair<string, string> entry in source)
            {
                string name = entry.Key;
                if (String.IsNullOrEmpty(name)) continue;

                string lowerName = NormalizeHeaderName(name);
                if (IsDisallowedHttp3Header(lowerName, trailersOnly)) continue;
                if (emittedHeaders.Contains(lowerName)) continue;
                if (entry.Value == null) continue;

                headers.Add(new Http3HeaderField { Name = lowerName, Value = entry.Value });
                emittedHeaders.Add(lowerName);
            }
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
                    _CachedDateHeaderSecond = currentSecond;
                }

                return _CachedDateHeaderValue;
            }
        }

        private static string NormalizeHeaderName(string name)
        {
            if (String.IsNullOrEmpty(name)) return name;

            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (character >= 'A' && character <= 'Z')
                {
                    return name.ToLowerInvariant();
                }
            }

            return name;
        }

        private bool ContainsHeader(NameValueCollection headers, string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (headers == null) return false;
            return headers.Get(name) != null;
        }

        private bool CanStreamPayload(Stream stream, long contentLength)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            if (!stream.CanRead || !stream.CanSeek) return false;
            if (stream.Position < 0 || stream.Length < stream.Position) return false;
            return (stream.Length - stream.Position) >= contentLength;
        }

        private bool IsDisallowedHttp3Header(string lowerName, bool trailersOnly)
        {
            if (String.IsNullOrEmpty(lowerName)) return true;

            if (lowerName.Equals("connection", StringComparison.Ordinal)
                || lowerName.Equals("keep-alive", StringComparison.Ordinal)
                || lowerName.Equals("proxy-connection", StringComparison.Ordinal)
                || lowerName.Equals("transfer-encoding", StringComparison.Ordinal)
                || lowerName.Equals("upgrade", StringComparison.Ordinal)
                || lowerName.Equals("host", StringComparison.Ordinal)
                || lowerName.StartsWith(":", StringComparison.Ordinal))
            {
                return true;
            }

            if (trailersOnly)
            {
                return lowerName.Equals("content-length", StringComparison.Ordinal)
                    || lowerName.Equals("content-type", StringComparison.Ordinal)
                    || lowerName.Equals("trailer", StringComparison.Ordinal);
            }

            return false;
        }
    }
}
#endif
