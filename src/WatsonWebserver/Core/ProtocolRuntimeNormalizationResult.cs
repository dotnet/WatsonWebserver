namespace WatsonWebserver.Core
{
    /// <summary>
    /// Result of normalizing protocol settings against runtime transport availability.
    /// </summary>
    public class ProtocolRuntimeNormalizationResult
    {
        /// <summary>
        /// Indicates whether HTTP/3 was disabled due to runtime unavailability.
        /// </summary>
        public bool Http3Disabled { get; set; } = false;

        /// <summary>
        /// Indicates whether Alt-Svc emission was disabled due to HTTP/3 unavailability.
        /// </summary>
        public bool AltSvcDisabled { get; set; } = false;
    }
}
