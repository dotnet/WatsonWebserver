namespace WatsonWebserver.Http1
{
    using System;
    using System.IO;
    using WatsonWebserver.Core;

    /// <summary>
    /// Transport stream and negotiated protocol details for a client connection.
    /// </summary>
    internal class ClientStreamContext
    {
        /// <summary>
        /// Negotiated application protocol.
        /// </summary>
        public HttpProtocol Protocol { get; set; } = HttpProtocol.Http1;

        /// <summary>
        /// Client stream.
        /// </summary>
        public Stream Stream
        {
            get
            {
                return _Stream;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Stream));
                _Stream = value;
            }
        }

        private Stream _Stream = Stream.Null;
    }
}

