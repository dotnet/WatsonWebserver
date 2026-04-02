namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// Parsed HTTP/3 control stream bootstrap payload.
    /// </summary>
    public class Http3ControlStreamPayload
    {
        /// <summary>
        /// Stream type.
        /// </summary>
        public Http3StreamType StreamType { get; set; } = Http3StreamType.Control;

        /// <summary>
        /// Initial peer settings.
        /// </summary>
        public Http3Settings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Settings));
                _Settings = value;
            }
        }

        private Http3Settings _Settings = new Http3Settings();
    }
}
