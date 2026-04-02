namespace WatsonWebserver.Core
{
    /// <summary>
    /// Supported HTTP protocol families.
    /// </summary>
    public enum HttpProtocol
    {
        /// <summary>
        /// HTTP/1.x.
        /// </summary>
        Http1 = 1,
        /// <summary>
        /// HTTP/2.
        /// </summary>
        Http2 = 2,
        /// <summary>
        /// HTTP/3.
        /// </summary>
        Http3 = 3
    }
}
