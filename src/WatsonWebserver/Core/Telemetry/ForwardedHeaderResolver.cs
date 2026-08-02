namespace WatsonWebserver.Core.Telemetry
{
    using System;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Resolves the logical client address and scheme from forwarded headers when Watson runs behind a
    /// trusted proxy. The raw socket peer is always available separately; this type only affects the
    /// span's client.address and url.scheme attributes and never influences a security decision.
    /// </summary>
    public static class ForwardedHeaderResolver
    {
        #region Public-Methods

        /// <summary>
        /// Resolve the client address for the request. When forwarded-header trust is disabled the raw
        /// socket peer address is returned. When enabled, the configured forwarded-for header is walked
        /// from the rightmost entry over trusted proxies, up to the configured hop limit, and the first
        /// untrusted entry is returned.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="resolvedFromHeader">Set to true when the address came from a forwarded header.</param>
        /// <returns>Client address, or null when unavailable.</returns>
        public static string ResolveClientAddress(HttpContextBase ctx, TelemetrySettings settings, out bool resolvedFromHeader)
        {
            resolvedFromHeader = false;
            if (ctx == null || settings == null) return null;

            string peer = ctx.Request?.Source?.IpAddress;

            if (!settings.TrustForwardedHeaders) return peer;

            string headerValue = ctx.Request?.RetrieveHeaderValue(settings.ForwardedForHeader);
            if (String.IsNullOrWhiteSpace(headerValue)) return peer;

            string[] entries = headerValue.Split(',');
            if (entries.Length == 0) return peer;

            string result = peer;
            int hops = 0;
            int maxHops = settings.ForwardLimit < 1 ? 1 : settings.ForwardLimit;

            for (int i = entries.Length - 1; i >= 0 && hops < maxHops; i--)
            {
                string candidate = entries[i].Trim();
                if (String.IsNullOrEmpty(candidate)) break;

                result = candidate;
                resolvedFromHeader = true;
                hops++;

                if (!IsTrustedProxy(settings, candidate)) break;
            }

            return result;
        }

        /// <summary>
        /// Resolve the URL scheme, honoring the configured forwarded-proto header when forwarded-header
        /// trust is enabled and the header is present.
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <param name="settings">Telemetry settings.</param>
        /// <param name="defaultScheme">Scheme to use when no trusted forwarded value is present.</param>
        /// <returns>Resolved scheme.</returns>
        public static string ResolveScheme(HttpContextBase ctx, TelemetrySettings settings, string defaultScheme)
        {
            if (ctx == null || settings == null || !settings.TrustForwardedHeaders) return defaultScheme;

            string headerValue = ctx.Request?.RetrieveHeaderValue(settings.ForwardedProtoHeader);
            if (String.IsNullOrWhiteSpace(headerValue)) return defaultScheme;

            string[] entries = headerValue.Split(',');
            string first = entries[0].Trim();
            if (String.IsNullOrEmpty(first)) return defaultScheme;

            return first.ToLowerInvariant();
        }

        #endregion

        #region Private-Methods

        private static bool IsTrustedProxy(TelemetrySettings settings, string ip)
        {
            if (settings.TrustedProxies == null) return false;
            if (String.IsNullOrEmpty(ip)) return false;

            try
            {
                return settings.TrustedProxies.MatchExists(ip);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}
