namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Watson-owned WebSocket session abstraction.
    /// </summary>
    public class WebSocketSession : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Session identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Indicates whether the underlying socket is still connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                WebSocketState state = State;
                return state == WebSocketState.Open || state == WebSocketState.CloseReceived || state == WebSocketState.CloseSent;
            }
        }

        /// <summary>
        /// Current socket state.
        /// </summary>
        public WebSocketState State
        {
            get
            {
                if (_HasTrackedState) return _TrackedState;

                try
                {
                    return _Socket?.State ?? WebSocketState.None;
                }
                catch (ObjectDisposedException)
                {
                    return WebSocketState.Closed;
                }
            }
        }

        /// <summary>
        /// Close status observed or requested for the session.
        /// </summary>
        public WebSocketCloseStatus? CloseStatus => _CloseStatus;

        /// <summary>
        /// Close status description observed or requested for the session.
        /// </summary>
        public string CloseStatusDescription => _CloseStatusDescription;

        /// <summary>
        /// Negotiated subprotocol, if any.
        /// </summary>
        public string Subprotocol => _Socket?.SubProtocol;

        /// <summary>
        /// Remote IP address.
        /// </summary>
        public string RemoteIp => Request?.RemoteIp ?? String.Empty;

        /// <summary>
        /// Remote TCP port.
        /// </summary>
        public int RemotePort => Request?.RemotePort ?? 0;

        /// <summary>
        /// Reduced immutable handshake metadata.
        /// </summary>
        public WebSocketRequestDescriptor Request { get; }

        /// <summary>
        /// Optional developer metadata bag.
        /// </summary>
        public object Metadata { get; set; }

        /// <summary>
        /// Session counters.
        /// </summary>
        public WebSocketSessionStatistics Statistics { get; } = new WebSocketSessionStatistics();

        internal event Action<WebSocketSession> SessionClosed;

        private readonly WebSocket _Socket;
        private readonly int _ReceiveBufferSize;
        private readonly int _MaxMessageSize;
        private readonly int _CloseHandshakeTimeoutMs;
        private readonly SemaphoreSlim _SendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _Lifetime = new CancellationTokenSource();
        private int _ReceiveState = 0;
        private int _Disposed = 0;
        private int _CloseStateSet = 0;
        private WebSocketCloseStatus? _CloseStatus = null;
        private string _CloseStatusDescription = null;
        private WebSocketState _TrackedState = WebSocketState.None;
        private bool _HasTrackedState = false;

        /// <summary>
        /// Instantiate the session.
        /// </summary>
        public WebSocketSession(
            WebSocket socket,
            WebSocketRequestDescriptor request,
            Guid guid = default,
            int receiveBufferSize = 65536,
            int maxMessageSize = 16777216,
            int closeHandshakeTimeoutMs = 5000)
        {
            _Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Id = guid == Guid.Empty ? Guid.NewGuid() : guid;
            _ReceiveBufferSize = receiveBufferSize < 1024 ? 1024 : receiveBufferSize;
            _MaxMessageSize = maxMessageSize < 1024 ? 1024 : maxMessageSize;
            _CloseHandshakeTimeoutMs = closeHandshakeTimeoutMs < 1000 ? 1000 : closeHandshakeTimeoutMs;
        }

        /// <summary>
        /// Receive the next whole message, or null when the session closes.
        /// </summary>
        public async Task<WebSocketMessage> ReceiveAsync(CancellationToken token = default)
        {
            EnsureSingleReceiver();

            try
            {
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _Lifetime.Token))
                {
                    return await ReceiveInternalAsync(linked.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _ReceiveState, 0);
            }
        }

        /// <summary>
        /// Asynchronously enumerate received whole messages until the session closes.
        /// </summary>
        public async IAsyncEnumerable<WebSocketMessage> ReadMessagesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
        {
            EnsureSingleReceiver();

            try
            {
                using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _Lifetime.Token))
                {
                    while (!linked.Token.IsCancellationRequested)
                    {
                        WebSocketMessage message = await ReceiveInternalAsync(linked.Token).ConfigureAwait(false);
                        if (message == null) yield break;
                        yield return message;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _ReceiveState, 0);
            }
        }

        /// <summary>
        /// Send a UTF-8 text message.
        /// </summary>
        public Task SendTextAsync(string data, CancellationToken token = default)
        {
            byte[] bytes = data == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(data);
            return SendAsync(WebSocketMessageType.Text, new ArraySegment<byte>(bytes), token);
        }

        /// <summary>
        /// Send a binary message.
        /// </summary>
        public Task SendBinaryAsync(byte[] data, CancellationToken token = default)
        {
            return SendAsync(WebSocketMessageType.Binary, new ArraySegment<byte>(data ?? Array.Empty<byte>()), token);
        }

        /// <summary>
        /// Send a binary message.
        /// </summary>
        public Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken token = default)
        {
            return SendAsync(WebSocketMessageType.Binary, data, token);
        }

        /// <summary>
        /// Close the session.
        /// </summary>
        public async Task CloseAsync(WebSocketCloseStatus closeStatus, string reason, CancellationToken token = default)
        {
            if (_Socket == null) return;
            if (_Socket.State == WebSocketState.Closed || _Socket.State == WebSocketState.Aborted) return;
            SetCloseState(closeStatus, reason);
            SetTrackedState(WebSocketState.CloseSent);

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token, _Lifetime.Token))
            {
                timeout.CancelAfter(_CloseHandshakeTimeoutMs);

                try
                {
                    await _Socket.CloseAsync(closeStatus, reason, timeout.Token).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    _Socket.Abort();
                }
                catch (OperationCanceledException)
                {
                    _Socket.Abort();
                }
                finally
                {
                    SetTrackedState(WebSocketState.Closed);
                    await DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Dispose the session.
        /// </summary>
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Dispose the session asynchronously.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            try
            {
                _Lifetime.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _SendLock.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _Lifetime.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }

            if (_Socket != null)
            {
                try
                {
                    _Socket.Dispose();
                }
                catch (Exception)
                {
                }
            }

            await Task.CompletedTask.ConfigureAwait(false);
            SessionClosed?.Invoke(this);
        }

        private void EnsureSingleReceiver()
        {
            if (Interlocked.CompareExchange(ref _ReceiveState, 1, 0) != 0)
            {
                throw new InvalidOperationException("Only one receive operation may be active per WebSocket session.");
            }
        }

        private async Task SendAsync(WebSocketMessageType messageType, ArraySegment<byte> data, CancellationToken token)
        {
            if (!IsConnected) throw new IOException("The WebSocket session is not connected.");

            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, _Lifetime.Token))
            {
                await _SendLock.WaitAsync(linked.Token).ConfigureAwait(false);

                try
                {
                    await _Socket.SendAsync(data, messageType, true, linked.Token).ConfigureAwait(false);
                    Statistics.IncrementSent(data.Count);
                }
                finally
                {
                    _SendLock.Release();
                }
            }
        }

        private async Task<WebSocketMessage> ReceiveInternalAsync(CancellationToken token)
        {
            if (!IsConnected) return null;

            byte[] receiveBuffer = ArrayPool<byte>.Shared.Rent(_ReceiveBufferSize);
            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    while (true)
                    {
                        WebSocketReceiveResult result = await _Socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            SetCloseState(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription);
                            SetTrackedState(WebSocketState.CloseReceived);
                            await CloseAsync(result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription, token).ConfigureAwait(false);
                            return null;
                        }

                        if (result.Count > 0)
                        {
                            stream.Write(receiveBuffer, 0, result.Count);
                            if (stream.Length > _MaxMessageSize)
                            {
                                SetCloseState(WebSocketCloseStatus.MessageTooBig, "WebSocket message exceeds the configured maximum size.");
                                SetTrackedState(WebSocketState.CloseSent);
                                await CloseAsync(WebSocketCloseStatus.MessageTooBig, "WebSocket message exceeds the configured maximum size.", token).ConfigureAwait(false);
                                return null;
                            }
                        }

                        if (result.EndOfMessage)
                        {
                            byte[] payload = stream.ToArray();
                            Statistics.IncrementReceived(payload.Length);
                            return new WebSocketMessage(result.MessageType, payload);
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(receiveBuffer);
            }
        }

        private void SetCloseState(WebSocketCloseStatus closeStatus, string reason)
        {
            if (Interlocked.CompareExchange(ref _CloseStateSet, 1, 0) != 0) return;

            _CloseStatus = closeStatus;
            _CloseStatusDescription = reason;
        }

        private void SetTrackedState(WebSocketState state)
        {
            _TrackedState = state;
            _HasTrackedState = true;
        }
    }
}
