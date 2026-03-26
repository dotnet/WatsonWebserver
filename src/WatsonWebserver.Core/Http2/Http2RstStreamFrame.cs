namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 RST_STREAM payload.
    /// </summary>
    public class Http2RstStreamFrame
    {
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
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamIdentifier));
                _StreamIdentifier = value;
            }
        }

        /// <summary>
        /// HTTP/2 error code.
        /// </summary>
        public Http2ErrorCode ErrorCode { get; set; } = Http2ErrorCode.NoError;

        private int _StreamIdentifier = 1;
    }
}
