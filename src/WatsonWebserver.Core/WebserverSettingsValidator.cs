namespace WatsonWebserver.Core
{
    using System;
    using WatsonWebserver.Core.Http3;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Validates webserver settings against the current transport capability matrix.
    /// </summary>
    public static class WebserverSettingsValidator
    {
        /// <summary>
        /// Normalize protocol settings for the current runtime.
        /// </summary>
        /// <param name="settings">Settings to normalize.</param>
        /// <param name="http3Availability">Detected HTTP/3 runtime availability.</param>
        /// <param name="logger">Optional logger for warnings.</param>
        /// <returns>Normalization result.</returns>
        public static ProtocolRuntimeNormalizationResult NormalizeForRuntime(
            WebserverSettings settings,
            Http3RuntimeAvailability http3Availability,
            Action<string> logger)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Protocols == null) throw new WebserverConfigurationException("Protocol settings are required.");
            if (settings.AltSvc == null) throw new WebserverConfigurationException("Alt-Svc settings are required.");
            if (http3Availability == null) throw new ArgumentNullException(nameof(http3Availability));

            ProtocolRuntimeNormalizationResult result = new ProtocolRuntimeNormalizationResult();

            if (settings.Protocols.EnableHttp3 && !http3Availability.IsAvailable)
            {
                settings.Protocols.EnableHttp3 = false;
                result.Http3Disabled = true;

                if (settings.AltSvc.Enabled)
                {
                    settings.AltSvc.Enabled = false;
                    result.AltSvcDisabled = true;
                }

                if (logger != null)
                {
                    logger.Invoke("HTTP/3 is enabled but QUIC is unavailable. HTTP/3 has been disabled for this server start. " + http3Availability.Message);

                    if (result.AltSvcDisabled)
                    {
                        logger.Invoke("Alt-Svc emission has been disabled because HTTP/3 is unavailable for this server start.");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Validate the supplied settings.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        /// <param name="supportsHttp2">True if the current transport supports HTTP/2.</param>
        /// <param name="supportsHttp3">True if the current transport supports HTTP/3.</param>
        public static void Validate(WebserverSettings settings, bool supportsHttp2, bool supportsHttp3)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Protocols == null) throw new WebserverConfigurationException("Protocol settings are required.");
            if (settings.Protocols.Http2 == null) throw new WebserverConfigurationException("HTTP/2 settings are required.");
            if (settings.Protocols.Http3 == null) throw new WebserverConfigurationException("HTTP/3 settings are required.");
            if (settings.WebSockets == null) throw new WebserverConfigurationException("WebSocket settings are required.");

            if (!settings.Protocols.EnableHttp1
                && !settings.Protocols.EnableHttp2
                && !settings.Protocols.EnableHttp3)
            {
                throw new WebserverConfigurationException("At least one protocol must be enabled.");
            }

            if (settings.Protocols.EnableHttp2 && !supportsHttp2)
            {
                throw new WebserverConfigurationException("HTTP/2 is enabled, but this server implementation currently supports HTTP/1.1 only.");
            }

            if (settings.Protocols.EnableHttp3 && !supportsHttp3)
            {
                throw new WebserverConfigurationException("HTTP/3 is enabled, but this server implementation currently supports HTTP/1.1 only.");
            }

            if (settings.Protocols.EnableHttp2 && !settings.Ssl.Enable && !settings.Protocols.EnableHttp2Cleartext)
            {
                throw new WebserverConfigurationException("HTTP/2 is enabled without TLS. Cleartext HTTP/2 (h2c) via prior knowledge is not supported in this configuration. Either enable TLS or disable HTTP/2.");
            }

            if (settings.Protocols.EnableHttp3 && !settings.Ssl.Enable)
            {
                throw new WebserverConfigurationException("HTTP/3 is enabled without TLS. HTTP/3 requires TLS and QUIC.");
            }

            if (settings.AltSvc != null && settings.AltSvc.Enabled && !settings.Protocols.EnableHttp3)
            {
                throw new WebserverConfigurationException("Alt-Svc emission is enabled but HTTP/3 is disabled. This will advertise a protocol the server cannot serve.");
            }

            ValidateWebSocketSettings(settings.WebSockets);
        }

        private static void ValidateWebSocketSettings(WebSocketSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.SupportedVersions == null || settings.SupportedVersions.Count < 1)
            {
                throw new WebserverConfigurationException("At least one WebSocket version must be configured.");
            }

            for (int i = 0; i < settings.SupportedVersions.Count; i++)
            {
                string version = settings.SupportedVersions[i];
                if (String.IsNullOrWhiteSpace(version))
                {
                    throw new WebserverConfigurationException("WebSocket versions cannot be null or whitespace.");
                }

                if (!String.Equals(version.Trim(), "13", StringComparison.Ordinal))
                {
                    throw new WebserverConfigurationException("Watson v1 supports WebSocket version 13 only.");
                }
            }
        }
    }
}
