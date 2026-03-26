namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// HTTP/3 DATA frame payload.
    /// </summary>
    public class Http3DataFrame
    {
        /// <summary>
        /// Payload bytes.
        /// </summary>
        public byte[] Data
        {
            get
            {
                return _Data;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Data));
                _Data = value;
            }
        }

        private byte[] _Data = Array.Empty<byte>();
    }
}
