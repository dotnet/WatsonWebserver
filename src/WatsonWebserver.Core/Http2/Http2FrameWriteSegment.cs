namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// Describes a frame header and payload slice to write without cloning the payload.
    /// </summary>
    public sealed class Http2FrameWriteSegment
    {
        /// <summary>
        /// Frame header.
        /// </summary>
        public Http2FrameHeader Header { get; set; } = null;

        /// <summary>
        /// Payload buffer.
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Payload offset.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Payload length.
        /// </summary>
        public int Count { get; set; } = 0;
    }
}
