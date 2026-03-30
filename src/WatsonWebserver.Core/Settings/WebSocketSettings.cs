namespace WatsonWebserver.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// WebSocket settings.
    /// </summary>
    public class WebSocketSettings
    {
        /// <summary>
        /// Minimum supported max-message size.
        /// </summary>
        public const int MinMaxMessageSize = 1024;

        /// <summary>
        /// Maximum supported max-message size.
        /// </summary>
        public const int MaxMaxMessageSize = 67108864;

        /// <summary>
        /// Minimum supported receive-buffer size.
        /// </summary>
        public const int MinReceiveBufferSize = 1024;

        /// <summary>
        /// Maximum supported receive-buffer size.
        /// </summary>
        public const int MaxReceiveBufferSize = 262144;

        /// <summary>
        /// Minimum supported close-handshake timeout in milliseconds.
        /// </summary>
        public const int MinCloseHandshakeTimeoutMs = 1000;

        /// <summary>
        /// Maximum supported close-handshake timeout in milliseconds.
        /// </summary>
        public const int MaxCloseHandshakeTimeoutMs = 60000;

        /// <summary>
        /// Enable WebSocket support.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Maximum supported whole-message size, in bytes.
        /// </summary>
        public int MaxMessageSize
        {
            get
            {
                return _MaxMessageSize;
            }
            set
            {
                _MaxMessageSize = Clamp(value, MinMaxMessageSize, MaxMaxMessageSize);
            }
        }

        /// <summary>
        /// Receive buffer size, in bytes.
        /// </summary>
        public int ReceiveBufferSize
        {
            get
            {
                return _ReceiveBufferSize;
            }
            set
            {
                _ReceiveBufferSize = Clamp(value, MinReceiveBufferSize, MaxReceiveBufferSize);
            }
        }

        /// <summary>
        /// Close-handshake timeout, in milliseconds.
        /// </summary>
        public int CloseHandshakeTimeoutMs
        {
            get
            {
                return _CloseHandshakeTimeoutMs;
            }
            set
            {
                _CloseHandshakeTimeoutMs = Clamp(value, MinCloseHandshakeTimeoutMs, MaxCloseHandshakeTimeoutMs);
            }
        }

        /// <summary>
        /// Allow the client to supply a session identifier through a request header.
        /// </summary>
        public bool AllowClientSuppliedGuid { get; set; } = false;

        /// <summary>
        /// Header name used for optional client-supplied session identifiers.
        /// </summary>
        public string ClientGuidHeaderName
        {
            get
            {
                return _ClientGuidHeaderName;
            }
            set
            {
                _ClientGuidHeaderName = String.IsNullOrWhiteSpace(value) ? "x-guid" : value.Trim();
            }
        }

        /// <summary>
        /// Supported WebSocket protocol versions.
        /// Watson v1 currently supports version 13 only.
        /// </summary>
        public List<string> SupportedVersions
        {
            get
            {
                return _SupportedVersions;
            }
            set
            {
                if (value == null || value.Count < 1)
                {
                    _SupportedVersions = new List<string> { "13" };
                    return;
                }

                _SupportedVersions = value
                    .Where(v => !String.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (_SupportedVersions.Count < 1)
                {
                    _SupportedVersions.Add("13");
                }
            }
        }

        /// <summary>
        /// Enable HTTP/1.1 WebSocket upgrades.
        /// </summary>
        public bool EnableHttp1 { get; set; } = true;

        /// <summary>
        /// Enable HTTP/2 WebSockets.
        /// Present for forward compatibility only in v1.
        /// </summary>
        public bool EnableHttp2 { get; set; } = false;

        /// <summary>
        /// Enable HTTP/3 WebSockets.
        /// Present for forward compatibility only in v1.
        /// </summary>
        public bool EnableHttp3 { get; set; } = false;

        private int _MaxMessageSize = 16777216;
        private int _ReceiveBufferSize = 65536;
        private int _CloseHandshakeTimeoutMs = 5000;
        private string _ClientGuidHeaderName = "x-guid";
        private List<string> _SupportedVersions = new List<string> { "13" };

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
