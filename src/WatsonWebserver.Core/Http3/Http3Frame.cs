namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// Raw HTTP/3 frame.
    /// </summary>
    public class Http3Frame
    {
        /// <summary>
        /// Frame header.
        /// </summary>
        public Http3FrameHeader Header
        {
            get
            {
                return _Header;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Header));
                _Header = value;
            }
        }

        /// <summary>
        /// Frame payload.
        /// </summary>
        public byte[] Payload
        {
            get
            {
                return _Payload;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Payload));
                _Payload = value;
            }
        }

        private Http3FrameHeader _Header = new Http3FrameHeader();
        private byte[] _Payload = Array.Empty<byte>();
    }
}
