namespace Test.Automated
{
    using System;
    using System.Collections.Specialized;

    /// <summary>
    /// Simple HTTP/2 response envelope for raw transport tests.
    /// </summary>
    internal class Http2ResponseEnvelope
    {
        /// <summary>
        /// Response headers.
        /// </summary>
        public NameValueCollection Headers { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response trailers.
        /// </summary>
        public NameValueCollection Trailers { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response body string.
        /// </summary>
        public string BodyString { get; set; } = String.Empty;
    }
}
