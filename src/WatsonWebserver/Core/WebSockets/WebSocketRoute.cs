namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Threading.Tasks;
    using UrlMatcher;

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
        /// For parameterized routes, parameters are written {name} and a final catch-all is written {*name}.
        /// Setting an invalid pattern on a parameterized route (a catch-all that is not the entire last segment, or more than one catch-all) throws ArgumentException.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                _Pattern = IsParameterized && !String.IsNullOrEmpty(value) ? UrlPattern.Parse(value) : null;
                _Path = value;
            }
        }

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
        /// The parsed pattern for a parameterized route.  Null for static routes.
        /// </summary>
        internal UrlPattern Pattern
        {
            get
            {
                return _Pattern;
            }
        }

        private string _Path = null;
        private UrlPattern _Pattern = null;

        /// <summary>
        /// Instantiate the route.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when path is null or whitespace, or handler is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a parameterized path contains an invalid catch-all.</exception>
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
