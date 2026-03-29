namespace WatsonWebserver.Core.WebSockets
{
    using System.Threading;

    /// <summary>
    /// Thread-safe WebSocket session counters.
    /// </summary>
    public class WebSocketSessionStatistics
    {
        /// <summary>
        /// Total messages received.
        /// </summary>
        public long MessagesReceived => Interlocked.Read(ref _MessagesReceived);

        /// <summary>
        /// Total messages sent.
        /// </summary>
        public long MessagesSent => Interlocked.Read(ref _MessagesSent);

        /// <summary>
        /// Total payload bytes received.
        /// </summary>
        public long BytesReceived => Interlocked.Read(ref _BytesReceived);

        /// <summary>
        /// Total payload bytes sent.
        /// </summary>
        public long BytesSent => Interlocked.Read(ref _BytesSent);

        private long _MessagesReceived = 0;
        private long _MessagesSent = 0;
        private long _BytesReceived = 0;
        private long _BytesSent = 0;

        /// <summary>
        /// Increment the received counters.
        /// </summary>
        /// <param name="bytes">Payload bytes.</param>
        public void IncrementReceived(long bytes)
        {
            Interlocked.Increment(ref _MessagesReceived);
            Interlocked.Add(ref _BytesReceived, bytes < 0 ? 0 : bytes);
        }

        /// <summary>
        /// Increment the sent counters.
        /// </summary>
        /// <param name="bytes">Payload bytes.</param>
        public void IncrementSent(long bytes)
        {
            Interlocked.Increment(ref _MessagesSent);
            Interlocked.Add(ref _BytesSent, bytes < 0 ? 0 : bytes);
        }

        /// <summary>
        /// Create an immutable snapshot of the current counters.
        /// </summary>
        /// <returns>Snapshot.</returns>
        public WebSocketSessionStatistics Snapshot()
        {
            WebSocketSessionStatistics snapshot = new WebSocketSessionStatistics();
            snapshot._MessagesReceived = MessagesReceived;
            snapshot._MessagesSent = MessagesSent;
            snapshot._BytesReceived = BytesReceived;
            snapshot._BytesSent = BytesSent;
            return snapshot;
        }
    }
}
