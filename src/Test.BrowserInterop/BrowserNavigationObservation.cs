namespace Test.BrowserInterop
{
    /// <summary>
    /// Browser navigation observation.
    /// </summary>
    internal class BrowserNavigationObservation
    {
        /// <summary>
        /// Navigated URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Chromium-reported protocol string.
        /// </summary>
        public string Protocol { get; set; } = string.Empty;

        /// <summary>
        /// Alt-Svc response header.
        /// </summary>
        public string AltSvcHeader { get; set; } = string.Empty;

        /// <summary>
        /// Response body text.
        /// </summary>
        public string BodyText { get; set; } = string.Empty;
    }
}
