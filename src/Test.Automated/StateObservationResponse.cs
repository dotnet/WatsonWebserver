namespace Test.Automated
{
    /// <summary>
    /// Typed response describing request state observations.
    /// </summary>
    public class StateObservationResponse
    {
        /// <summary>
        /// Trace header value.
        /// </summary>
        public string TraceHeader { get; set; } = null;

        /// <summary>
        /// Request body string.
        /// </summary>
        public string Body { get; set; } = null;

        /// <summary>
        /// Request content length.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// Indicates whether the request was chunked.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;
    }
}
