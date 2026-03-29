namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;

    /// <summary>
    /// Reduced immutable request metadata retained after a WebSocket upgrade.
    /// </summary>
    public class WebSocketRequestDescriptor
    {
        /// <summary>
        /// Request path without the querystring.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Normalized request path.
        /// </summary>
        public string NormalizedPath { get; }

        /// <summary>
        /// Request query values.
        /// </summary>
        public IReadOnlyDictionary<string, string> Query { get; }

        /// <summary>
        /// Request headers captured at handshake time.
        /// </summary>
        public NameValueCollection Headers { get; }

        /// <summary>
        /// Requested WebSocket version.
        /// </summary>
        public string RequestedVersion { get; }

        /// <summary>
        /// Requested subprotocols.
        /// </summary>
        public IReadOnlyList<string> RequestedSubprotocols { get; }

        /// <summary>
        /// Remote IP address.
        /// </summary>
        public string RemoteIp { get; }

        /// <summary>
        /// Remote TCP port.
        /// </summary>
        public int RemotePort { get; }

        /// <summary>
        /// Instantiate the descriptor.
        /// </summary>
        public WebSocketRequestDescriptor(
            string path,
            NameValueCollection headers,
            IReadOnlyDictionary<string, string> query,
            string requestedVersion,
            IReadOnlyList<string> requestedSubprotocols,
            string remoteIp,
            int remotePort)
        {
            Path = String.IsNullOrWhiteSpace(path) ? "/" : path;
            NormalizedPath = UrlDetails.NormalizeRawPathForRouting(Path);
            Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            if (headers != null)
            {
                string[] headerKeys = headers.AllKeys;
                if (headerKeys != null)
                {
                    for (int i = 0; i < headerKeys.Length; i++)
                    {
                        string key = headerKeys[i];
                        if (String.IsNullOrWhiteSpace(key)) continue;

                        string[] values = headers.GetValues(key);
                        if (values == null || values.Length < 1)
                        {
                            Headers.Add(key, headers.Get(key));
                            continue;
                        }

                        for (int j = 0; j < values.Length; j++)
                        {
                            Headers.Add(key, values[j]);
                        }
                    }
                }
            }
            Query = query ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            RequestedVersion = requestedVersion;
            RequestedSubprotocols = requestedSubprotocols ?? Array.Empty<string>();
            RemoteIp = remoteIp ?? String.Empty;
            RemotePort = remotePort;
        }

        /// <summary>
        /// Create a descriptor from an HTTP context.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <returns>Descriptor.</returns>
        public static WebSocketRequestDescriptor FromHttpContext(HttpContextBase context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            Dictionary<string, string> query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (context.Request?.Query?.Elements != null)
            {
                string[] keys = context.Request.Query.Elements.AllKeys;
                if (keys != null)
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        string key = keys[i];
                        if (String.IsNullOrWhiteSpace(key)) continue;
                        query[key] = context.Request.Query.Elements.Get(key);
                    }
                }
            }

            return new WebSocketRequestDescriptor(
                context.Request?.Url?.RawWithoutQuery,
                context.Request?.Headers,
                query,
                context.Request?.RetrieveHeaderValue("Sec-WebSocket-Version"),
                WebSocketHandshakeUtilities.ParseSubprotocols(context.Request?.RetrieveHeaderValue("Sec-WebSocket-Protocol")),
                context.Request?.Source?.IpAddress,
                context.Request?.Source?.Port ?? 0);
        }
    }
}
