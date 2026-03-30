namespace Test.BrowserInterop
{
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Development certificate lookup result.
    /// </summary>
    internal class BrowserDevelopmentCertificateInfo
    {
        /// <summary>
        /// Whether a development certificate was found.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether the development certificate is trusted.
        /// </summary>
        public bool IsTrusted { get; set; }

        /// <summary>
        /// Development certificate instance.
        /// </summary>
        public X509Certificate2 Certificate { get; set; } = null;

        /// <summary>
        /// Detail string.
        /// </summary>
        public string Detail { get; set; } = string.Empty;
    }
}
