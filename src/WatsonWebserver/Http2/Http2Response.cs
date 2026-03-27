namespace WatsonWebserver.Http2
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;

    /// <summary>
    /// HTTP/2 response.
    /// </summary>
    public class Http2Response : HttpResponseBase
    {
        #region Public-Members

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

        #endregion

        #region Private-Members

        private readonly HttpRequestBase _Request;
        private readonly WebserverSettings _Settings;
        private readonly Http2ConnectionWriter _Writer;
        private readonly Http2StreamStateMachine _StateMachine;
        private readonly Func<int, int, CancellationToken, Task<int>> _ReserveSendWindowAsync;
        private readonly int _StreamIdentifier;
        private readonly int _PeerMaxFrameSize;
        private MemoryStream _Data = null;
        private byte[] _DataAsBytes = null;
        private bool _HeadersSent = false;
        private static readonly object _DateHeaderSync = new object();
        private static long _CachedDateHeaderSecond = -1;
        private static string _CachedDateHeaderValue = null;
        private static readonly object _SimpleHeaderBlockSync = new object();
        private static long _CachedSimpleHeaderBlockSecond = -1;
        private static int _CachedSimpleHeaderBlockStatusCode = 0;
        private static long _CachedSimpleHeaderBlockContentLength = -1;
        private static string _CachedSimpleHeaderBlockContentType = null;
        private static byte[] _CachedSimpleHeaderBlockBytes = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the response.
        /// </summary>
        /// <param name="request">Associated request.</param>
        /// <param name="settings">Server settings.</param>
        /// <param name="writer">HTTP/2 connection writer.</param>
        /// <param name="stateMachine">Stream state machine.</param>
        /// <param name="reserveSendWindowAsync">Flow-control reservation callback.</param>
        /// <param name="streamIdentifier">HTTP/2 stream identifier.</param>
        /// <param name="peerMaxFrameSize">Peer-advertised maximum frame size.</param>
        public Http2Response(
            HttpRequestBase request,
            WebserverSettings settings,
            Http2ConnectionWriter writer,
            Http2StreamStateMachine stateMachine,
            Func<int, int, CancellationToken, Task<int>> reserveSendWindowAsync,
            int streamIdentifier,
            int peerMaxFrameSize)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            if (reserveSendWindowAsync == null) throw new ArgumentNullException(nameof(reserveSendWindowAsync));
            if (streamIdentifier < 1) throw new ArgumentOutOfRangeException(nameof(streamIdentifier));
            if (peerMaxFrameSize < Http2Constants.MinMaxFrameSize || peerMaxFrameSize > Http2Constants.MaxMaxFrameSize) throw new ArgumentOutOfRangeException(nameof(peerMaxFrameSize));

            _Request = request;
            _Settings = settings;
            _Writer = writer;
            _StateMachine = stateMachine;
            _ReserveSendWindowAsync = reserveSendWindowAsync;
            _StreamIdentifier = streamIdentifier;
            _PeerMaxFrameSize = peerMaxFrameSize;

            Protocol = HttpProtocol.Http2;
            ProtocolVersion = "HTTP/2";
        }

        #endregion

        #region Public-Methods

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
            }

            await WriteDataFramesAsync(payload, isFinal && !sendTrailers, token).ConfigureAwait(false);

            if (sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
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
            await WriteDataFramesAsync(payload, isFinal && !sendTrailers, token).ConfigureAwait(false);

            if (sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }

            if (isFinal)
            {
                MarkResponseCompleted();
                ResponseSent = true;
            }

            return true;
        }

        #endregion

        #region Private-Methods

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

            if (await TrySendBatchedSimpleResponseAsync(responsePayload, endStream, sendBody, sendTrailers, token).ConfigureAwait(false))
            {
                MarkResponseCompleted();
                ResponseSent = true;
                return true;
            }

            await WriteHeadersAsync(!sendBody && endStream && !sendTrailers, token).ConfigureAwait(false);

            if (sendBody)
            {
                await WriteDataFramesAsync(responsePayload, endStream && !sendTrailers, token).ConfigureAwait(false);
            }

            if (sendTrailers)
            {
                await WriteTrailersAsync(token).ConfigureAwait(false);
            }

            MarkResponseCompleted();
            ResponseSent = true;
            return true;
        }

        private async Task<bool> TrySendBatchedSimpleResponseAsync(byte[] payload, bool endStream, bool sendBody, bool sendTrailers, CancellationToken token)
        {
            if (!endStream) return false;
            if (sendTrailers) return false;
            if (_HeadersSent) return false;
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (sendBody && payload.Length > _PeerMaxFrameSize) return false;

            byte[] encodedHeaders = BuildEncodedResponseHeaders();
            List<Http2RawFrame> headerFrames = BuildHeaderFrames(encodedHeaders, !sendBody);
            if (headerFrames.Count != 1) return false;

            List<Http2RawFrame> frames = new List<Http2RawFrame>(sendBody ? 2 : 1);
            frames.Add(headerFrames[0]);

            _StateMachine.SendHeaders(!sendBody);

            if (sendBody)
            {
                Http2RawFrame dataFrame = new Http2RawFrame(
                    new Http2FrameHeader
                    {
                        Length = payload.Length,
                        Type = Http2FrameType.Data,
                        Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                        StreamIdentifier = _StreamIdentifier
                    },
                    payload);

                frames.Add(dataFrame);
                _StateMachine.SendData(true);
            }

            MarkResponseStarted();
            await _Writer.WriteFramesAsync(frames, token).ConfigureAwait(false);
            _HeadersSent = true;
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

            MarkResponseCompleted();
            ResponseSent = true;
            return true;
        }

        private async Task WriteTrailersAsync(CancellationToken token)
        {
            List<HpackHeaderField> trailerHeaders = BuildTrailerHeaders();
            byte[] encodedHeaders = HpackCodec.Encode(trailerHeaders);
            List<Http2RawFrame> trailerFrames = BuildHeaderFrames(encodedHeaders, true);

            _StateMachine.SendHeaders(true);
            for (int i = 0; i < trailerFrames.Count; i++)
            {
                await _Writer.WriteFrameAsync(trailerFrames[i], token).ConfigureAwait(false);
            }
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

        private async Task WriteHeadersAsync(bool endStream, CancellationToken token)
        {
            if (_HeadersSent) return;

            byte[] encodedHeaders = BuildEncodedResponseHeaders();
            List<Http2RawFrame> headerFrames = BuildHeaderFrames(encodedHeaders, endStream);

            _StateMachine.SendHeaders(endStream);
            MarkResponseStarted();
            for (int i = 0; i < headerFrames.Count; i++)
            {
                await _Writer.WriteFrameAsync(headerFrames[i], token).ConfigureAwait(false);
            }
            _HeadersSent = true;
        }

        private byte[] BuildEncodedResponseHeaders()
        {
            if (IsSimpleResponseHeaderPath())
            {
                return GetEncodedSimpleResponseHeaders();
            }

            List<HpackHeaderField> responseHeaders = BuildResponseHeaders();
            return HpackCodec.Encode(responseHeaders);
        }

        private async Task WriteDataFramesAsync(byte[] payload, bool endStream, CancellationToken token)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            int maxFrameSize = _PeerMaxFrameSize;
            int offset = 0;
            List<Http2FrameWriteSegment> segments = null;

            if (payload.Length < 1)
            {
                if (endStream)
                {
                    Http2RawFrame emptyDataFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = 0,
                            Type = Http2FrameType.Data,
                            Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                            StreamIdentifier = _StreamIdentifier
                        },
                        Array.Empty<byte>());

                    _StateMachine.SendData(true);
                    await _Writer.WriteFrameAsync(emptyDataFrame, token).ConfigureAwait(false);
                }

                return;
            }

            while (offset < payload.Length)
            {
                int desiredBytes = Math.Min(maxFrameSize, payload.Length - offset);
                int bytesToSend = await _ReserveSendWindowAsync(_StreamIdentifier, desiredBytes, token).ConfigureAwait(false);
                if (bytesToSend < 1)
                {
                    throw new IOException("Unable to reserve HTTP/2 flow-control window for response data.");
                }

                bool finalFrame = (offset + bytesToSend) >= payload.Length;
                bool currentEndStream = endStream && finalFrame;
                Http2FrameHeader dataHeader = new Http2FrameHeader
                {
                    Length = bytesToSend,
                    Type = Http2FrameType.Data,
                    Flags = currentEndStream ? (byte)Http2FrameFlags.EndStreamOrAck : (byte)Http2FrameFlags.None,
                    StreamIdentifier = _StreamIdentifier
                };

                _StateMachine.SendData(currentEndStream);
                if (segments == null) segments = new List<Http2FrameWriteSegment>();
                segments.Add(new Http2FrameWriteSegment
                {
                    Header = dataHeader,
                    Payload = payload,
                    Offset = offset,
                    Count = bytesToSend
                });
                offset += bytesToSend;
            }

            if (segments != null && segments.Count > 0)
            {
                await _Writer.WriteFrameSegmentsAsync(segments, token).ConfigureAwait(false);
            }
        }

        private async Task WriteDataFramesFromStreamAsync(Stream stream, long contentLength, bool endStream, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));

            int bufferSize = Math.Min(_PeerMaxFrameSize, 65536);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            long bytesRemaining = contentLength;
            long totalBytesSent = 0;

            try
            {
                while (bytesRemaining > 0)
                {
                    int desiredBytes = (int)Math.Min(buffer.Length, bytesRemaining);
                    int bytesRead = await stream.ReadAsync(buffer, 0, desiredBytes, token).ConfigureAwait(false);
                    if (bytesRead < 1)
                    {
                        throw new IOException("Unexpected end of stream while reading HTTP/2 response payload.");
                    }

                    _DataAsBytes = null;
                    _Data.Write(buffer, 0, bytesRead);

                    int offset = 0;
                    List<Http2FrameWriteSegment> segments = null;
                    while (offset < bytesRead)
                    {
                        int desiredFrameBytes = Math.Min(_PeerMaxFrameSize, bytesRead - offset);
                        int bytesToSend = await _ReserveSendWindowAsync(_StreamIdentifier, desiredFrameBytes, token).ConfigureAwait(false);
                        if (bytesToSend < 1)
                        {
                            throw new IOException("Unable to reserve HTTP/2 flow-control window for response data.");
                        }

                        bool currentEndStream = endStream && (totalBytesSent + bytesToSend) >= contentLength;
                        Http2FrameHeader dataHeader = new Http2FrameHeader
                        {
                            Length = bytesToSend,
                            Type = Http2FrameType.Data,
                            Flags = currentEndStream ? (byte)Http2FrameFlags.EndStreamOrAck : (byte)0,
                            StreamIdentifier = _StreamIdentifier
                        };

                        _StateMachine.SendData(currentEndStream);
                        if (segments == null) segments = new List<Http2FrameWriteSegment>();
                        segments.Add(new Http2FrameWriteSegment
                        {
                            Header = dataHeader,
                            Payload = buffer,
                            Offset = offset,
                            Count = bytesToSend
                        });
                        offset += bytesToSend;
                        totalBytesSent += bytesToSend;
                    }

                    if (segments != null && segments.Count > 0)
                    {
                        await _Writer.WriteFrameSegmentsAsync(segments, token).ConfigureAwait(false);
                    }

                    bytesRemaining -= bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private List<Http2RawFrame> BuildHeaderFrames(byte[] encodedHeaders, bool endStream)
        {
            if (encodedHeaders == null) throw new ArgumentNullException(nameof(encodedHeaders));

            List<Http2RawFrame> frames = new List<Http2RawFrame>();
            int maxFrameSize = _PeerMaxFrameSize;
            int offset = 0;
            bool firstFrame = true;

            if (encodedHeaders.Length < 1)
            {
                frames.Add(
                    Http2RawFrame.CreateOwned(
                        new Http2FrameHeader
                        {
                            Length = 0,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (endStream ? (byte)Http2FrameFlags.EndStreamOrAck : 0)),
                            StreamIdentifier = _StreamIdentifier
                        },
                        Array.Empty<byte>()));
                return frames;
            }

            if (encodedHeaders.Length <= maxFrameSize)
            {
                frames.Add(
                    Http2RawFrame.CreateOwned(
                        new Http2FrameHeader
                        {
                            Length = encodedHeaders.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (endStream ? (byte)Http2FrameFlags.EndStreamOrAck : 0)),
                            StreamIdentifier = _StreamIdentifier
                        },
                        encodedHeaders));
                return frames;
            }

            while (offset < encodedHeaders.Length)
            {
                int bytesToSend = Math.Min(maxFrameSize, encodedHeaders.Length - offset);
                byte[] fragment = new byte[bytesToSend];
                Buffer.BlockCopy(encodedHeaders, offset, fragment, 0, bytesToSend);

                bool finalFragment = (offset + bytesToSend) >= encodedHeaders.Length;
                Http2FrameType frameType = firstFrame ? Http2FrameType.Headers : Http2FrameType.Continuation;
                byte flags = 0;
                if (finalFragment) flags |= (byte)Http2FrameFlags.EndHeaders;
                if (firstFrame && endStream) flags |= (byte)Http2FrameFlags.EndStreamOrAck;

                frames.Add(
                    Http2RawFrame.CreateOwned(
                        new Http2FrameHeader
                        {
                            Length = fragment.Length,
                            Type = frameType,
                            Flags = flags,
                            StreamIdentifier = _StreamIdentifier
                        },
                        fragment));

                offset += bytesToSend;
                firstFrame = false;
            }

            return frames;
        }

        private List<HpackHeaderField> BuildResponseHeaders()
        {
            if (IsSimpleResponseHeaderPath())
            {
                return BuildSimpleResponseHeaders();
            }

            List<HpackHeaderField> headers = new List<HpackHeaderField>();
            HashSet<string> emittedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            headers.Add(new HpackHeaderField { Name = ":status", Value = StatusCode.ToString() });
            emittedHeaders.Add(":status");

            if (ServerSentEvents)
            {
                if (String.IsNullOrEmpty(ContentType)) ContentType = "text/event-stream; charset=utf-8";
            }

            if (!String.IsNullOrEmpty(ContentType))
            {
                headers.Add(new HpackHeaderField { Name = "content-type", Value = ContentType });
                emittedHeaders.Add("content-type");
            }

            if (!ServerSentEvents)
            {
                headers.Add(new HpackHeaderField { Name = "content-length", Value = ContentLength.ToString() });
                emittedHeaders.Add("content-length");
            }

            if (ServerSentEvents && !ContainsHeader(Headers, "cache-control") && !emittedHeaders.Contains("cache-control"))
            {
                headers.Add(new HpackHeaderField { Name = "cache-control", Value = "no-cache" });
                emittedHeaders.Add("cache-control");
            }

            string altSvcHeaderValue = AltSvcHeaderBuilder.Build(_Settings);
            if (!String.IsNullOrEmpty(altSvcHeaderValue) && !ContainsHeader(Headers, "alt-svc") && !emittedHeaders.Contains("alt-svc"))
            {
                headers.Add(new HpackHeaderField { Name = "alt-svc", Value = altSvcHeaderValue });
                emittedHeaders.Add("alt-svc");
            }

            headers.Add(new HpackHeaderField { Name = "date", Value = GetCurrentDateHeaderValue() });
            emittedHeaders.Add("date");
            AddConfiguredHeaders(headers, Headers, false, emittedHeaders);

            if (_Settings.Headers.DefaultHeaders != null)
            {
                AddConfiguredHeaders(headers, _Settings.Headers.DefaultHeaders, false, emittedHeaders);
            }

            return headers;
        }

        private List<HpackHeaderField> BuildSimpleResponseHeaders()
        {
            List<HpackHeaderField> headers = new List<HpackHeaderField>(4);
            headers.Add(new HpackHeaderField { Name = ":status", Value = StatusCode.ToString() });

            if (!String.IsNullOrEmpty(ContentType))
            {
                headers.Add(new HpackHeaderField { Name = "content-type", Value = ContentType });
            }

            headers.Add(new HpackHeaderField { Name = "content-length", Value = ContentLength.ToString() });
            headers.Add(new HpackHeaderField { Name = "date", Value = GetCurrentDateHeaderValue() });
            return headers;
        }

        private bool IsSimpleResponseHeaderPath()
        {
            if (ServerSentEvents) return false;
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
                    List<HpackHeaderField> headers = BuildSimpleResponseHeaders();
                    _CachedSimpleHeaderBlockBytes = HpackCodec.Encode(headers);
                    _CachedSimpleHeaderBlockSecond = currentSecond;
                    _CachedSimpleHeaderBlockStatusCode = statusCode;
                    _CachedSimpleHeaderBlockContentLength = contentLength;
                    _CachedSimpleHeaderBlockContentType = contentType;
                }

                return _CachedSimpleHeaderBlockBytes;
            }
        }

        private List<HpackHeaderField> BuildTrailerHeaders()
        {
            List<HpackHeaderField> headers = new List<HpackHeaderField>();
            HashSet<string> emittedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddConfiguredHeaders(headers, Trailers, true, emittedHeaders);
            return headers;
        }

        private void AddConfiguredHeaders(List<HpackHeaderField> headers, NameValueCollection source, bool trailersOnly, HashSet<string> emittedHeaders)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (emittedHeaders == null) throw new ArgumentNullException(nameof(emittedHeaders));
            if (source == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                string name = source.GetKey(i);
                if (String.IsNullOrEmpty(name)) continue;

                string lowerName = NormalizeHeaderName(name);
                if (IsDisallowedHttp2Header(lowerName, trailersOnly)) continue;
                if (emittedHeaders.Contains(lowerName)) continue;

                string[] values = source.GetValues(i);
                if (values == null || values.Length < 1) continue;

                for (int j = 0; j < values.Length; j++)
                {
                    if (values[j] == null) continue;
                    headers.Add(new HpackHeaderField { Name = lowerName, Value = values[j] });
                }

                emittedHeaders.Add(lowerName);
            }
        }

        private void AddConfiguredHeaders(List<HpackHeaderField> headers, Dictionary<string, string> source, bool trailersOnly, HashSet<string> emittedHeaders)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (emittedHeaders == null) throw new ArgumentNullException(nameof(emittedHeaders));
            if (source == null) return;

            foreach (KeyValuePair<string, string> entry in source)
            {
                string name = entry.Key;
                if (String.IsNullOrEmpty(name)) continue;

                string lowerName = NormalizeHeaderName(name);
                if (IsDisallowedHttp2Header(lowerName, trailersOnly)) continue;
                if (emittedHeaders.Contains(lowerName)) continue;
                if (entry.Value == null) continue;

                headers.Add(new HpackHeaderField { Name = lowerName, Value = entry.Value });
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

        private bool IsDisallowedHttp2Header(string lowerName, bool trailersOnly)
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

        #endregion
    }
}

