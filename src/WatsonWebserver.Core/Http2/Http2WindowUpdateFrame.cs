namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 WINDOW_UPDATE payload.
    /// </summary>
    public class Http2WindowUpdateFrame
    {
        /// <summary>
        /// Stream identifier. Zero indicates connection-level flow control.
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

        /// <summary>
        /// Window increment value.
        /// </summary>
        public int WindowSizeIncrement
        {
            get
            {
                return _WindowSizeIncrement;
            }
            set
            {
                if (value < 1 || value > Int32.MaxValue) throw new ArgumentOutOfRangeException(nameof(WindowSizeIncrement));
                _WindowSizeIncrement = value;
            }
        }

        private int _StreamIdentifier = 0;
        private int _WindowSizeIncrement = 1;
    }
}
