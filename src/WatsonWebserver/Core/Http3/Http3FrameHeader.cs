namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// HTTP/3 frame header.
    /// </summary>
    public class Http3FrameHeader
    {
        /// <summary>
        /// Frame type.
        /// </summary>
        public long Type { get; set; } = (long)Http3FrameType.Data;

        /// <summary>
        /// Payload length.
        /// </summary>
        public long Length
        {
            get
            {
                return _Length;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Length));
                _Length = value;
            }
        }

        private long _Length = 0;
    }
}
