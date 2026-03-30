namespace WatsonWebserver.Core.Settings
{
    using System;

    /// <summary>
    /// Input-output settings.
    /// </summary>
    public class IOSettings
    {
        /// <summary>
        /// HTTP/1.1-specific settings.
        /// </summary>
        public Http1IOSettings Http1
        {
            get
            {
                return _Http1;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Http1));
                _Http1 = value;
            }
        }

        /// <summary>
        /// Buffer size to use when interacting with streams.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                _StreamBufferSize = value;
            }
        }

        /// <summary>
        /// Maximum number of concurrent requests.
        /// </summary>
        public int MaxRequests
        {
            get
            {
                return _MaxRequests;
            }
            set
            {
                if (value < 1) throw new ArgumentException("Maximum requests must be greater than zero.");
                _MaxRequests = value;
            }
        }

        /// <summary>
        /// Read timeout, in milliseconds, for inbound socket reads.
        /// </summary>
        public int ReadTimeoutMs
        {
            get
            {
                return _ReadTimeoutMs;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMs));
                _ReadTimeoutMs = value;
            }
        }

        /// <summary>
        /// Maximum incoming header size, in bytes.
        /// </summary>
        public int MaxIncomingHeadersSize
        {
            get
            {
                return _MaxIncomingHeadersSize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxIncomingHeadersSize));
                _MaxIncomingHeadersSize = value;
            }
        }

        /// <summary>
        /// Flag indicating whether or not the server requests a persistent connection.
        /// </summary>
        public bool EnableKeepAlive { get; set; } = false;

        /// <summary>
        /// Maximum request body size in bytes.
        /// A value of zero or less disables this check.
        /// Default is 0 (unlimited).
        /// This limit applies to Content-Length validation before reading the body.
        /// </summary>
        public long MaxRequestBodySize
        {
            get
            {
                return _MaxRequestBodySize;
            }
            set
            {
                _MaxRequestBodySize = value;
            }
        }

        /// <summary>
        /// Maximum number of headers allowed in a request.
        /// A value of zero or less disables this check.
        /// Default is 64.
        /// </summary>
        public int MaxHeaderCount
        {
            get
            {
                return _MaxHeaderCount;
            }
            set
            {
                _MaxHeaderCount = value;
            }
        }

        private int _StreamBufferSize = 65536;
        private int _MaxRequests = 1024;
        private int _ReadTimeoutMs = 10000;
        private int _MaxIncomingHeadersSize = 65536;
        private long _MaxRequestBodySize = 0;
        private int _MaxHeaderCount = 64;
        private Http1IOSettings _Http1 = new Http1IOSettings();

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public IOSettings()
        {
        }
    }
}
