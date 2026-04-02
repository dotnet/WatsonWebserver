namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// WebSocket route definition.
    /// </summary>
    public class WebSocketRoute
    {
        /// <summary>
        /// Route identifier.
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Route path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Indicates whether the route contains parameters.
        /// </summary>
        public bool IsParameterized { get; }

        /// <summary>
        /// Route handler.
        /// </summary>
        public Func<HttpContextBase, WebSocketSession, Task> Handler { get; }

        /// <summary>
        /// User metadata.
        /// </summary>
        public object Metadata { get; set; }

        /// <summary>
        /// Instantiate the route.
        /// </summary>
        public WebSocketRoute(string path, Func<HttpContextBase, WebSocketSession, Task> handler, object metadata = null)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            IsParameterized = path.IndexOf('{') >= 0 && path.IndexOf('}') > path.IndexOf('{');
            Path = IsParameterized ? path : UrlDetails.NormalizeRawPathForRouting(path);
            Handler = handler;
            Metadata = metadata;
        }
    }
}
