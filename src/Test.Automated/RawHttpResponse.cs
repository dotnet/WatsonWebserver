namespace Test.Automated
{
    using System.Collections.Specialized;

    /// <summary>
    /// Minimal parsed HTTP/1.x response.
    /// </summary>
    internal class RawHttpResponse
    {
        /// <summary>
        /// Status line.
        /// </summary>
        public string StatusLine { get; set; } = string.Empty;

        /// <summary>
        /// Response headers.
        /// </summary>
        public NameValueCollection Headers { get; set; } = new NameValueCollection();

        /// <summary>
        /// Response body text.
        /// </summary>
        public string Body { get; set; } = string.Empty;
    }
}
