namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Builds Alt-Svc header values from explicit server settings.
    /// </summary>
    public static class AltSvcHeaderBuilder
    {
        /// <summary>
        /// Build an Alt-Svc header value.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <returns>Alt-Svc header value, or null when disabled.</returns>
        public static string Build(WebserverSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.AltSvc == null || !settings.AltSvc.Enabled) return null;
            if (settings.Protocols == null || !settings.Protocols.EnableHttp3) return null;
            if (String.IsNullOrWhiteSpace(settings.AltSvc.Http3Alpn)) throw new InvalidOperationException("Alt-Svc requires a non-empty HTTP/3 ALPN token.");

            int port = settings.AltSvc.Port > 0 ? settings.AltSvc.Port : settings.Port;
            string authority = BuildAuthority(settings.AltSvc.Authority, port);
            return settings.AltSvc.Http3Alpn.Trim() + "=\"" + authority + "\"; ma=" + settings.AltSvc.MaxAgeSeconds;
        }

        private static string BuildAuthority(string authority, int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            if (String.IsNullOrWhiteSpace(authority))
            {
                return ":" + port;
            }

            string trimmedAuthority = authority.Trim();
            if (trimmedAuthority.IndexOf(':') >= 0)
            {
                return trimmedAuthority;
            }

            return trimmedAuthority + ":" + port;
        }
    }
}
