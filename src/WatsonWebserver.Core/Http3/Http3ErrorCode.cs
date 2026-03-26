namespace WatsonWebserver.Core.Http3
{
    /// <summary>
    /// HTTP/3 application error codes.
    /// </summary>
    public enum Http3ErrorCode : long
    {
        /// <summary>
        /// No error.
        /// </summary>
        NoError = 0x100,
        /// <summary>
        /// General protocol error.
        /// </summary>
        GeneralProtocolError = 0x101,
        /// <summary>
        /// Internal error.
        /// </summary>
        InternalError = 0x102,
        /// <summary>
        /// Stream creation error.
        /// </summary>
        StreamCreationError = 0x103,
        /// <summary>
        /// Critical stream closed.
        /// </summary>
        ClosedCriticalStream = 0x104,
        /// <summary>
        /// Unexpected frame.
        /// </summary>
        FrameUnexpected = 0x105,
        /// <summary>
        /// Frame content error.
        /// </summary>
        FrameError = 0x106,
        /// <summary>
        /// Excessive load.
        /// </summary>
        ExcessiveLoad = 0x107,
        /// <summary>
        /// Identifier error.
        /// </summary>
        IdError = 0x108,
        /// <summary>
        /// SETTINGS error.
        /// </summary>
        SettingsError = 0x109,
        /// <summary>
        /// Missing control stream SETTINGS.
        /// </summary>
        MissingSettings = 0x10A,
        /// <summary>
        /// Request rejected.
        /// </summary>
        RequestRejected = 0x10B,
        /// <summary>
        /// Request cancelled.
        /// </summary>
        RequestCancelled = 0x10C,
        /// <summary>
        /// Incomplete request.
        /// </summary>
        RequestIncomplete = 0x10D,
        /// <summary>
        /// Malformed message.
        /// </summary>
        MessageError = 0x10E,
        /// <summary>
        /// Version fallback.
        /// </summary>
        VersionFallback = 0x110
    }
}
