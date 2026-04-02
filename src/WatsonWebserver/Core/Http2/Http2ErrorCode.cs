namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 error codes.
    /// </summary>
    public enum Http2ErrorCode : uint
    {
        /// <summary>
        /// Graceful completion.
        /// </summary>
        NoError = 0,
        /// <summary>
        /// Generic protocol error.
        /// </summary>
        ProtocolError = 1,
        /// <summary>
        /// Internal implementation failure.
        /// </summary>
        InternalError = 2,
        /// <summary>
        /// Flow-control violation.
        /// </summary>
        FlowControlError = 3,
        /// <summary>
        /// Settings timeout.
        /// </summary>
        SettingsTimeout = 4,
        /// <summary>
        /// Stream closed.
        /// </summary>
        StreamClosed = 5,
        /// <summary>
        /// Invalid frame size.
        /// </summary>
        FrameSizeError = 6,
        /// <summary>
        /// Refused stream.
        /// </summary>
        RefusedStream = 7,
        /// <summary>
        /// Request canceled.
        /// </summary>
        Cancel = 8,
        /// <summary>
        /// Compression state failure.
        /// </summary>
        CompressionError = 9,
        /// <summary>
        /// TCP connect failure for proxy use.
        /// </summary>
        ConnectError = 10,
        /// <summary>
        /// Excessive processing demand.
        /// </summary>
        EnhanceYourCalm = 11,
        /// <summary>
        /// Inadequate transport security.
        /// </summary>
        InadequateSecurity = 12,
        /// <summary>
        /// HTTP/1.1 required.
        /// </summary>
        Http11Required = 13
    }
}
