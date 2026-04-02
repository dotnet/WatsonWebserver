namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 frame flags.
    /// </summary>
    [Flags]
    public enum Http2FrameFlags : byte
    {
        /// <summary>
        /// No flags.
        /// </summary>
        None = 0,
        /// <summary>
        /// END_STREAM or ACK, depending on frame type.
        /// </summary>
        EndStreamOrAck = 1,
        /// <summary>
        /// END_HEADERS.
        /// </summary>
        EndHeaders = 4,
        /// <summary>
        /// PADDED.
        /// </summary>
        Padded = 8,
        /// <summary>
        /// PRIORITY.
        /// </summary>
        Priority = 32
    }
}
