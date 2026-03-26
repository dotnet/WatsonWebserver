namespace Test.Automated
{
    using System.IO;

    /// <summary>
    /// In-progress HTTP/2 response assembly state.
    /// </summary>
    internal class Http2ResponseAccumulator
    {
        /// <summary>
        /// Buffered header block bytes.
        /// </summary>
        public MemoryStream HeaderBlock { get; set; } = new MemoryStream();

        /// <summary>
        /// Buffered body bytes.
        /// </summary>
        public MemoryStream Body { get; set; } = new MemoryStream();

        /// <summary>
        /// Response envelope.
        /// </summary>
        public Http2ResponseEnvelope Response { get; set; } = new Http2ResponseEnvelope();

        /// <summary>
        /// Indicates whether the initial headers have been received.
        /// </summary>
        public bool HeadersReceived { get; set; } = false;
    }
}
