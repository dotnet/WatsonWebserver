namespace Test.BrowserInterop
{
    /// <summary>
    /// Browser capability summary.
    /// </summary>
    internal class BrowserCapabilities
    {
        /// <summary>
        /// Whether a supported Chromium browser is available.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Browser display name.
        /// </summary>
        public string BrowserName { get; set; } = string.Empty;

        /// <summary>
        /// Browser executable path.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Detail string.
        /// </summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>
        /// Whether a trusted localhost development certificate is available.
        /// </summary>
        public bool HasTrustedDevelopmentCertificate { get; set; }

        /// <summary>
        /// Trusted certificate detail string.
        /// </summary>
        public string CertificateDetail { get; set; } = string.Empty;
    }
}
