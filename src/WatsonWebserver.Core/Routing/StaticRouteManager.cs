namespace WatsonWebserver.Core.Routing
{
    using System;
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Static route manager.  Static routes are used for requests using any HTTP method to a specific path.
    /// </summary>
    public class StaticRouteManager
    {
        #region Private-Members

        private readonly object _Sync = new object();
        private readonly List<StaticRoute> _Routes = new List<StaticRoute>();
        private IReadOnlyList<StaticRoute> _RouteSnapshot = Array.Empty<StaticRoute>();
        private FrozenDictionary<HttpMethod, FrozenDictionary<string, StaticRoute>> _RouteMap =
            FrozenDictionary<HttpMethod, FrozenDictionary<string, StaticRoute>>.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public StaticRouteManager()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="handler">Method to invoke.</param>
        /// <param name="exceptionHandler">The method that should be called to handle exceptions.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        /// <param name="openApiMetadata">OpenAPI documentation metadata.</param>
        public void Add(
            HttpMethod method,
            string path,
            Func<HttpContextBase, Task> handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            Guid guid = default(Guid),
            object metadata = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            StaticRoute route = new StaticRoute(method, path, handler, exceptionHandler, guid, metadata, openApiMetadata);
            Add(route);
        }

        /// <summary>
        /// Retrieve all routes.
        /// </summary>
        /// <returns>List of static routes.</returns>
        public IReadOnlyList<StaticRoute> GetAll()
        {
            return _RouteSnapshot;
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            string normalizedPath = UrlDetails.NormalizeRawPathForRouting(path);

            lock (_Sync)
            {
                StaticRoute route = GetRouteInternal(method, normalizedPath);
                if (route == null || route == default(StaticRoute))
                {
                    return;
                }

                _Routes.Remove(route);
                RebuildSnapshots();
            }
        }

        /// <summary>
        /// Retrieve a static route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>StaticRoute if the route exists, otherwise null.</returns>
        public StaticRoute Get(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            return GetNormalized(method, UrlDetails.NormalizeRawPathForRouting(path));
        }

        /// <summary>
        /// Check if a static route exists.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            return GetNormalized(method, UrlDetails.NormalizeRawPathForRouting(path)) != null;
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <param name="route">Matching route.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpContextBase, Task> Match(HttpMethod method, string path, out StaticRoute route)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            return MatchNormalized(method, UrlDetails.NormalizeRawPathForRouting(path), out route);
        }

        /// <summary>
        /// Match a request method and already-normalized URL path to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="normalizedPath">Normalized URL path.</param>
        /// <param name="route">Matching route.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpContextBase, Task> MatchNormalized(HttpMethod method, string normalizedPath, out StaticRoute route)
        {
            route = GetNormalized(method, normalizedPath);
            return route != null ? route.Handler : null;
        }

        #endregion

        #region Private-Methods

        private void Add(StaticRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));

            route.Path = UrlDetails.NormalizeRawPathForRouting(route.Path);

            lock (_Sync)
            {
                StaticRoute existing = GetRouteInternal(route.Method, route.Path);
                if (existing != null && existing != default(StaticRoute))
                {
                    return;
                }

                _Routes.Add(route);
                RebuildSnapshots();
            }
        }

        private StaticRoute GetNormalized(HttpMethod method, string normalizedPath)
        {
            if (String.IsNullOrEmpty(normalizedPath)) return null;
            return GetRouteInternal(method, normalizedPath);
        }

        private StaticRoute GetRouteInternal(HttpMethod method, string normalizedPath)
        {
            FrozenDictionary<string, StaticRoute> routesByPath = null;
            if (!_RouteMap.TryGetValue(method, out routesByPath) || routesByPath == null)
            {
                return null;
            }

            StaticRoute route = null;
            if (!routesByPath.TryGetValue(normalizedPath, out route))
            {
                return null;
            }

            return route;
        }

        private void RebuildSnapshots()
        {
            Dictionary<HttpMethod, Dictionary<string, StaticRoute>> routeMap = new Dictionary<HttpMethod, Dictionary<string, StaticRoute>>();

            for (int i = 0; i < _Routes.Count; i++)
            {
                StaticRoute route = _Routes[i];
                if (route == null) continue;

                Dictionary<string, StaticRoute> routesByPath = null;
                if (!routeMap.TryGetValue(route.Method, out routesByPath) || routesByPath == null)
                {
                    routesByPath = new Dictionary<string, StaticRoute>(StringComparer.Ordinal);
                    routeMap[route.Method] = routesByPath;
                }

                routesByPath[route.Path] = route;
            }

            Dictionary<HttpMethod, FrozenDictionary<string, StaticRoute>> frozenByMethod = new Dictionary<HttpMethod, FrozenDictionary<string, StaticRoute>>();
            foreach (KeyValuePair<HttpMethod, Dictionary<string, StaticRoute>> entry in routeMap)
            {
                frozenByMethod[entry.Key] = entry.Value.ToFrozenDictionary(StringComparer.Ordinal);
            }

            _RouteSnapshot = _Routes.ToArray();
            _RouteMap = frozenByMethod.ToFrozenDictionary();
        }

        #endregion
    }
}
