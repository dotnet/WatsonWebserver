namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Threading.Tasks;
    using UrlMatcher;

    /// <summary>
    /// WebSocket route manager supporting static and parameterized paths.
    /// </summary>
    public class WebSocketRouteManager
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<string, WebSocketRoute> _StaticRoutes = new Dictionary<string, WebSocketRoute>(StringComparer.Ordinal);
        private readonly List<WebSocketRoute> _ParameterRoutes = new List<WebSocketRoute>();

        /// <summary>
        /// Add a route.
        /// </summary>
        public void Add(string path, Func<HttpContextBase, WebSocketSession, Task> handler, object metadata = null)
        {
            WebSocketRoute route = new WebSocketRoute(path, handler, metadata);

            lock (_Sync)
            {
                if (!route.IsParameterized)
                {
                    if (_StaticRoutes.ContainsKey(route.Path))
                    {
                        throw new InvalidOperationException("A WebSocket route already exists for path '" + route.Path + "'.");
                    }

                    _StaticRoutes[route.Path] = route;
                    return;
                }

                if (_ParameterRoutes.Any(r => String.Equals(r.Path, route.Path, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("A parameterized WebSocket route already exists for path '" + route.Path + "'.");
                }

                _ParameterRoutes.Add(route);
            }
        }

        /// <summary>
        /// Retrieve all routes.
        /// </summary>
        public IReadOnlyList<WebSocketRoute> GetAll()
        {
            lock (_Sync)
            {
                return _StaticRoutes.Values.Concat(_ParameterRoutes).ToArray();
            }
        }

        /// <summary>
        /// Match a request path to a route.
        /// </summary>
        public Func<HttpContextBase, WebSocketSession, Task> Match(string path, out NameValueCollection parameters, out WebSocketRoute route)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            string normalizedPath = UrlDetails.NormalizeRawPathForRouting(path);

            lock (_Sync)
            {
                if (_StaticRoutes.TryGetValue(normalizedPath, out route))
                {
                    parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    return route.Handler;
                }

                for (int i = 0; i < _ParameterRoutes.Count; i++)
                {
                    WebSocketRoute candidate = _ParameterRoutes[i];
                    if (Matcher.Match(normalizedPath, candidate.Path, out parameters))
                    {
                        route = candidate;
                        return candidate.Handler;
                    }
                }
            }

            parameters = null;
            route = null;
            return null;
        }
    }
}
