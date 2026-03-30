namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 PING payload.
    /// </summary>
    public class Http2PingFrame
    {
        /// <summary>
        /// Indicates whether this frame acknowledges a prior ping.
        /// </summary>
        public bool Acknowledge { get; set; } = false;

        /// <summary>
        /// Opaque 8-byte ping payload.
        /// </summary>
        public byte[] OpaqueData
        {
            get
            {
                return _OpaqueData;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(OpaqueData));
                if (value.Length != 8) throw new ArgumentOutOfRangeException(nameof(OpaqueData), "PING payloads must be exactly 8 bytes.");
                _OpaqueData = (byte[])value.Clone();
            }
        }

        private byte[] _OpaqueData = new byte[8];
    }
}
