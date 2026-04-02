namespace WatsonWebserver.Core.Http1
{
    /// <summary>
    /// Result from reading an HTTP/1.1 header block.
    /// </summary>
    public class Http1HeaderReadResult
    {
        /// <summary>
        /// Request header bytes.
        /// </summary>
        public byte[] HeaderBytes { get; set; } = System.Array.Empty<byte>();

        /// <summary>
        /// Prefetched bytes beyond the end of the header block.
        /// </summary>
        public byte[] PrefixBytes { get; set; } = System.Array.Empty<byte>();
    }
}
