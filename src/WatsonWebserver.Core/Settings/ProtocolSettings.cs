namespace WatsonWebserver.Core.Settings
{
    using System;
    using WatsonWebserver.Core.Http2;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// Protocol enablement and limits.
    /// </summary>
    public class ProtocolSettings
    {
        /// <summary>
        /// Enable HTTP/1.1 support.
        /// </summary>
        public bool EnableHttp1 { get; set; } = true;

        /// <summary>
        /// Enable HTTP/2 support.
        /// </summary>
        public bool EnableHttp2 { get; set; } = false;

        /// <summary>
        /// Enable HTTP/3 support.
        /// </summary>
        public bool EnableHttp3 { get; set; } = false;

        /// <summary>
        /// Enable cleartext HTTP/2 prior-knowledge mode.
        /// </summary>
        public bool EnableHttp2Cleartext { get; set; } = false;

        /// <summary>
        /// Maximum concurrent streams per connection.
        /// </summary>
        public int MaxConcurrentStreams
        {
            get
            {
                return (int)_Http2.MaxConcurrentStreams;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxConcurrentStreams));
                _Http2.MaxConcurrentStreams = (uint)value;
            }
        }

        /// <summary>
        /// HTTP/2 protocol settings advertised to peers.
        /// </summary>
        public Http2Settings Http2
        {
            get
            {
                return _Http2;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Http2));
                _Http2 = value;
            }
        }

        /// <summary>
        /// HTTP/3 protocol settings advertised to peers.
        /// </summary>
        public Http3Settings Http3
        {
            get
            {
                return _Http3;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Http3));
                _Http3 = value;
            }
        }

        /// <summary>
        /// Idle connection timeout in milliseconds.
        /// </summary>
        public int IdleTimeoutMs
        {
            get
            {
                return _IdleTimeoutMs;
            }
            set
            {
                if (value < 1000) throw new ArgumentOutOfRangeException(nameof(IdleTimeoutMs));
                _IdleTimeoutMs = value;
            }
        }

        private int _IdleTimeoutMs = 120000;
        private Http2Settings _Http2 = new Http2Settings();
        private Http3Settings _Http3 = new Http3Settings();
    }
}
