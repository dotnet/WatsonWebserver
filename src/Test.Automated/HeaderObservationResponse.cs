namespace Test.Automated
{
    /// <summary>
    /// Typed response describing request header observations.
    /// </summary>
    public class HeaderObservationResponse
    {
        /// <summary>
        /// Indicates whether the custom header exists.
        /// </summary>
        public bool HeaderExists { get; set; } = false;

        /// <summary>
        /// Header value retrieved from the fast lookup path.
        /// </summary>
        public string RetrievedHeaderValue { get; set; } = null;

        /// <summary>
        /// Header value retrieved from materialized headers.
        /// </summary>
        public string MaterializedHeaderValue { get; set; } = null;

        /// <summary>
        /// Request content type.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Request user agent.
        /// </summary>
        public string UserAgent { get; set; } = null;

        /// <summary>
        /// Request query value.
        /// </summary>
        public string QueryValue { get; set; } = null;

        /// <summary>
        /// Request body string.
        /// </summary>
        public string Body { get; set; } = null;
    }
}
