namespace Test.BrowserInterop
{
    /// <summary>
    /// Browser test endpoint selection.
    /// </summary>
    internal class BrowserLocalEndpoint
    {
        /// <summary>
        /// Hostname used by the browser.
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Hostname or IP address used by the server bind configuration.
        /// </summary>
        public string BindAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Whether the endpoint is non-loopback.
        /// </summary>
        public bool IsNonLoopback { get; set; } = false;
    }
}
