namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 frame header.
    /// </summary>
    public class Http2FrameHeader
    {
        /// <summary>
        /// Payload length in bytes.
        /// </summary>
        public int Length
        {
            get
            {
                return _Length;
            }
            set
            {
                if (value < 0 || value > Http2Constants.MaxMaxFrameSize) throw new ArgumentOutOfRangeException(nameof(Length));
                _Length = value;
            }
        }

        /// <summary>
        /// Frame type.
        /// </summary>
        public Http2FrameType Type { get; set; } = Http2FrameType.Data;

        /// <summary>
        /// Frame flags.
        /// </summary>
        public byte Flags { get; set; } = 0;

        /// <summary>
        /// Stream identifier.
        /// </summary>
        public int StreamIdentifier
        {
            get
            {
                return _StreamIdentifier;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(StreamIdentifier));
                _StreamIdentifier = value & Int32.MaxValue;
            }
        }

        private int _Length = 0;
        private int _StreamIdentifier = 0;
    }
}
