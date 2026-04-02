namespace WatsonWebserver.Core.Http3
{
    /// <summary>
    /// QUIC runtime availability details for HTTP/3 startup decisions.
    /// </summary>
    public class Http3RuntimeAvailability
    {
        /// <summary>
        /// True if the QUIC runtime can be used by the current process.
        /// </summary>
        public bool IsAvailable { get; set; } = false;

        /// <summary>
        /// True if System.Net.Quic was located.
        /// </summary>
        public bool AssemblyPresent { get; set; } = false;

        /// <summary>
        /// Diagnostic message describing the outcome.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
