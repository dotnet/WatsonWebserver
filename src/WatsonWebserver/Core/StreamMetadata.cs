namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Request stream metadata shared across protocol implementations.
    /// </summary>
    public class StreamMetadata
    {
        /// <summary>
        /// Stream identifier.
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
        /// Indicates whether the protocol supports multiplexing for the stream.
        /// </summary>
        public bool Multiplexed { get; set; } = false;

        private Guid _Guid = Guid.Empty;
    }
}
