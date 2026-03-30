namespace WatsonWebserver.Http3
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net.Quic;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// Minimal HTTP/3 server connection session.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal class Http3ConnectionSession
    {
        /// <summary>
        /// Instantiate the session.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="events">Server events.</param>
        /// <param name="connection">QUIC connection.</param>
        /// <param name="processContext">Shared request processor.</param>
        public Http3ConnectionSession(
            WebserverSettings settings,
            WebserverEvents events,
            QuicConnection connection,
            Func<HttpContextBase, CancellationToken, Task> processContext)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (processContext == null) throw new ArgumentNullException(nameof(processContext));

            _Settings = settings;
            _Events = events;
            _Connection = connection;
            _ProcessContext = processContext;
            _SourceIpAddress = connection.RemoteEndPoint.Address.ToString();
            _DestinationIpAddress = connection.LocalEndPoint.Address.ToString();
        }

        private readonly WebserverSettings _Settings;
        private readonly WebserverEvents _Events;
        private readonly QuicConnection _Connection;
        private readonly Func<HttpContextBase, CancellationToken, Task> _ProcessContext;
        private readonly string _SourceIpAddress;
        private readonly string _DestinationIpAddress;
        private readonly object _Lock = new object();
        private readonly List<Task> _ActiveStreamTasks = new List<Task>();
        private readonly SemaphoreSlim _ControlStreamWriteLock = new SemaphoreSlim(1, 1);
        private QuicStream _OutboundControlStream = null;
        private QuicStream _OutboundQpackEncoderStream = null;
        private QuicStream _OutboundQpackDecoderStream = null;
        private QuicStream _InboundControlStream = null;
        private QuicStream _InboundQpackEncoderStream = null;
        private QuicStream _InboundQpackDecoderStream = null;
        private bool _RemoteControlStreamReceived = false;
        private bool _RemoteQpackEncoderStreamReceived = false;
        private bool _RemoteQpackDecoderStreamReceived = false;
        private bool _ConnectionClosing = false;
        private long _LastAcceptedBidirectionalStreamId = 0;
        private CancellationTokenSource _AcceptTimeoutTokenSource = null;

        /// <summary>
        /// Run the session.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RunAsync(CancellationToken token)
        {
            using (CancellationTokenSource acceptTimeoutTokenSource = new CancellationTokenSource())
            using (CancellationTokenRegistration acceptTimeoutRegistration = token.Register(static state => ((CancellationTokenSource)state).Cancel(), acceptTimeoutTokenSource))
            try
            {
                _AcceptTimeoutTokenSource = acceptTimeoutTokenSource;
                await SendControlStreamAsync(token).ConfigureAwait(false);
                await SendQpackStreamAsync(Http3StreamType.QpackEncoder, token).ConfigureAwait(false);
                await SendQpackStreamAsync(Http3StreamType.QpackDecoder, token).ConfigureAwait(false);

                while (!token.IsCancellationRequested && !_ConnectionClosing)
                {
                    QuicStream stream;
                    CancellationTokenSource currentAcceptTimeoutTokenSource = _AcceptTimeoutTokenSource;
                    if (currentAcceptTimeoutTokenSource == null)
                    {
                        throw new InvalidOperationException("HTTP/3 accept timeout state is not initialized.");
                    }

                    currentAcceptTimeoutTokenSource.CancelAfter(GetSessionIdleTimeoutMs());

                    try
                    {
                        stream = await _Connection.AcceptInboundStreamAsync(currentAcceptTimeoutTokenSource.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            await BeginGracefulShutdownAsync(_LastAcceptedBidirectionalStreamId, true, CancellationToken.None).ConfigureAwait(false);
                            break;
                        }

                        await BeginGracefulShutdownAsync(_LastAcceptedBidirectionalStreamId, true, CancellationToken.None).ConfigureAwait(false);
                        break;
                    }
                    finally
                    {
                        if (!currentAcceptTimeoutTokenSource.IsCancellationRequested)
                        {
                            currentAcceptTimeoutTokenSource.CancelAfter(Timeout.Infinite);
                        }
                    }

                    if (stream.Type == QuicStreamType.Bidirectional)
                    {
                        _LastAcceptedBidirectionalStreamId = stream.Id;
                    }

                    Task streamTask = stream.Type == QuicStreamType.Unidirectional
                        ? HandleUnidirectionalStreamAsync(stream, token)
                        : HandleBidirectionalStreamAsync(stream, token);

                    RegisterStreamTask(streamTask);
                    _ = ObserveStreamTaskAsync(streamTask);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (QuicException)
            {
            }
            finally
            {
                _AcceptTimeoutTokenSource = null;
                await WaitForActiveStreamsAsync().ConfigureAwait(false);
                _InboundControlStream = null;
                _InboundQpackEncoderStream = null;
                _InboundQpackDecoderStream = null;
                _OutboundQpackEncoderStream = null;
                _OutboundQpackDecoderStream = null;
                _OutboundControlStream = null;
            }
        }

        private async Task SendControlStreamAsync(CancellationToken token)
        {
            _OutboundControlStream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token).ConfigureAwait(false);
            byte[] payload = Http3ControlStreamSerializer.Serialize(_Settings.Protocols.Http3);
            await _OutboundControlStream.WriteAsync(payload, false, token).ConfigureAwait(false);
        }

        private async Task SendQpackStreamAsync(Http3StreamType streamType, CancellationToken token)
        {
            QuicStream qpackStream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token).ConfigureAwait(false);
            byte[] streamTypeBytes = Http3VarInt.Encode((long)streamType);
            await qpackStream.WriteAsync(streamTypeBytes, false, token).ConfigureAwait(false);

            if (streamType == Http3StreamType.QpackEncoder)
            {
                _OutboundQpackEncoderStream = qpackStream;
            }
            else if (streamType == Http3StreamType.QpackDecoder)
            {
                _OutboundQpackDecoderStream = qpackStream;
            }
        }

        private async Task HandleUnidirectionalStreamAsync(QuicStream stream, CancellationToken token)
        {
            try
            {
                long streamType = await Http3VarInt.ReadAsync(stream, token).ConfigureAwait(false);
                if (streamType == (long)Http3StreamType.Control)
                {
                    await HandleRemoteControlStreamAsync(stream, streamType, token).ConfigureAwait(false);
                    return;
                }

                if (streamType == (long)Http3StreamType.QpackEncoder)
                {
                    await HandleRemoteQpackStreamAsync(stream, Http3StreamType.QpackEncoder, token).ConfigureAwait(false);
                    return;
                }

                if (streamType == (long)Http3StreamType.QpackDecoder)
                {
                    await HandleRemoteQpackStreamAsync(stream, Http3StreamType.QpackDecoder, token).ConfigureAwait(false);
                    return;
                }

                await ReadRemainingBytesAsync(stream, token).ConfigureAwait(false);
            }
            catch (Http3ProtocolException e)
            {
                await CloseConnectionAsync(e.ErrorCode, e.Message).ConfigureAwait(false);
            }
            catch (QuicException)
            {
            }
            catch (IOException)
            {
            }
        }

        private async Task HandleRemoteControlStreamAsync(QuicStream stream, long streamType, CancellationToken token)
        {
            if (_RemoteControlStreamReceived)
            {
                throw new Http3ProtocolException(Http3ErrorCode.StreamCreationError, "HTTP/3 peer opened more than one control stream.");
            }

            Http3Frame settingsFrame = await Http3FrameSerializer.ReadFrameAsync(stream, token).ConfigureAwait(false);
            if (settingsFrame.Header.Type != (long)Http3FrameType.Settings)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MissingSettings, "HTTP/3 control stream did not begin with SETTINGS.");
            }

            Http3SettingsSerializer.ParsePayload(settingsFrame.Payload);
            _RemoteControlStreamReceived = true;
            _InboundControlStream = stream;
        }

        private async Task HandleRemoteQpackStreamAsync(QuicStream stream, Http3StreamType streamType, CancellationToken token)
        {
            if (streamType == Http3StreamType.QpackEncoder)
            {
                if (_RemoteQpackEncoderStreamReceived)
                {
                    throw new Http3ProtocolException(Http3ErrorCode.StreamCreationError, "HTTP/3 peer opened more than one QPACK encoder stream.");
                }

                _RemoteQpackEncoderStreamReceived = true;
                _InboundQpackEncoderStream = stream;
            }
            else if (streamType == Http3StreamType.QpackDecoder)
            {
                if (_RemoteQpackDecoderStreamReceived)
                {
                    throw new Http3ProtocolException(Http3ErrorCode.StreamCreationError, "HTTP/3 peer opened more than one QPACK decoder stream.");
                }

                _RemoteQpackDecoderStreamReceived = true;
                _InboundQpackDecoderStream = stream;
            }
        }

        private async Task HandleBidirectionalStreamAsync(QuicStream stream, CancellationToken token)
        {
            Http3Context context = null;
            CancellationTokenSource requestTokenSource = null;

            try
            {
                Http3MessageBody message = await Http3MessageSerializer.ReadMessageAsync(stream, CancellationToken.None).ConfigureAwait(false);
                if (TryGetDebugLogPath(out string debugLogPath))
                {
                    DebugLog(debugLogPath, "request-header-block=" + Convert.ToHexString(message.Headers.HeaderBlock));
                }

                Http3Request request = BuildRequest(message);
                Http3Response response = new Http3Response(request, _Settings, stream);
                context = new Http3Context(_Settings, request, response, BuildConnectionMetadata, BuildStreamMetadata);
                requestTokenSource = new CancellationTokenSource();
                context.TokenSource = requestTokenSource;
                await _ProcessContext(context, requestTokenSource.Token).ConfigureAwait(false);
            }
            catch (Http3ProtocolException e)
            {
                if (TryGetDebugLogPath(out string debugLogPath))
                {
                    DebugLog(debugLogPath, "protocol-exception=" + e.ErrorCode.ToString() + " message=" + e.Message);
                }

                await CloseConnectionAsync(e.ErrorCode, e.Message).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            finally
            {
                if (context != null)
                {
                    context.Dispose();
                }

                if (requestTokenSource != null)
                {
                    requestTokenSource.Dispose();
                }

                try
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }

        private Http3Request BuildRequest(Http3MessageBody message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            List<Http3HeaderField> headers = Http3HeaderCodec.Decode(message.Headers.HeaderBlock);
            List<HttpHeaderField> regularHeaders = new List<HttpHeaderField>();
            string methodRaw = null;
            string path = null;
            string scheme = null;
            string authority = null;
            bool sawRegularHeaders = false;

            for (int i = 0; i < headers.Count; i++)
            {
                Http3HeaderField header = headers[i];
                if (header == null || String.IsNullOrEmpty(header.Name))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 header block contained an empty header name.");
                }

                if (!IsLowercaseHeaderName(header.Name))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 header names must be lowercase.");
                }

                if (header.Name.StartsWith(":", StringComparison.Ordinal))
                {
                    if (sawRegularHeaders)
                    {
                        throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 pseudo-headers must precede regular headers.");
                    }

                    if (header.Name.Equals(":method", StringComparison.Ordinal))
                    {
                        if (methodRaw != null) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Duplicate :method pseudo-header was received.");
                        methodRaw = header.Value;
                    }
                    else if (header.Name.Equals(":path", StringComparison.Ordinal))
                    {
                        if (path != null) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Duplicate :path pseudo-header was received.");
                        path = header.Value;
                    }
                    else if (header.Name.Equals(":scheme", StringComparison.Ordinal))
                    {
                        if (scheme != null) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Duplicate :scheme pseudo-header was received.");
                        scheme = header.Value;
                    }
                    else if (header.Name.Equals(":authority", StringComparison.Ordinal))
                    {
                        if (authority != null) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Duplicate :authority pseudo-header was received.");
                        authority = header.Value;
                    }
                    else
                    {
                        throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Unsupported HTTP/3 pseudo-header '" + header.Name + "' was received.");
                    }
                }
                else
                {
                    sawRegularHeaders = true;
                    ValidateRequestHeader(header.Name, header.Value, authority);
                    regularHeaders.Add(new HttpHeaderField(header.Name, header.Value));
                }
            }

            if (String.IsNullOrEmpty(methodRaw))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 requests must include :method.");
            }

            if (String.IsNullOrEmpty(path))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 requests must include :path.");
            }

            if (String.IsNullOrEmpty(scheme))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 requests must include :scheme.");
            }

            if (!path.StartsWith("/", StringComparison.Ordinal) && !path.Equals("*", StringComparison.Ordinal))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 :path must be absolute or '*'.");
            }

            long bodyLength = message.BodyOrNull != null ? message.BodyOrNull.Length : 0;
            if (_Settings.IO.MaxRequestBodySize > 0 && bodyLength > _Settings.IO.MaxRequestBodySize)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Request body size " + bodyLength + " exceeds maximum allowed size " + _Settings.IO.MaxRequestBodySize + ".");
            }

            NameValueCollection trailerHeaders = ParseTrailers(message.Trailers);
            Stream body = message.BodyOrNull != null ? (Stream)message.BodyOrNull : Stream.Null;
            if (body.CanSeek)
            {
                body.Position = 0;
            }

            return new Http3Request(
                _Settings,
                new SourceDetails(_SourceIpAddress, _Connection.RemoteEndPoint.Port),
                new DestinationDetails(_DestinationIpAddress, _Connection.LocalEndPoint.Port, !String.IsNullOrEmpty(authority) ? authority : _Settings.Hostname),
                ParseMethod(methodRaw),
                methodRaw,
                scheme,
                authority,
                path,
                regularHeaders.ToArray(),
                trailerHeaders,
                body,
                bodyLength);
        }


        private void ValidateRequestHeader(string lowerName, string value, string authority)
        {
            if (String.IsNullOrEmpty(lowerName))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 header block contained an empty header name.");
            }

            if (lowerName.Equals("connection", StringComparison.Ordinal)
                || lowerName.Equals("keep-alive", StringComparison.Ordinal)
                || lowerName.Equals("proxy-connection", StringComparison.Ordinal)
                || lowerName.Equals("transfer-encoding", StringComparison.Ordinal)
                || lowerName.Equals("upgrade", StringComparison.Ordinal))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 requests must not include connection-specific headers.");
            }

            if (lowerName.Equals("te", StringComparison.Ordinal)
                && !String.Equals(value, "trailers", StringComparison.OrdinalIgnoreCase))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 TE header must be 'trailers' when present.");
            }

            if (lowerName.Equals("host", StringComparison.Ordinal)
                && !String.IsNullOrEmpty(authority)
                && !String.Equals(value, authority, StringComparison.OrdinalIgnoreCase))
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 host header must match :authority when both are present.");
            }
        }

        private NameValueCollection ParseTrailers(Http3HeadersFrame trailersFrame)
        {
            NameValueCollection trailers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (trailersFrame == null) return trailers;

            List<Http3HeaderField> headers = Http3HeaderCodec.Decode(trailersFrame.HeaderBlock);
            for (int i = 0; i < headers.Count; i++)
            {
                Http3HeaderField header = headers[i];
                if (header == null || String.IsNullOrEmpty(header.Name))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 trailers must contain a non-empty name.");
                }

                if (!IsLowercaseHeaderName(header.Name))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 trailer names must be lowercase.");
                }

                if (header.Name.StartsWith(":", StringComparison.Ordinal))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 trailers must not include pseudo-headers.");
                }

                if (IsDisallowedTrailerHeader(header.Name))
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 trailers must not include connection-specific or representation-defining fields.");
                }

                trailers.Add(header.Name, header.Value);
            }

            return trailers;
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

        private HttpMethod ParseMethod(string methodRaw)
        {
            return HttpMethodParser.TryParse(methodRaw, out HttpMethod parsedMethod)
                ? parsedMethod
                : HttpMethod.UNKNOWN;
        }

        private ConnectionMetadata BuildConnectionMetadata()
        {
            ConnectionMetadata metadata = new ConnectionMetadata();
            metadata.Protocol = HttpProtocol.Http3;
            metadata.IsEncrypted = true;
            metadata.Source = new SourceDetails(_SourceIpAddress, _Connection.RemoteEndPoint.Port);
            metadata.Destination = new DestinationDetails(_DestinationIpAddress, _Connection.LocalEndPoint.Port, _Settings.Hostname);
            return metadata;
        }

        private StreamMetadata BuildStreamMetadata()
        {
            StreamMetadata metadata = new StreamMetadata();
            metadata.Protocol = HttpProtocol.Http3;
            metadata.Multiplexed = true;
            return metadata;
        }

        private async Task<byte[]> ReadRemainingBytesAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] buffer = new byte[Math.Max(4096, _Settings.IO.StreamBufferSize)];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead < 1) break;
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return memoryStream.ToArray();
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

        private int GetSessionIdleTimeoutMs()
        {
            int configuredIdleTimeoutMs = Math.Max(1, _Settings.Protocols.IdleTimeoutMs);
            int gracefulIdleTimeoutMs = configuredIdleTimeoutMs - 500;
            return Math.Max(250, gracefulIdleTimeoutMs);
        }

        private void RegisterStreamTask(Task streamTask)
        {
            if (streamTask == null) throw new ArgumentNullException(nameof(streamTask));

            lock (_Lock)
            {
                _ActiveStreamTasks.Add(streamTask);
            }
        }

        private async Task ObserveStreamTaskAsync(Task streamTask)
        {
            try
            {
                await streamTask.ConfigureAwait(false);
            }
            finally
            {
                lock (_Lock)
                {
                    _ActiveStreamTasks.Remove(streamTask);
                }
            }
        }

        private async Task WaitForActiveStreamsAsync()
        {
            Task[] tasks;

            lock (_Lock)
            {
                tasks = _ActiveStreamTasks.ToArray();
            }

            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        private async Task BeginGracefulShutdownAsync(long identifier, bool waitForActiveStreams, CancellationToken token)
        {
            lock (_Lock)
            {
                if (_ConnectionClosing) return;
                _ConnectionClosing = true;
            }

            await SendGoAwayAsync(identifier, token).ConfigureAwait(false);

            if (waitForActiveStreams)
            {
                await WaitForActiveStreamsWithTimeoutAsync().ConfigureAwait(false);
            }

            await CloseConnectionNoErrorAsync().ConfigureAwait(false);
        }

        private async Task SendGoAwayAsync(long identifier, CancellationToken token)
        {
            try
            {
                if (_OutboundControlStream != null)
                {
                    Http3GoAwayFrame goAwayFrame = new Http3GoAwayFrame();
                    goAwayFrame.Identifier = identifier;
                    Http3Frame rawFrame = Http3FrameSerializer.CreateGoAwayFrame(goAwayFrame);
                    byte[] frameBytes = Http3FrameSerializer.SerializeFrame(rawFrame);

                    await _ControlStreamWriteLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        await _OutboundControlStream.WriteAsync(frameBytes, true, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        _ControlStreamWriteLock.Release();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private async Task CloseConnectionNoErrorAsync()
        {
            try
            {
                await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            try
            {
                await _Connection.CloseAsync((long)Http3ErrorCode.NoError, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private async Task WaitForActiveStreamsWithTimeoutAsync()
        {
            int shutdownWaitMs = Math.Max(250, _Settings.Protocols.IdleTimeoutMs);

            try
            {
                Task waitTask = WaitForActiveStreamsAsync();
                Task timeoutTask = Task.Delay(shutdownWaitMs, CancellationToken.None);
                Task completedTask = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
                if (completedTask == waitTask)
                {
                    await waitTask.ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }
        private async Task CloseConnectionAsync(Http3ErrorCode errorCode, string message)
        {
            lock (_Lock)
            {
                if (_ConnectionClosing) return;
                _ConnectionClosing = true;
            }

            try
            {
                await _Connection.CloseAsync((long)errorCode, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            if (!String.IsNullOrEmpty(message))
            {
                _Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, new Http3ProtocolException(errorCode, message)));
            }
        }

        private static bool TryGetDebugLogPath(out string path)
        {
            path = Environment.GetEnvironmentVariable("WATSON_HTTP3_DEBUG_PATH");
            return !String.IsNullOrEmpty(path);
        }

        private static void DebugLog(string path, string message)
        {
            if (String.IsNullOrEmpty(path)) return;

            try
            {
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }
    }
}

