namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 connection state.
    /// </summary>
    public enum Http2ConnectionState
    {
        /// <summary>
        /// Connection has not yet received the client preface.
        /// </summary>
        AwaitingPreface = 0,
        /// <summary>
        /// Connection preface has been exchanged.
        /// </summary>
        Open = 1,
        /// <summary>
        /// Connection is draining and should not accept new streams.
        /// </summary>
        Draining = 2,
        /// <summary>
        /// Connection is closed.
        /// </summary>
        Closed = 3
    }
}
