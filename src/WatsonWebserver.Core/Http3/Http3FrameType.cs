namespace WatsonWebserver.Core.Http3
{
    /// <summary>
    /// HTTP/3 frame types.
    /// </summary>
    public enum Http3FrameType : long
    {
        /// <summary>
        /// DATA.
        /// </summary>
        Data = 0x0,
        /// <summary>
        /// HEADERS.
        /// </summary>
        Headers = 0x1,
        /// <summary>
        /// CANCEL_PUSH.
        /// </summary>
        CancelPush = 0x3,
        /// <summary>
        /// SETTINGS.
        /// </summary>
        Settings = 0x4,
        /// <summary>
        /// PUSH_PROMISE.
        /// </summary>
        PushPromise = 0x5,
        /// <summary>
        /// GOAWAY.
        /// </summary>
        GoAway = 0x7,
        /// <summary>
        /// MAX_PUSH_ID.
        /// </summary>
        MaxPushId = 0xD
    }
}
