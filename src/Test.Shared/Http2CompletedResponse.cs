namespace Test.Shared
{
    /// <summary>
    /// Completed HTTP/2 response with stream identifier.
    /// </summary>
    public class Http2CompletedResponse
    {
        /// <summary>
        /// Stream identifier.
        /// </summary>
        public int StreamIdentifier { get; set; } = 0;

        /// <summary>
        /// Response envelope.
        /// </summary>
        public Http2ResponseEnvelope Response { get; set; } = new Http2ResponseEnvelope();
    }
}
