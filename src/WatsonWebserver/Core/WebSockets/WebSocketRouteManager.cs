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
        /// <exception cref="ArgumentException">Thrown when a parameterized path contains an invalid catch-all.  The route is not added.</exception>
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
        /// Static routes are checked first, then parameterized routes without a catch-all, then catch-all routes ({*name}).
        /// The request path is normalized (lowercased, trailing slash added) before matching, so captured values are lowercase.
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

                if (_ParameterRoutes.Count > 0)
                {
                    Matcher matcher = new Matcher(normalizedPath);

                    // routes without a catch-all first, then catch-all routes, each in registration order
                    for (int pass = 0; pass < 2; pass++)
                    {
                        bool catchAllPass = pass == 1;

                        for (int i = 0; i < _ParameterRoutes.Count; i++)
                        {
                            WebSocketRoute candidate = _ParameterRoutes[i];
                            UrlPattern pattern = candidate.Pattern;
                            if (pattern == null || pattern.IsCatchAll != catchAllPass) continue;

                            if (matcher.Match(pattern, out parameters))
                            {
                                route = candidate;
                                return candidate.Handler;
                            }
                        }
                    }
                }
            }

            parameters = null;
            route = null;
            return null;
        }
    }
}
