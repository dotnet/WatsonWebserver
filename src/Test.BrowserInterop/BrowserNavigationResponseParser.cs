namespace Test.BrowserInterop
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Parses Chromium CDP response-received events into typed observations.
    /// </summary>
    internal static class BrowserNavigationResponseParser
    {
        /// <summary>
        /// Parse a CDP response-received event payload.
        /// </summary>
        /// <param name="payload">Event payload.</param>
        /// <returns>Navigation observation.</returns>
        public static BrowserNavigationObservation Parse(JsonElement payload)
        {
            BrowserNavigationObservation observation = new BrowserNavigationObservation();

            if (payload.TryGetProperty("type", out JsonElement typeElement))
            {
                if (!String.Equals(typeElement.GetString(), "Document", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            if (!payload.TryGetProperty("response", out JsonElement responseElement))
            {
                return null;
            }

            if (responseElement.TryGetProperty("url", out JsonElement urlElement))
            {
                observation.Url = urlElement.GetString() ?? String.Empty;
            }

            if (responseElement.TryGetProperty("status", out JsonElement statusElement))
            {
                observation.StatusCode = statusElement.GetInt32();
            }

            if (responseElement.TryGetProperty("protocol", out JsonElement protocolElement))
            {
                observation.Protocol = protocolElement.GetString() ?? String.Empty;
            }

            if (responseElement.TryGetProperty("headers", out JsonElement headersElement))
            {
                if (TryGetHeader(headersElement, "alt-svc", out string headerValue))
                {
                    observation.AltSvcHeader = headerValue;
                }
            }

            return observation;
        }

        private static bool TryGetHeader(JsonElement headersElement, string name, out string value)
        {
            value = String.Empty;

            JsonElement.ObjectEnumerator enumerator = headersElement.EnumerateObject();
            while (enumerator.MoveNext())
            {
                JsonProperty property = enumerator.Current;
                if (String.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value.GetString() ?? String.Empty;
                    return true;
                }
            }

            return false;
        }
    }
}
