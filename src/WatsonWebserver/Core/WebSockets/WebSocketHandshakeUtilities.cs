namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Shared WebSocket handshake helpers.
    /// </summary>
    public static class WebSocketHandshakeUtilities
    {
        /// <summary>
        /// RFC 6455 GUID used to compute the accept key.
        /// </summary>
        public const string AcceptKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// Supported WebSocket protocol version for Watson v1.
        /// </summary>
        public const string SupportedVersion = "13";

        /// <summary>
        /// Compute the RFC 6455 <c>Sec-WebSocket-Accept</c> header value.
        /// </summary>
        /// <param name="requestKey">Client-supplied <c>Sec-WebSocket-Key</c> header value.</param>
        /// <returns>Accept key.</returns>
        public static string ComputeAcceptKey(string requestKey)
        {
            if (String.IsNullOrWhiteSpace(requestKey)) throw new ArgumentNullException(nameof(requestKey));

            byte[] bytes = Encoding.ASCII.GetBytes(requestKey.Trim() + AcceptKeyGuid);
#if NET8_0_OR_GREATER
            return Convert.ToBase64String(SHA1.HashData(bytes));
#else
            using (SHA1 sha1 = SHA1.Create())
            {
                return Convert.ToBase64String(sha1.ComputeHash(bytes));
            }
#endif
        }

        /// <summary>
        /// Determine whether the requested version is supported by the configured version list.
        /// </summary>
        /// <param name="supportedVersions">Configured versions.</param>
        /// <param name="requestedVersion">Requested version.</param>
        /// <returns>True if supported.</returns>
        public static bool IsSupportedVersion(IEnumerable<string> supportedVersions, string requestedVersion)
        {
            if (supportedVersions == null) return false;
            if (String.IsNullOrWhiteSpace(requestedVersion)) return false;

            return supportedVersions.Any(v => String.Equals(v?.Trim(), requestedVersion.Trim(), StringComparison.Ordinal));
        }

        /// <summary>
        /// Parse the requested subprotocol list.
        /// </summary>
        /// <param name="headerValue">Raw header value.</param>
        /// <returns>Ordered requested subprotocols.</returns>
        public static IReadOnlyList<string> ParseSubprotocols(string headerValue)
        {
            if (String.IsNullOrWhiteSpace(headerValue)) return Array.Empty<string>();

            return headerValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !String.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
