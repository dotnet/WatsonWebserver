namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 GOAWAY payload.
    /// </summary>
    public class Http2GoAwayFrame
    {
        /// <summary>
        /// Last processed stream identifier.
        /// </summary>
        public int LastStreamIdentifier
        {
            get
            {
                return _LastStreamIdentifier;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(LastStreamIdentifier));
                _LastStreamIdentifier = value & Int32.MaxValue;
            }
        }

        /// <summary>
        /// HTTP/2 error code.
        /// </summary>
        public Http2ErrorCode ErrorCode { get; set; } = Http2ErrorCode.NoError;

        /// <summary>
        /// Optional debug data.
        /// </summary>
        public byte[] AdditionalDebugData
        {
            get
            {
                return _AdditionalDebugData;
            }
            set
            {
                if (value == null)
                {
                    _AdditionalDebugData = Array.Empty<byte>();
                    return;
                }

                _AdditionalDebugData = (byte[])value.Clone();
            }
        }

        private int _LastStreamIdentifier = 0;
        private byte[] _AdditionalDebugData = Array.Empty<byte>();
    }
}
