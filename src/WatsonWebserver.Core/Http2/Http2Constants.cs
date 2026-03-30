namespace WatsonWebserver.Core.Http2
{
    using System.Text;

    /// <summary>
    /// HTTP/2 protocol constants used by frame and settings helpers.
    /// </summary>
    public static class Http2Constants
    {
        /// <summary>
        /// Length of an HTTP/2 frame header in bytes.
        /// </summary>
        public const int FrameHeaderLength = 9;

        /// <summary>
        /// Default HTTP/2 maximum frame size.
        /// </summary>
        public const int DefaultMaxFrameSize = 16384;

        /// <summary>
        /// Minimum legal HTTP/2 maximum frame size.
        /// </summary>
        public const int MinMaxFrameSize = 16384;

        /// <summary>
        /// Maximum legal HTTP/2 maximum frame size.
        /// </summary>
        public const int MaxMaxFrameSize = 16777215;

        /// <summary>
        /// Default HPACK dynamic table size.
        /// </summary>
        public const uint DefaultHeaderTableSize = 4096;

        /// <summary>
        /// Default HTTP/2 initial flow-control window size.
        /// </summary>
        public const int DefaultInitialWindowSize = 65535;

        /// <summary>
        /// Maximum legal HTTP/2 initial flow-control window size.
        /// </summary>
        public const int MaxInitialWindowSize = 2147483647;

        /// <summary>
        /// Default advertised header list size limit.
        /// </summary>
        public const uint DefaultMaxHeaderListSize = 65536;

        /// <summary>
        /// Client connection preface bytes.
        /// </summary>
        public static readonly byte[] ClientConnectionPrefaceBytes = Encoding.ASCII.GetBytes("PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n");
    }
}
