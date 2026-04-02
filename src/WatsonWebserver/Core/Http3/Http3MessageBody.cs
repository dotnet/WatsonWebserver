namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.IO;

    /// <summary>
    /// Parsed HTTP/3 bidirectional message content.
    /// </summary>
    public class Http3MessageBody
    {
        /// <summary>
        /// First HEADERS frame.
        /// </summary>
        public Http3HeadersFrame Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Headers));
                _Headers = value;
            }
        }

        /// <summary>
        /// Concatenated DATA payload.
        /// </summary>
        public MemoryStream Body
        {
            get
            {
                if (_Body == null)
                {
                    _Body = new MemoryStream();
                }

                return _Body;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Body));
                _Body = value;
            }
        }

        /// <summary>
        /// Concatenated DATA payload, if any DATA frames were received.
        /// </summary>
        public MemoryStream BodyOrNull
        {
            get
            {
                return _Body;
            }
        }

        /// <summary>
        /// Optional trailing HEADERS frame.
        /// </summary>
        public Http3HeadersFrame Trailers { get; set; } = null;

        private Http3HeadersFrame _Headers = new Http3HeadersFrame();
        private MemoryStream _Body = null;
    }
}
