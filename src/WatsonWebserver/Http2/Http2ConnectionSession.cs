namespace WatsonWebserver.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;

    /// <summary>
    /// Minimal HTTP/2 server connection session.
    /// </summary>
    internal class Http2ConnectionSession
    {
        /// <summary>
        /// Instantiate the session.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="events">Server events.</param>
        /// <param name="stream">Transport stream.</param>
        /// <param name="processContext">Shared request processor.</param>
        /// <param name="source">Source endpoint.</param>
        /// <param name="destination">Destination endpoint.</param>
        /// <param name="encrypted">True if the connection is encrypted.</param>
        public Http2ConnectionSession(
            WebserverSettings settings,
            WebserverEvents events,
            Stream stream,
            Func<HttpContextBase, CancellationToken, Task> processContext,
            IPEndPoint source,
            IPEndPoint destination,
            bool encrypted)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (processContext == null) throw new ArgumentNullException(nameof(processContext));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            _Settings = settings;
            _Events = events;
            _Stream = stream;
            _ProcessContext = processContext;
            _Source = source;
            _Destination = destination;
            _Encrypted = encrypted;
            _SourceIpAddress = source.Address.ToString();
            _DestinationIpAddress = destination.Address.ToString();
            _ConnectionReceiveWindow = Http2Constants.DefaultInitialWindowSize;
            _ConnectionSendWindow = Http2Constants.DefaultInitialWindowSize;
            _HpackDecoderContext = new HpackDecoderContext((int)Math.Min(Int32.MaxValue, settings.Protocols.Http2.HeaderTableSize));
        }

        /// <summary>
        /// Run the HTTP/2 connection session.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            using (CancellationTokenSource readTimeoutTokenSource = new CancellationTokenSource())
            using (CancellationTokenRegistration readTimeoutRegistration = token.Register(static state => ((CancellationTokenSource)state).Cancel(), readTimeoutTokenSource))
            try
            {
                _ReadTimeoutTokenSource = readTimeoutTokenSource;
                Http2HandshakeResult handshake = await Http2ConnectionHandshake.ReadClientHandshakeAsync(_Stream, token).ConfigureAwait(false);
                _RemoteSettings = handshake.RemoteSettings;
                _State = Http2ConnectionState.Open;

                using (Http2ConnectionWriter writer = new Http2ConnectionWriter(_Stream, true))
                {
                    _Writer = writer;
                    await writer.WriteSettingsAsync(_Settings.Protocols.Http2, token).ConfigureAwait(false);
                    await writer.WriteSettingsAcknowledgementAsync(token).ConfigureAwait(false);

                    while (_State != Http2ConnectionState.Closed && !token.IsCancellationRequested)
                    {
                        Http2RawFrame frame = await ReadNextFrameAsync(token).ConfigureAwait(false);
                        await HandleFrameAsync(writer, frame, token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                if (_Writer != null)
                {
                    try
                    {
                        await SendGoAwayAsync(_Writer, _LastRemoteStreamIdentifier, Http2ErrorCode.NoError, "HTTP/2 idle timeout elapsed.", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                }

                _State = Http2ConnectionState.Closed;
            }
            catch (EndOfStreamException)
            {
                _State = Http2ConnectionState.Closed;
            }
            catch (IOException)
            {
                _State = Http2ConnectionState.Closed;
            }
            finally
            {
                _ReadTimeoutTokenSource = null;
                _Writer = null;
                await WaitForActiveRequestsAsync().ConfigureAwait(false);
            }
        }

        private async Task<Http2RawFrame> ReadNextFrameAsync(CancellationToken token)
        {
            CancellationTokenSource readTimeoutTokenSource = _ReadTimeoutTokenSource;
            if (readTimeoutTokenSource == null)
            {
                throw new InvalidOperationException("HTTP/2 read timeout state is not initialized.");
            }

            readTimeoutTokenSource.CancelAfter(_Settings.Protocols.IdleTimeoutMs);

            try
            {
                return await Http2FrameSerializer.ReadFrameAsync(_Stream, readTimeoutTokenSource.Token).ConfigureAwait(false);
            }
            finally
            {
                if (!readTimeoutTokenSource.IsCancellationRequested)
                {
                    readTimeoutTokenSource.CancelAfter(Timeout.Infinite);
                }
            }
        }

        private readonly WebserverSettings _Settings;
        private readonly WebserverEvents _Events;
        private readonly Stream _Stream;
        private readonly Func<HttpContextBase, CancellationToken, Task> _ProcessContext;
        private readonly IPEndPoint _Source;
        private readonly IPEndPoint _Destination;
        private readonly bool _Encrypted;
        private readonly string _SourceIpAddress;
        private readonly string _DestinationIpAddress;
        private readonly object _SessionLock = new object();
        private readonly object _FlowControlLock = new object();
        private readonly Dictionary<int, Http2StreamStateMachine> _Streams = new Dictionary<int, Http2StreamStateMachine>();
        private readonly Dictionary<int, Http2PendingRequest> _PendingRequests = new Dictionary<int, Http2PendingRequest>();
        private readonly Dictionary<int, Task> _ActiveRequestTasks = new Dictionary<int, Task>();
        private readonly Dictionary<int, CancellationTokenSource> _ActiveRequestTokens = new Dictionary<int, CancellationTokenSource>();
        private readonly Dictionary<int, int> _StreamReceiveWindows = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _StreamSendWindows = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _PendingStreamReceiveWindowUpdates = new Dictionary<int, int>();
        private Http2ConnectionState _State = Http2ConnectionState.AwaitingPreface;
        private Http2Settings _RemoteSettings = new Http2Settings();
        private Http2ConnectionWriter _Writer = null;
        private TaskCompletionSource<bool> _SendWindowSignal = CreateSendWindowSignal();
        private int _LastRemoteStreamIdentifier = 0;
        private int _ConnectionReceiveWindow;
        private int _ConnectionSendWindow;
        private int _PendingConnectionReceiveWindowUpdate = 0;
        private CancellationTokenSource _ReadTimeoutTokenSource = null;
        private readonly HpackDecoderContext _HpackDecoderContext;

        private async Task HandleFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            switch (frame.Header.Type)
            {
                case Http2FrameType.Settings:
                    await HandleSettingsFrameAsync(writer, frame, token).ConfigureAwait(false);
                    break;
                case Http2FrameType.Ping:
                    await HandlePingFrameAsync(writer, frame, token).ConfigureAwait(false);
                    break;
                case Http2FrameType.Headers:
                    await HandleHeadersFrameAsync(writer, frame, token).ConfigureAwait(false);
                    break;
                case Http2FrameType.Data:
                    await HandleDataFrameAsync(writer, frame, token).ConfigureAwait(false);
                    break;
                case Http2FrameType.RstStream:
                    HandleRstStreamFrame(frame);
                    break;
                case Http2FrameType.GoAway:
                    _State = Http2ConnectionState.Closed;
                    break;
                case Http2FrameType.WindowUpdate:
                    await HandleWindowUpdateFrameAsync(writer, frame, token).ConfigureAwait(false);
                    break;
                default:
                    await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.InternalError, "Unsupported HTTP/2 frame type was received.", token).ConfigureAwait(false);
                    break;
            }
        }

        private async Task HandleSettingsFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            bool isAcknowledgement = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
            if (isAcknowledgement) return;

            _RemoteSettings = Http2FrameSerializer.ReadSettingsFrame(frame);
            await writer.WriteSettingsAcknowledgementAsync(token).ConfigureAwait(false);
        }

        private async Task HandleWindowUpdateFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            try
            {
                Http2WindowUpdateFrame windowUpdateFrame = Http2FrameSerializer.ReadWindowUpdateFrame(frame);
                bool signalWaiters = false;

                lock (_FlowControlLock)
                {
                    if (windowUpdateFrame.StreamIdentifier == 0)
                    {
                        _ConnectionSendWindow = AddFlowControlWindow(_ConnectionSendWindow, windowUpdateFrame.WindowSizeIncrement);
                        signalWaiters = true;
                    }
                    else if (_StreamSendWindows.ContainsKey(windowUpdateFrame.StreamIdentifier))
                    {
                        _StreamSendWindows[windowUpdateFrame.StreamIdentifier] = AddFlowControlWindow(_StreamSendWindows[windowUpdateFrame.StreamIdentifier], windowUpdateFrame.WindowSizeIncrement);
                        signalWaiters = true;
                    }
                }

                if (signalWaiters)
                {
                    SignalSendWindowAvailability();
                }
            }
            catch (Http2ProtocolException ex)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, ex.ErrorCode, ex.Message, token).ConfigureAwait(false);
            }
        }

        private async Task HandlePingFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            Http2PingFrame pingFrame = Http2FrameSerializer.ReadPingFrame(frame);
            if (pingFrame.Acknowledge) return;

            Http2PingFrame acknowledgement = new Http2PingFrame();
            acknowledgement.Acknowledge = true;
            acknowledgement.OpaqueData = pingFrame.OpaqueData;
            await writer.WritePingAsync(acknowledgement, token).ConfigureAwait(false);
        }

        private async Task HandleHeadersFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            if (frame.Header.StreamIdentifier < 1 || (frame.Header.StreamIdentifier % 2) == 0)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.ProtocolError, "Client-initiated request streams must use odd non-zero stream identifiers.", token).ConfigureAwait(false);
                return;
            }

            bool endStream = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
            Http2StreamStateMachine stateMachine = GetOrCreateRemoteStream(frame.Header.StreamIdentifier);

            try
            {
                byte[] headerBlock = await ReadHeaderBlockAsync(frame, token).ConfigureAwait(false);
                if (TryGetPendingRequest(frame.Header.StreamIdentifier, out Http2PendingRequest pendingRequest))
                {
                    if (!endStream)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 trailing HEADERS must terminate the request stream.");
                    }

                    NameValueCollection trailers = ParseTrailerFields(headerBlock);
                    MergeTrailers(pendingRequest.Trailers, trailers);
                    stateMachine.ReceiveHeaders(true);
                    pendingRequest = TakePendingRequest(frame.Header.StreamIdentifier);
                    await DispatchRequestAsync(writer, frame.Header.StreamIdentifier, stateMachine, pendingRequest, token).ConfigureAwait(false);
                    return;
                }

                stateMachine.ReceiveHeaders(endStream);
                if (!endStream)
                {
                    Http2PendingRequest createdPendingRequest = ParsePendingRequest(headerBlock);
                    StorePendingRequest(frame.Header.StreamIdentifier, createdPendingRequest);
                    return;
                }

                Http2PendingRequest completedRequest = ParsePendingRequest(headerBlock);
                await DispatchRequestAsync(writer, frame.Header.StreamIdentifier, stateMachine, completedRequest, token).ConfigureAwait(false);
            }
            catch (Http2ProtocolException ex)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, ex.ErrorCode, ex.Message, token).ConfigureAwait(false);
                return;
            }
            catch (Http2StreamStateException ex)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.ProtocolError, ex.Message, token).ConfigureAwait(false);
                return;
            }
        }

        private async Task HandleDataFrameAsync(Http2ConnectionWriter writer, Http2RawFrame frame, CancellationToken token)
        {
            if (!TryGetStreamStateMachine(frame.Header.StreamIdentifier, out Http2StreamStateMachine stateMachine))
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.ProtocolError, "Received DATA for an unknown or idle stream.", token).ConfigureAwait(false);
                return;
            }

            bool endStream = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;

            try
            {
                ArraySegment<byte> dataPayload = GetDataPayload(frame);
                ConsumeReceiveWindow(frame.Header.StreamIdentifier, dataPayload.Count);
                stateMachine.ReceiveData(endStream);

                if (!TryGetPendingRequest(frame.Header.StreamIdentifier, out Http2PendingRequest pendingRequest))
                {
                    await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.ProtocolError, "Received DATA without a pending request header block.", token).ConfigureAwait(false);
                    return;
                }

                if (dataPayload.Count > 0)
                {
                    pendingRequest.AppendBody(dataPayload.Array, dataPayload.Offset, dataPayload.Count);
                    await ReplenishReceiveWindowAsync(writer, frame.Header.StreamIdentifier, dataPayload.Count, endStream, token).ConfigureAwait(false);

                    if (_Settings.IO.MaxRequestBodySize > 0 && pendingRequest.BodyLength > _Settings.IO.MaxRequestBodySize)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Request body size " + pendingRequest.BodyLength + " exceeds maximum allowed size " + _Settings.IO.MaxRequestBodySize + ".");
                    }
                }

                if (endStream)
                {
                    pendingRequest = TakePendingRequest(frame.Header.StreamIdentifier);
                    await DispatchRequestAsync(writer, frame.Header.StreamIdentifier, stateMachine, pendingRequest, token).ConfigureAwait(false);
                }
            }
            catch (Http2ProtocolException ex)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, ex.ErrorCode, ex.Message, token).ConfigureAwait(false);
                return;
            }
            catch (Http2StreamStateException ex)
            {
                await SendGoAwayAsync(writer, frame.Header.StreamIdentifier, Http2ErrorCode.ProtocolError, ex.Message, token).ConfigureAwait(false);
                return;
            }
        }

        private async Task DispatchRequestAsync(Http2ConnectionWriter writer, int streamIdentifier, Http2StreamStateMachine stateMachine, Http2PendingRequest pendingRequest, CancellationToken token)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            if (pendingRequest == null) throw new ArgumentNullException(nameof(pendingRequest));

            if (GetActiveRequestCount() >= _Settings.Protocols.Http2.MaxConcurrentStreams)
            {
                await SendRstStreamAsync(writer, streamIdentifier, Http2ErrorCode.RefusedStream, token).ConfigureAwait(false);
                return;
            }

            Task requestTask = ProcessPendingRequestAsync(writer, streamIdentifier, stateMachine, pendingRequest, token);
            RegisterActiveRequestTask(streamIdentifier, requestTask);
            _ = ObserveRequestTaskAsync(streamIdentifier, requestTask);
        }

        private async Task ProcessPendingRequestAsync(Http2ConnectionWriter writer, int streamIdentifier, Http2StreamStateMachine stateMachine, Http2PendingRequest pendingRequest, CancellationToken token)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (stateMachine == null) throw new ArgumentNullException(nameof(stateMachine));
            if (pendingRequest == null) throw new ArgumentNullException(nameof(pendingRequest));

            Http2Context context = null;
            CancellationTokenSource requestTokenSource = null;

            try
            {
                Http2Request request = BuildRequest(pendingRequest);
                Http2Response response = new Http2Response(request, _Settings, writer, stateMachine, ReserveSendWindowAsync, streamIdentifier, _RemoteSettings.MaxFrameSize);
                context = new Http2Context(_Settings, request, response, BuildConnectionMetadata, BuildStreamMetadata);
                requestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                context.TokenSource = requestTokenSource;
                RegisterActiveRequestToken(streamIdentifier, requestTokenSource);
                await _ProcessContext(context, requestTokenSource.Token).ConfigureAwait(false);
            }
            catch (Http2ProtocolException ex)
            {
                await SendGoAwayAsync(writer, streamIdentifier, ex.ErrorCode, ex.Message, token).ConfigureAwait(false);
            }
            catch (Http2StreamStateException ex)
            {
                await SendGoAwayAsync(writer, streamIdentifier, Http2ErrorCode.ProtocolError, ex.Message, token).ConfigureAwait(false);
            }
            finally
            {
                RemoveActiveRequestToken(streamIdentifier);

                if (stateMachine.State == Http2StreamState.Closed)
                {
                    RemoveStreamState(streamIdentifier);
                    RemoveStreamFlowControlState(streamIdentifier);
                }

                if (context != null)
                {
                    context.Dispose();
                }

                if (requestTokenSource != null)
                {
                    requestTokenSource.Dispose();
                }
            }
        }

        private async Task<byte[]> ReadHeaderBlockAsync(Http2RawFrame initialFrame, CancellationToken token)
        {
            if (initialFrame == null) throw new ArgumentNullException(nameof(initialFrame));

            bool endHeaders = (initialFrame.Header.Flags & (byte)Http2FrameFlags.EndHeaders) == (byte)Http2FrameFlags.EndHeaders;
            ArraySegment<byte> initialHeaderFragment = GetHeaderBlockFragment(initialFrame);

            if (endHeaders)
            {
                if (_Settings.Protocols.Http2.MaxHeaderListSize > 0 && initialHeaderFragment.Count > _Settings.Protocols.Http2.MaxHeaderListSize)
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 header block exceeds the configured maximum header list size.");
                }

                if (initialHeaderFragment.Count < 1)
                {
                    return Array.Empty<byte>();
                }

                if (initialHeaderFragment.Offset == 0 && initialHeaderFragment.Count == initialHeaderFragment.Array.Length)
                {
                    return initialHeaderFragment.Array;
                }

                byte[] singleFrameHeaderBlock = new byte[initialHeaderFragment.Count];
                Buffer.BlockCopy(initialHeaderFragment.Array, initialHeaderFragment.Offset, singleFrameHeaderBlock, 0, initialHeaderFragment.Count);
                return singleFrameHeaderBlock;
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                if (initialHeaderFragment.Count > 0)
                {
                    memoryStream.Write(initialHeaderFragment.Array, initialHeaderFragment.Offset, initialHeaderFragment.Count);
                }

                while (!endHeaders)
                {
                    Http2RawFrame continuationFrame = await Http2FrameSerializer.ReadFrameAsync(_Stream, token).ConfigureAwait(false);
                    if (continuationFrame.Header.Type != Http2FrameType.Continuation)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected CONTINUATION frame while reading HTTP/2 header block.");
                    }

                    if (continuationFrame.Header.StreamIdentifier != initialFrame.Header.StreamIdentifier)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "CONTINUATION frames must use the same stream identifier as the originating HEADERS frame.");
                    }

                    if (continuationFrame.Payload.Length > 0)
                    {
                        memoryStream.Write(continuationFrame.Payload, 0, continuationFrame.Payload.Length);
                    }

                    if (_Settings.Protocols.Http2.MaxHeaderListSize > 0 && memoryStream.Length > _Settings.Protocols.Http2.MaxHeaderListSize)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 header block exceeds the configured maximum header list size.");
                    }

                    endHeaders = (continuationFrame.Header.Flags & (byte)Http2FrameFlags.EndHeaders) == (byte)Http2FrameFlags.EndHeaders;
                }

                return memoryStream.ToArray();
            }
        }

        private Http2PendingRequest ParsePendingRequest(byte[] headerBlockFragment)
        {
            if (headerBlockFragment == null) throw new ArgumentNullException(nameof(headerBlockFragment));

            List<HpackHeaderField> decodedHeaders = HpackCodec.Decode(headerBlockFragment, _HpackDecoderContext);
            List<HttpHeaderField> headers = new List<HttpHeaderField>();
            string methodRaw = null;
            string path = null;
            string scheme = null;
            string authority = null;
            bool regularHeaderSeen = false;

            for (int i = 0; i < decodedHeaders.Count; i++)
            {
                HpackHeaderField header = decodedHeaders[i];
                if (header == null || String.IsNullOrEmpty(header.Name))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 headers must contain a non-empty name.");
                }

                if (!IsLowercaseHeaderName(header.Name))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 header names must be lowercase.");
                }

                if (header.Name.StartsWith(":", StringComparison.Ordinal))
                {
                    if (regularHeaderSeen)
                    {
                        throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 pseudo-headers must precede regular headers.");
                    }

                    switch (header.Name)
                    {
                        case ":method":
                            if (methodRaw != null) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Duplicate :method pseudo-header was received.");
                            methodRaw = header.Value;
                            break;
                        case ":path":
                            if (path != null) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Duplicate :path pseudo-header was received.");
                            path = header.Value;
                            break;
                        case ":scheme":
                            if (scheme != null) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Duplicate :scheme pseudo-header was received.");
                            scheme = header.Value;
                            break;
                        case ":authority":
                            if (authority != null) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Duplicate :authority pseudo-header was received.");
                            authority = header.Value;
                            break;
                        default:
                            throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Unsupported HTTP/2 pseudo-header was received.");
                    }
                }
                else
                {
                    regularHeaderSeen = true;
                    ValidateRequestHeader(header.Name, header.Value, authority);
                    headers.Add(new HttpHeaderField(header.Name, header.Value));
                }
            }

            if (String.IsNullOrEmpty(methodRaw))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 requests must include :method.");
            }

            if (String.IsNullOrEmpty(path))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 requests must include :path.");
            }

            if (String.IsNullOrEmpty(scheme))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 requests must include :scheme.");
            }

            if (!path.StartsWith("/", StringComparison.Ordinal) && !path.Equals("*", StringComparison.Ordinal))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 :path must be absolute or '*'.");
            }

            HttpMethod method = ParseMethod(methodRaw);
            Http2PendingRequest pendingRequest = new Http2PendingRequest();
            pendingRequest.Method = method;
            pendingRequest.MethodRaw = methodRaw;
            pendingRequest.Scheme = scheme;
            pendingRequest.Authority = authority;
            pendingRequest.Path = path;
            pendingRequest.Headers = headers.ToArray();
            pendingRequest.ExpectedContentLength = GetExpectedContentLength(headers);

            if (pendingRequest.ExpectedContentLength.HasValue
                && pendingRequest.ExpectedContentLength.Value > 0
                && pendingRequest.ExpectedContentLength.Value <= Int32.MaxValue)
            {
                pendingRequest.InitializeExactBodyBuffer((int)pendingRequest.ExpectedContentLength.Value);
            }

            return pendingRequest;
        }

        private void ValidateRequestHeader(string lowerName, string value, string authority)
        {
            if (String.IsNullOrEmpty(lowerName))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 headers must contain a non-empty name.");
            }

            if (lowerName.Equals("connection", StringComparison.Ordinal)
                || lowerName.Equals("keep-alive", StringComparison.Ordinal)
                || lowerName.Equals("proxy-connection", StringComparison.Ordinal)
                || lowerName.Equals("transfer-encoding", StringComparison.Ordinal)
                || lowerName.Equals("upgrade", StringComparison.Ordinal))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 requests must not include connection-specific headers.");
            }

            if (lowerName.Equals("te", StringComparison.Ordinal)
                && !String.Equals(value, "trailers", StringComparison.OrdinalIgnoreCase))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 TE header must be 'trailers' when present.");
            }

            if (lowerName.Equals("host", StringComparison.Ordinal)
                && !String.IsNullOrEmpty(authority)
                && !String.Equals(value, authority, StringComparison.OrdinalIgnoreCase))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 host header must match :authority when both are present.");
            }
        }

        private long? GetExpectedContentLength(System.Collections.Generic.IReadOnlyList<HttpHeaderField> headers)
        {
            if (headers == null || headers.Count < 1) return null;

            string contentLengthHeader = GetHeaderValue(headers, WebserverConstants.HeaderContentLength);
            if (String.IsNullOrEmpty(contentLengthHeader)) return null;

            if (!Int64.TryParse(contentLengthHeader, out long parsedContentLength) || parsedContentLength < 0)
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 Content-Length must be a non-negative integer.");
            }

            return parsedContentLength;
        }

        private Http2Request BuildRequest(Http2PendingRequest pendingRequest)
        {
            if (pendingRequest == null) throw new ArgumentNullException(nameof(pendingRequest));

            MemoryStream body = pendingRequest.DetachBodyStream();
            Stream bodyStream = body != null ? (Stream)body : Stream.Null;
            long bodyLength = body != null ? body.Length : 0;
            if (body != null && body.CanSeek)
            {
                body.Position = 0;
            }

            return new Http2Request(
                _Settings,
                new SourceDetails(_SourceIpAddress, _Source.Port),
                new DestinationDetails(_DestinationIpAddress, _Destination.Port, !String.IsNullOrEmpty(pendingRequest.Authority) ? pendingRequest.Authority : _Settings.Hostname),
                pendingRequest.Method,
                pendingRequest.MethodRaw,
                pendingRequest.Scheme,
                pendingRequest.Authority,
                pendingRequest.Path,
                pendingRequest.Headers,
                pendingRequest.Trailers,
                bodyStream,
                bodyLength);
        }

        private static string GetHeaderValue(System.Collections.Generic.IReadOnlyList<HttpHeaderField> headers, string key)
        {
            if (headers == null || headers.Count < 1 || String.IsNullOrEmpty(key)) return null;

            string firstValue = null;
            System.Text.StringBuilder combined = null;

            for (int i = 0; i < headers.Count; i++)
            {
                HttpHeaderField header = headers[i];
                if (!String.Equals(header.Name, key, StringComparison.OrdinalIgnoreCase)) continue;

                if (firstValue == null)
                {
                    firstValue = header.Value;
                }
                else
                {
                    if (combined == null)
                    {
                        combined = new System.Text.StringBuilder(firstValue);
                    }

                    combined.Append(',');
                    combined.Append(header.Value);
                }
            }

            if (combined != null) return combined.ToString();
            return firstValue;
        }


        private ConnectionMetadata BuildConnectionMetadata()
        {
            ConnectionMetadata metadata = new ConnectionMetadata();
            metadata.Protocol = HttpProtocol.Http2;
            metadata.IsEncrypted = _Encrypted;
            metadata.Source = new SourceDetails(_SourceIpAddress, _Source.Port);
            metadata.Destination = new DestinationDetails(_DestinationIpAddress, _Destination.Port, _Settings.Hostname);
            return metadata;
        }

        private StreamMetadata BuildStreamMetadata()
        {
            StreamMetadata metadata = new StreamMetadata();
            metadata.Protocol = HttpProtocol.Http2;
            metadata.Multiplexed = true;
            return metadata;
        }

        private HttpMethod ParseMethod(string methodRaw)
        {
            if (String.IsNullOrEmpty(methodRaw)) throw new ArgumentNullException(nameof(methodRaw));
            return HttpMethodParser.TryParse(methodRaw, out HttpMethod parsedMethod)
                ? parsedMethod
                : HttpMethod.UNKNOWN;
        }

        private void HandleRstStreamFrame(Http2RawFrame frame)
        {
            Http2RstStreamFrame rstStreamFrame = Http2FrameSerializer.ReadRstStreamFrame(frame);

            if (TryGetStreamStateMachine(rstStreamFrame.StreamIdentifier, out Http2StreamStateMachine stateMachine))
            {
                stateMachine.ReceiveReset();
                RemoveStreamState(rstStreamFrame.StreamIdentifier);
            }

            RemovePendingRequest(rstStreamFrame.StreamIdentifier);
            CancelActiveRequest(rstStreamFrame.StreamIdentifier);
            RemoveStreamFlowControlState(rstStreamFrame.StreamIdentifier);
        }

        private Http2StreamStateMachine GetOrCreateRemoteStream(int streamIdentifier)
        {
            lock (_SessionLock)
            {
                if (_Streams.TryGetValue(streamIdentifier, out Http2StreamStateMachine existingStateMachine))
                {
                    return existingStateMachine;
                }

                Http2StreamStateMachine stateMachine = new Http2StreamStateMachine(streamIdentifier);
                _Streams.Add(streamIdentifier, stateMachine);
                if (streamIdentifier > _LastRemoteStreamIdentifier) _LastRemoteStreamIdentifier = streamIdentifier;

                lock (_FlowControlLock)
                {
                    _StreamReceiveWindows[streamIdentifier] = _Settings.Protocols.Http2.InitialWindowSize;
                    _StreamSendWindows[streamIdentifier] = _RemoteSettings.InitialWindowSize;
                    _PendingStreamReceiveWindowUpdates[streamIdentifier] = 0;
                }

                return stateMachine;
            }
        }

        private async Task SendRstStreamAsync(Http2ConnectionWriter writer, int streamIdentifier, Http2ErrorCode errorCode, CancellationToken token)
        {
            Http2RstStreamFrame rstStreamFrame = new Http2RstStreamFrame();
            rstStreamFrame.StreamIdentifier = streamIdentifier;
            rstStreamFrame.ErrorCode = errorCode;
            await writer.WriteFrameAsync(Http2FrameSerializer.CreateRstStreamFrame(rstStreamFrame), token).ConfigureAwait(false);

            if (TryGetStreamStateMachine(streamIdentifier, out Http2StreamStateMachine stateMachine))
            {
                stateMachine.SendReset();
                RemoveStreamState(streamIdentifier);
            }

            RemovePendingRequest(streamIdentifier);
            RemoveStreamFlowControlState(streamIdentifier);
        }

        private async Task SendGoAwayAsync(Http2ConnectionWriter writer, int lastStreamIdentifier, Http2ErrorCode errorCode, string debugText, CancellationToken token)
        {
            Http2GoAwayFrame goAwayFrame = new Http2GoAwayFrame();
            goAwayFrame.LastStreamIdentifier = Math.Max(_LastRemoteStreamIdentifier, Math.Max(0, lastStreamIdentifier));
            goAwayFrame.ErrorCode = errorCode;
            goAwayFrame.AdditionalDebugData = String.IsNullOrEmpty(debugText) ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(debugText);

            await writer.WriteGoAwayAsync(goAwayFrame, token).ConfigureAwait(false);
            _State = Http2ConnectionState.Closed;
        }

        private async Task<int> ReserveSendWindowAsync(int streamIdentifier, int maximumLength, CancellationToken token)
        {
            if (streamIdentifier < 1) throw new ArgumentOutOfRangeException(nameof(streamIdentifier));
            if (maximumLength < 1) throw new ArgumentOutOfRangeException(nameof(maximumLength));

            while (true)
            {
                Task waitTask = null;

                lock (_FlowControlLock)
                {
                    if (_StreamSendWindows.TryGetValue(streamIdentifier, out int streamWindow))
                    {
                        int availableBytes = Math.Min(maximumLength, Math.Min(_ConnectionSendWindow, streamWindow));
                        if (availableBytes > 0)
                        {
                            _ConnectionSendWindow -= availableBytes;
                            _StreamSendWindows[streamIdentifier] = streamWindow - availableBytes;
                            return availableBytes;
                        }
                    }
                    else
                    {
                        return 0;
                    }

                    waitTask = _SendWindowSignal.Task;
                }

#if NET8_0_OR_GREATER
                await waitTask.WaitAsync(token).ConfigureAwait(false);
#else
                Task cancelTask = Task.Delay(Timeout.Infinite, token);
                Task completedTask = await Task.WhenAny(waitTask, cancelTask).ConfigureAwait(false);
                if (completedTask == cancelTask)
                {
                    token.ThrowIfCancellationRequested();
                }

                await waitTask.ConfigureAwait(false);
#endif
            }
        }

        private void ConsumeReceiveWindow(int streamIdentifier, int payloadLength)
        {
            if (payloadLength < 1) return;

            lock (_FlowControlLock)
            {
                if (!_StreamReceiveWindows.TryGetValue(streamIdentifier, out int streamWindow))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.FlowControlError, "Received DATA for a stream without receive flow-control state.");
                }

                if (payloadLength > _ConnectionReceiveWindow || payloadLength > streamWindow)
                {
                    throw new Http2ProtocolException(Http2ErrorCode.FlowControlError, "Received DATA exceeds the available HTTP/2 flow-control window.");
                }

                _ConnectionReceiveWindow -= payloadLength;
                _StreamReceiveWindows[streamIdentifier] = streamWindow - payloadLength;
            }
        }

        private async Task ReplenishReceiveWindowAsync(Http2ConnectionWriter writer, int streamIdentifier, int increment, bool endStream, CancellationToken token)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (increment < 1) return;

            int connectionIncrementToSend = 0;
            int streamIncrementToSend = 0;
            int threshold = Math.Max(16384, _Settings.Protocols.Http2.InitialWindowSize / 2);

            lock (_FlowControlLock)
            {
                _PendingConnectionReceiveWindowUpdate = AddFlowControlWindow(_PendingConnectionReceiveWindowUpdate, increment);
                if (_PendingConnectionReceiveWindowUpdate >= threshold || endStream)
                {
                    connectionIncrementToSend = _PendingConnectionReceiveWindowUpdate;
                    _ConnectionReceiveWindow = AddFlowControlWindow(_ConnectionReceiveWindow, connectionIncrementToSend);
                    _PendingConnectionReceiveWindowUpdate = 0;
                }

                if (_PendingStreamReceiveWindowUpdates.ContainsKey(streamIdentifier))
                {
                    _PendingStreamReceiveWindowUpdates[streamIdentifier] = AddFlowControlWindow(_PendingStreamReceiveWindowUpdates[streamIdentifier], increment);
                    if (_PendingStreamReceiveWindowUpdates[streamIdentifier] >= threshold || endStream)
                    {
                        streamIncrementToSend = _PendingStreamReceiveWindowUpdates[streamIdentifier];
                        if (_StreamReceiveWindows.ContainsKey(streamIdentifier))
                        {
                            _StreamReceiveWindows[streamIdentifier] = AddFlowControlWindow(_StreamReceiveWindows[streamIdentifier], streamIncrementToSend);
                        }

                        _PendingStreamReceiveWindowUpdates[streamIdentifier] = 0;
                    }
                }
            }

            if (connectionIncrementToSend > 0)
            {
                Http2WindowUpdateFrame connectionUpdate = new Http2WindowUpdateFrame();
                connectionUpdate.StreamIdentifier = 0;
                connectionUpdate.WindowSizeIncrement = connectionIncrementToSend;
                await writer.WriteFrameAsync(Http2FrameSerializer.CreateWindowUpdateFrame(connectionUpdate), token).ConfigureAwait(false);
            }

            if (streamIncrementToSend > 0)
            {
                Http2WindowUpdateFrame streamUpdate = new Http2WindowUpdateFrame();
                streamUpdate.StreamIdentifier = streamIdentifier;
                streamUpdate.WindowSizeIncrement = streamIncrementToSend;
                await writer.WriteFrameAsync(Http2FrameSerializer.CreateWindowUpdateFrame(streamUpdate), token).ConfigureAwait(false);
            }
        }

        private bool TryGetStreamStateMachine(int streamIdentifier, out Http2StreamStateMachine stateMachine)
        {
            lock (_SessionLock)
            {
                return _Streams.TryGetValue(streamIdentifier, out stateMachine);
            }
        }

        private void RemoveStreamState(int streamIdentifier)
        {
            lock (_SessionLock)
            {
                if (_Streams.ContainsKey(streamIdentifier))
                {
                    _Streams.Remove(streamIdentifier);
                }
            }
        }

        private void StorePendingRequest(int streamIdentifier, Http2PendingRequest pendingRequest)
        {
            if (pendingRequest == null) throw new ArgumentNullException(nameof(pendingRequest));

            lock (_SessionLock)
            {
                _PendingRequests[streamIdentifier] = pendingRequest;
            }
        }

        private bool TryGetPendingRequest(int streamIdentifier, out Http2PendingRequest pendingRequest)
        {
            lock (_SessionLock)
            {
                return _PendingRequests.TryGetValue(streamIdentifier, out pendingRequest);
            }
        }

        private Http2PendingRequest TakePendingRequest(int streamIdentifier)
        {
            Http2PendingRequest pendingRequest = null;

            lock (_SessionLock)
            {
                if (_PendingRequests.TryGetValue(streamIdentifier, out pendingRequest))
                {
                    _PendingRequests.Remove(streamIdentifier);
                }
            }

            return pendingRequest;
        }

        private void RemovePendingRequest(int streamIdentifier)
        {
            Http2PendingRequest pendingRequest = null;

            lock (_SessionLock)
            {
                if (_PendingRequests.TryGetValue(streamIdentifier, out pendingRequest))
                {
                    _PendingRequests.Remove(streamIdentifier);
                }
            }

            pendingRequest?.ReleaseResources();
        }

        private NameValueCollection ParseTrailerFields(byte[] headerBlockFragment)
        {
            if (headerBlockFragment == null) throw new ArgumentNullException(nameof(headerBlockFragment));

            List<HpackHeaderField> decodedHeaders = HpackCodec.Decode(headerBlockFragment, _HpackDecoderContext);
            NameValueCollection trailers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < decodedHeaders.Count; i++)
            {
                HpackHeaderField header = decodedHeaders[i];
                if (header == null || String.IsNullOrEmpty(header.Name))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 trailers must contain a non-empty name.");
                }

                if (!IsLowercaseHeaderName(header.Name))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 trailer names must be lowercase.");
                }

                if (header.Name.StartsWith(":", StringComparison.Ordinal))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 trailers must not include pseudo-headers.");
                }

                if (IsDisallowedTrailerHeader(header.Name))
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HTTP/2 trailers must not include connection-specific or representation-defining fields.");
                }

                trailers.Add(header.Name, header.Value);
            }

            return trailers;
        }

        private void MergeTrailers(NameValueCollection destination, NameValueCollection source)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source == null) throw new ArgumentNullException(nameof(source));

            for (int i = 0; i < source.Count; i++)
            {
                string key = source.GetKey(i);
                if (String.IsNullOrEmpty(key)) continue;

                string[] values = source.GetValues(i);
                if (values == null || values.Length < 1) continue;

                for (int j = 0; j < values.Length; j++)
                {
                    if (values[j] == null) continue;
                    destination.Add(key, values[j]);
                }
            }
        }

        private ArraySegment<byte> GetHeaderBlockFragment(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            int offset = 0;
            int length = frame.Payload.Length;

            if ((frame.Header.Flags & (byte)Http2FrameFlags.Padded) == (byte)Http2FrameFlags.Padded)
            {
                if (length < 1)
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Padded HEADERS frame is missing the Pad Length field.");
                }

                int padLength = frame.Payload[0];
                offset++;
                length--;

                if (padLength > length)
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "HEADERS frame padding exceeds the available payload length.");
                }

                length -= padLength;
            }

            if ((frame.Header.Flags & (byte)Http2FrameFlags.Priority) == (byte)Http2FrameFlags.Priority)
            {
                if (length < 5)
                {
                    throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Priority HEADERS frame is missing its dependency payload.");
                }

                offset += 5;
                length -= 5;
            }

            if (length < 1)
            {
                return new ArraySegment<byte>(Array.Empty<byte>());
            }

            return new ArraySegment<byte>(frame.Payload, offset, length);
        }

        private ArraySegment<byte> GetDataPayload(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            if ((frame.Header.Flags & (byte)Http2FrameFlags.Padded) != (byte)Http2FrameFlags.Padded)
            {
                return new ArraySegment<byte>(frame.Payload ?? Array.Empty<byte>());
            }

            if (frame.Payload == null || frame.Payload.Length < 1)
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Padded DATA frame is missing the Pad Length field.");
            }

            int padLength = frame.Payload[0];
            int dataLength = frame.Payload.Length - 1;
            if (padLength > dataLength)
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "DATA frame padding exceeds the available payload length.");
            }

            dataLength -= padLength;
            return new ArraySegment<byte>(frame.Payload, 1, dataLength);
        }

        private bool IsDisallowedTrailerHeader(string lowerName)
        {
            if (String.IsNullOrEmpty(lowerName)) return true;

            return lowerName.Equals("connection", StringComparison.Ordinal)
                || lowerName.Equals("keep-alive", StringComparison.Ordinal)
                || lowerName.Equals("proxy-connection", StringComparison.Ordinal)
                || lowerName.Equals("transfer-encoding", StringComparison.Ordinal)
                || lowerName.Equals("upgrade", StringComparison.Ordinal)
                || lowerName.Equals("host", StringComparison.Ordinal)
                || lowerName.Equals("content-length", StringComparison.Ordinal)
                || lowerName.Equals("content-type", StringComparison.Ordinal)
                || lowerName.Equals("trailer", StringComparison.Ordinal);
        }

        private int GetActiveRequestCount()
        {
            lock (_SessionLock)
            {
                return _ActiveRequestTasks.Count;
            }
        }

        private static bool IsLowercaseHeaderName(string name)
        {
            if (String.IsNullOrEmpty(name)) return false;

            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (character >= 'A' && character <= 'Z') return false;
            }

            return true;
        }

        private void RegisterActiveRequestTask(int streamIdentifier, Task requestTask)
        {
            if (requestTask == null) throw new ArgumentNullException(nameof(requestTask));

            lock (_SessionLock)
            {
                _ActiveRequestTasks[streamIdentifier] = requestTask;
            }
        }

        private void RegisterActiveRequestToken(int streamIdentifier, CancellationTokenSource requestTokenSource)
        {
            if (requestTokenSource == null) throw new ArgumentNullException(nameof(requestTokenSource));

            lock (_SessionLock)
            {
                _ActiveRequestTokens[streamIdentifier] = requestTokenSource;
            }
        }

        private void RemoveActiveRequestToken(int streamIdentifier)
        {
            lock (_SessionLock)
            {
                if (_ActiveRequestTokens.ContainsKey(streamIdentifier))
                {
                    _ActiveRequestTokens.Remove(streamIdentifier);
                }
            }
        }

        private void CancelActiveRequest(int streamIdentifier)
        {
            CancellationTokenSource requestTokenSource = null;

            lock (_SessionLock)
            {
                if (_ActiveRequestTokens.TryGetValue(streamIdentifier, out requestTokenSource))
                {
                    _ActiveRequestTokens.Remove(streamIdentifier);
                }
            }

            if (requestTokenSource != null && !requestTokenSource.IsCancellationRequested)
            {
                requestTokenSource.Cancel();
            }
        }

        private async Task ObserveRequestTaskAsync(int streamIdentifier, Task requestTask)
        {
            try
            {
                await requestTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (_SessionLock)
                {
                    if (_ActiveRequestTasks.ContainsKey(streamIdentifier))
                    {
                        _ActiveRequestTasks.Remove(streamIdentifier);
                    }
                }
            }
        }

        private async Task WaitForActiveRequestsAsync()
        {
            Task[] requestTasks = null;

            lock (_SessionLock)
            {
                requestTasks = new Task[_ActiveRequestTasks.Count];
                _ActiveRequestTasks.Values.CopyTo(requestTasks, 0);
            }

            if (requestTasks.Length > 0)
            {
                await Task.WhenAll(requestTasks).ConfigureAwait(false);
            }
        }

        private void RemoveStreamFlowControlState(int streamIdentifier)
        {
            lock (_FlowControlLock)
            {
                if (_StreamReceiveWindows.ContainsKey(streamIdentifier))
                {
                    _StreamReceiveWindows.Remove(streamIdentifier);
                }

                if (_StreamSendWindows.ContainsKey(streamIdentifier))
                {
                    _StreamSendWindows.Remove(streamIdentifier);
                }

                if (_PendingStreamReceiveWindowUpdates.ContainsKey(streamIdentifier))
                {
                    _PendingStreamReceiveWindowUpdates.Remove(streamIdentifier);
                }
            }
        }

        private void SignalSendWindowAvailability()
        {
            TaskCompletionSource<bool> previousSignal;

            lock (_FlowControlLock)
            {
                previousSignal = _SendWindowSignal;
                _SendWindowSignal = CreateSendWindowSignal();
            }

            previousSignal.TrySetResult(true);
        }

        private int AddFlowControlWindow(int currentValue, int increment)
        {
            long updatedValue = (long)currentValue + increment;
            if (updatedValue > Http2Constants.MaxInitialWindowSize)
            {
                throw new Http2ProtocolException(Http2ErrorCode.FlowControlError, "HTTP/2 flow-control window exceeded the maximum legal size.");
            }

            return (int)updatedValue;
        }

        private static TaskCompletionSource<bool> CreateSendWindowSignal()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}

