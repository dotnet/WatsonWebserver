namespace WatsonWebserver.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// HTTP/1.1 WebSocket handshake helpers.
    /// </summary>
    internal static class Http1WebSocketHandshake
    {
        internal static bool TryValidate(
            WebserverSettings settings,
            HttpContextBase context,
            out int statusCode,
            out string reason,
            out Dictionary<string, string> responseHeaders,
            out string acceptKey)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (context == null) throw new ArgumentNullException(nameof(context));

            statusCode = 400;
            reason = null;
            responseHeaders = null;
            acceptKey = null;

            if (!settings.WebSockets.Enable || !settings.WebSockets.EnableHttp1)
            {
                reason = "WebSocket support is disabled.";
                return false;
            }

            if (context.Protocol != HttpProtocol.Http1)
            {
                statusCode = 505;
                reason = "WebSockets are currently supported for HTTP/1.1 only.";
                return false;
            }

            if (context.Request.Method != HttpMethod.GET)
            {
                statusCode = 405;
                reason = "WebSocket upgrades require GET.";
                return false;
            }

            if (!String.Equals(context.Request.ProtocolVersion, "HTTP/1.1", StringComparison.OrdinalIgnoreCase))
            {
                statusCode = 505;
                reason = "WebSocket upgrades require HTTP/1.1.";
                return false;
            }

            string connection = context.Request.RetrieveHeaderValue(WebserverConstants.HeaderConnection);
            if (!WebSocketProtocolDetector.HeaderContainsToken(connection, "upgrade"))
            {
                reason = "Missing Connection: Upgrade header.";
                return false;
            }

            string upgrade = context.Request.RetrieveHeaderValue("Upgrade");
            if (!WebSocketProtocolDetector.HeaderContainsToken(upgrade, "websocket"))
            {
                reason = "Missing Upgrade: websocket header.";
                return false;
            }

            string version = context.Request.RetrieveHeaderValue("Sec-WebSocket-Version");
            if (!WebSocketHandshakeUtilities.IsSupportedVersion(settings.WebSockets.SupportedVersions, version))
            {
                statusCode = 426;
                reason = "Unsupported WebSocket version.";
                responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Sec-WebSocket-Version", String.Join(", ", settings.WebSockets.SupportedVersions) }
                };
                return false;
            }

            string key = context.Request.RetrieveHeaderValue("Sec-WebSocket-Key");
            if (!IsValidRequestKey(key))
            {
                reason = "Missing or invalid Sec-WebSocket-Key header.";
                return false;
            }

            acceptKey = WebSocketHandshakeUtilities.ComputeAcceptKey(key);
            return true;
        }

        internal static async Task SendUpgradeResponseAsync(Stream stream, string acceptKey, string subprotocol, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (String.IsNullOrWhiteSpace(acceptKey)) throw new ArgumentNullException(nameof(acceptKey));

            StringBuilder builder = new StringBuilder();
            builder.Append("HTTP/1.1 101 Switching Protocols\r\n");
            builder.Append("Connection: Upgrade\r\n");
            builder.Append("Upgrade: websocket\r\n");
            builder.Append("Sec-WebSocket-Accept: ").Append(acceptKey).Append("\r\n");
            if (!String.IsNullOrWhiteSpace(subprotocol))
            {
                builder.Append("Sec-WebSocket-Protocol: ").Append(subprotocol).Append("\r\n");
            }

            builder.Append("\r\n");
            byte[] bytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        internal static async Task SendFailureResponseAsync(
            Stream stream,
            int statusCode,
            string reason,
            IDictionary<string, string> headers,
            CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            StringBuilder builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(GetReasonPhrase(statusCode)).Append("\r\n");
            builder.Append("Content-Length: 0\r\n");
            builder.Append("Connection: close\r\n");
            builder.Append("Date: ").Append(DateTime.UtcNow.ToString(WebserverConstants.HeaderDateValueFormat)).Append("\r\n");

            if (headers != null)
            {
                foreach (KeyValuePair<string, string> header in headers)
                {
                    if (String.IsNullOrWhiteSpace(header.Key)) continue;
                    builder.Append(header.Key).Append(": ").Append(header.Value ?? String.Empty).Append("\r\n");
                }
            }

            builder.Append("\r\n");

            byte[] bytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        private static bool IsValidRequestKey(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return false;

            try
            {
                byte[] bytes = Convert.FromBase64String(key.Trim());
                return bytes.Length == 16;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string GetReasonPhrase(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad Request",
                405 => "Method Not Allowed",
                426 => "Upgrade Required",
                505 => "HTTP Version Not Supported",
                _ => "Bad Request"
            };
        }
    }
}
