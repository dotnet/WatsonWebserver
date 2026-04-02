namespace WatsonWebserver.Core.WebSockets
{
    using System;

    /// <summary>
    /// Detects WebSocket upgrade attempts from HTTP request metadata.
    /// </summary>
    public static class WebSocketProtocolDetector
    {
        /// <summary>
        /// Determine whether the request is attempting a WebSocket upgrade.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <returns>True if the request looks like a WebSocket upgrade attempt.</returns>
        public static bool IsWebSocketUpgradeRequest(HttpContextBase context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Protocol != HttpProtocol.Http1) return false;
            if (context.Request == null) return false;

            string upgrade = context.Request.RetrieveHeaderValue("Upgrade");
            string connection = context.Request.RetrieveHeaderValue(WebserverConstants.HeaderConnection);
            string version = context.Request.RetrieveHeaderValue("Sec-WebSocket-Version");
            string key = context.Request.RetrieveHeaderValue("Sec-WebSocket-Key");

            if (HeaderContainsToken(upgrade, "websocket")) return true;
            if (HeaderContainsToken(connection, "upgrade")) return true;
            if (!String.IsNullOrWhiteSpace(version)) return true;
            if (!String.IsNullOrWhiteSpace(key)) return true;
            return false;
        }

        /// <summary>
        /// Determine whether a comma-delimited header contains a token.
        /// </summary>
        /// <param name="headerValue">Header value.</param>
        /// <param name="token">Token to locate.</param>
        /// <returns>True if present.</returns>
        public static bool HeaderContainsToken(string headerValue, string token)
        {
            if (String.IsNullOrWhiteSpace(headerValue)) return false;
            if (String.IsNullOrWhiteSpace(token)) return false;

            string[] parts = headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (String.Equals(parts[i].Trim(), token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
