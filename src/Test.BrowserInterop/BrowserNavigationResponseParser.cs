namespace Test.BrowserInterop
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Parses Chromium CDP response-received events into typed observations.
    /// </summary>
    internal static class BrowserNavigationResponseParser
    {
        /// <summary>
        /// Parse a CDP response-received event payload.
        /// </summary>
        /// <param name="payload">Event payload JSON.</param>
        /// <returns>Navigation observation.</returns>
        public static BrowserNavigationObservation Parse(string payload)
        {
            if (String.IsNullOrEmpty(payload)) throw new ArgumentNullException(nameof(payload));

            BrowserNavigationResponseReceivedPayload navigationPayload = JsonSerializer.Deserialize<BrowserNavigationResponseReceivedPayload>(payload);
            if (navigationPayload == null)
            {
                return null;
            }

            if (!String.Equals(navigationPayload.Type, "Document", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (navigationPayload.Response == null)
            {
                return null;
            }

            BrowserNavigationObservation observation = new BrowserNavigationObservation();
            observation.Url = navigationPayload.Response.Url ?? String.Empty;
            observation.StatusCode = navigationPayload.Response.Status;
            observation.Protocol = navigationPayload.Response.Protocol ?? String.Empty;
            observation.AltSvcHeader = GetHeaderValue(navigationPayload.Response.Headers, "alt-svc");
            return observation;
        }

        private static string GetHeaderValue(Dictionary<string, string> headers, string name)
        {
            if (headers == null) return String.Empty;
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (headers.TryGetValue(name, out string value))
            {
                return value ?? String.Empty;
            }

            return String.Empty;
        }
    }
}
