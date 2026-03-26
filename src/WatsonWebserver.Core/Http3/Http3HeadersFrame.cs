namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// HTTP/3 HEADERS frame payload.
    /// </summary>
    public class Http3HeadersFrame
    {
        /// <summary>
        /// Encoded QPACK header block.
        /// </summary>
        public byte[] HeaderBlock
        {
            get
            {
                return _HeaderBlock;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(HeaderBlock));
                _HeaderBlock = value;
            }
        }

        private byte[] _HeaderBlock = Array.Empty<byte>();
    }
}
