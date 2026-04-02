namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 frame with raw payload bytes.
    /// </summary>
    public class Http2RawFrame
    {
        /// <summary>
        /// Frame header.
        /// </summary>
        public Http2FrameHeader Header
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
                _Payload = (byte[])value.Clone();
            }
        }

        private Http2FrameHeader _Header = new Http2FrameHeader();
        private byte[] _Payload = Array.Empty<byte>();

        /// <summary>
        /// Instantiate the frame.
        /// </summary>
        public Http2RawFrame()
        {
        }

        /// <summary>
        /// Instantiate the frame.
        /// </summary>
        /// <param name="header">Frame header.</param>
        /// <param name="payload">Frame payload.</param>
        public Http2RawFrame(Http2FrameHeader header, byte[] payload)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (header.Length != payload.Length) throw new ArgumentException("Frame payload length must match the declared header length.", nameof(payload));

            _Header = header;
            _Payload = (byte[])payload.Clone();
        }

        /// <summary>
        /// Instantiate a frame from a payload buffer already owned by the caller.
        /// </summary>
        /// <param name="header">Frame header.</param>
        /// <param name="ownedPayload">Caller-owned payload buffer.</param>
        /// <returns>Frame instance.</returns>
        public static Http2RawFrame CreateOwned(Http2FrameHeader header, byte[] ownedPayload)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (ownedPayload == null) throw new ArgumentNullException(nameof(ownedPayload));
            if (header.Length != ownedPayload.Length) throw new ArgumentException("Frame payload length must match the declared header length.", nameof(ownedPayload));

            Http2RawFrame frame = new Http2RawFrame();
            frame._Header = header;
            frame._Payload = ownedPayload;
            return frame;
        }
    }
}
