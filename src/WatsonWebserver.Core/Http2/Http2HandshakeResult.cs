namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// Result of the initial HTTP/2 connection handshake read.
    /// </summary>
    public class Http2HandshakeResult
    {
        /// <summary>
        /// Indicates whether a valid client preface was received.
        /// </summary>
        public bool ClientPrefaceReceived { get; set; } = false;

        /// <summary>
        /// Remote peer settings.
        /// </summary>
        public Http2Settings RemoteSettings
        {
            get
            {
                return _RemoteSettings;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(RemoteSettings));
                _RemoteSettings = value;
            }
        }

        private Http2Settings _RemoteSettings = new Http2Settings();
    }
}
