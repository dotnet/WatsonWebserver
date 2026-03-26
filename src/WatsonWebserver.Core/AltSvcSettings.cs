namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Alt-Svc advertising settings.
    /// </summary>
    public class AltSvcSettings
    {
        /// <summary>
        /// Enable Alt-Svc emission.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Optional advertised authority.
        /// </summary>
        public string Authority { get; set; } = null;

        /// <summary>
        /// Port advertised in Alt-Svc. A value of zero uses the primary server port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// HTTP/3 ALPN token to advertise.
        /// </summary>
        public string Http3Alpn { get; set; } = "h3";

        /// <summary>
        /// Advertised max-age in seconds.
        /// </summary>
        public int MaxAgeSeconds
        {
            get
            {
                return _MaxAgeSeconds;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxAgeSeconds));
                _MaxAgeSeconds = value;
            }
        }

        private int _Port = 0;
        private int _MaxAgeSeconds = 86400;
    }
}
