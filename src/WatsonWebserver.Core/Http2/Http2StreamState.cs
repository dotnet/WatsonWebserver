namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 stream state.
    /// </summary>
    public enum Http2StreamState
    {
        /// <summary>
        /// Stream is idle.
        /// </summary>
        Idle = 0,
        /// <summary>
        /// Stream is reserved for local use.
        /// </summary>
        ReservedLocal = 1,
        /// <summary>
        /// Stream is reserved for remote use.
        /// </summary>
        ReservedRemote = 2,
        /// <summary>
        /// Stream is open.
        /// </summary>
        Open = 3,
        /// <summary>
        /// Stream is half-closed by the local endpoint.
        /// </summary>
        HalfClosedLocal = 4,
        /// <summary>
        /// Stream is half-closed by the remote endpoint.
        /// </summary>
        HalfClosedRemote = 5,
        /// <summary>
        /// Stream is closed.
        /// </summary>
        Closed = 6
    }
}
