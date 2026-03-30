namespace Test.BrowserInterop
{
    using System.Collections.Generic;

    /// <summary>
    /// Typed response payload for a browser navigation observation.
    /// </summary>
    internal class BrowserNavigationResponsePayload
    {
        /// <summary>
        /// Response URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int Status { get; set; } = 0;

        /// <summary>
        /// Chromium-reported protocol string.
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// Response headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
    }
}
