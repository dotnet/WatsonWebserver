namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 frame types.
    /// </summary>
    public enum Http2FrameType : byte
    {
        /// <summary>
        /// DATA.
        /// </summary>
        Data = 0,
        /// <summary>
        /// HEADERS.
        /// </summary>
        Headers = 1,
        /// <summary>
        /// PRIORITY.
        /// </summary>
        Priority = 2,
        /// <summary>
        /// RST_STREAM.
        /// </summary>
        RstStream = 3,
        /// <summary>
        /// SETTINGS.
        /// </summary>
        Settings = 4,
        /// <summary>
        /// PUSH_PROMISE.
        /// </summary>
        PushPromise = 5,
        /// <summary>
        /// PING.
        /// </summary>
        Ping = 6,
        /// <summary>
        /// GOAWAY.
        /// </summary>
        GoAway = 7,
        /// <summary>
        /// WINDOW_UPDATE.
        /// </summary>
        WindowUpdate = 8,
        /// <summary>
        /// CONTINUATION.
        /// </summary>
        Continuation = 9
    }
}
