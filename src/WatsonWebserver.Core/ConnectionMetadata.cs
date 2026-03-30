namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Connection metadata shared across protocol implementations.
    /// </summary>
    public class ConnectionMetadata
    {
        /// <summary>
        /// Connection identifier.
        /// </summary>
        public Guid Guid
        {
            get
            {
                if (_Guid == Guid.Empty) _Guid = Guid.NewGuid();
                return _Guid;
            }
            set
            {
                if (value == Guid.Empty) throw new ArgumentException("Guid cannot be empty.", nameof(Guid));
                _Guid = value;
            }
        }

        /// <summary>
        /// Negotiated protocol.
        /// </summary>
        public HttpProtocol Protocol { get; set; } = HttpProtocol.Http1;

        /// <summary>
        /// Source endpoint details.
        /// </summary>
        public SourceDetails Source
        {
            get
            {
                if (_Source == null) _Source = new SourceDetails();
                return _Source;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Source));
                _Source = value;
            }
        }

        /// <summary>
        /// Destination endpoint details.
        /// </summary>
        public DestinationDetails Destination
        {
            get
            {
                if (_Destination == null) _Destination = new DestinationDetails();
                return _Destination;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Destination));
                _Destination = value;
            }
        }

        /// <summary>
        /// Indicates whether the connection is encrypted.
        /// </summary>
        public bool IsEncrypted { get; set; } = false;

        private Guid _Guid = Guid.Empty;
        private SourceDetails _Source = null;
        private DestinationDetails _Destination = null;
    }
}
