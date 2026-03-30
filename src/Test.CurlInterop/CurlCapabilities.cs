namespace Test.CurlInterop
{
    /// <summary>
    /// curl capability summary.
    /// </summary>
    internal class CurlCapabilities
    {
        /// <summary>
        /// Whether curl is available.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether the binary reports HTTP/2 support.
        /// </summary>
        public bool SupportsHttp2 { get; set; }

        /// <summary>
        /// Whether the binary reports HTTP/3 support.
        /// </summary>
        public bool SupportsHttp3 { get; set; }

        /// <summary>
        /// Whether the binary reports Alt-Svc support.
        /// </summary>
        public bool SupportsAltSvc { get; set; }

        /// <summary>
        /// Raw version output.
        /// </summary>
        public string VersionOutput { get; set; } = string.Empty;
    }
}
