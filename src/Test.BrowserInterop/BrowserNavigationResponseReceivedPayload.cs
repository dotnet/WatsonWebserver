namespace Test.BrowserInterop
{
    using System.Collections.Generic;

    /// <summary>
    /// Typed CDP payload for Network.responseReceived.
    /// </summary>
    internal class BrowserNavigationResponseReceivedPayload
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Response payload.
        /// </summary>
        public BrowserNavigationResponsePayload Response { get; set; } = null;
    }
}
