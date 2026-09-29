namespace WatsonWebserver.Core.Routing
{
    using WatsonWebserver.Core;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using UrlMatcher;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Parameter route manager.  Parameter routes are used for requests using any HTTP method to any path where parameters are defined in the URL.
    /// For example, /{version}/api.
    /// For a matching URL, the HttpRequest.Url.Parameters will contain a key called 'version' with the value found in the URL.
    /// </summary>
    public class ParameterRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim();
        private Dictionary<ParameterRoute, Func<HttpContextBase, Task>> _Routes = new Dictionary<ParameterRoute, Func<HttpContextBase, Task>>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ParameterRouteManager()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path, i.e. /path/to/resource, /users/{id}, or /files/{*path} (catch-all, must be the last segment).</param>
        /// <param name="handler">Method to invoke.</param>
        /// <param name="exceptionHandler">The method that should be called to handle exceptions.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        /// <param name="openApiMetadata">OpenAPI documentation metadata.</param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty, or handler is null.</exception>
        /// <exception cref="ArgumentException">Thrown when path contains an invalid catch-all (not the entire last segment, or more than one). The route is not added.</exception>
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

            _Lock.EnterWriteLock();
            try
            {
                ParameterRoute pr = new ParameterRoute(method, path, handler, exceptionHandler, guid, metadata, openApiMetadata);
                _Routes.Add(pr, handler);
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Retrieve all routes.
        /// </summary>
        /// <returns>List of parameter routes.</returns>
        public IReadOnlyList<ParameterRoute> GetAll()
        {
            _Lock.EnterReadLock();
            try
            {
                return _Routes.Keys.ToList().AsReadOnly();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            _Lock.EnterWriteLock();
            try
            {
                if (_Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path)))
                {
                    List<ParameterRoute> removeList = _Routes.Where(r => r.Key.Method == method && r.Key.Path.Equals(path))
                        .Select(r => r.Key)
                        .ToList();

                    foreach (ParameterRoute remove in removeList)
                    {
                        _Routes.Remove(remove);
                    }
                }
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Retrieve a parameter route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>ParameterRoute if the route exists, otherwise null.</returns>
        public ParameterRoute Get(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            _Lock.EnterReadLock();
            try
            {
                if (_Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path)))
                {
                    return _Routes.First(r => r.Key.Method == method && r.Key.Path.Equals(path)).Key;
                }
            }
            finally
            {
                _Lock.ExitReadLock();
            }

            return null;
        }

        /// <summary>
        /// Check if a content route exists.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            _Lock.EnterReadLock();
            try
            {
                return _Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path));
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// Routes without a catch-all are evaluated first, in registration order, followed by catch-all routes
        /// (paths ending in {*name}), in registration order, so a catch-all never shadows a more specific route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <param name="vals">Values extracted from the URL.</param>
        /// <param name="pr">Matching route.</param>
        /// <returns>True if match exists.</returns>
        public Func<HttpContextBase, Task> Match(HttpMethod method, string path, out NameValueCollection vals, out ParameterRoute pr)
        {
            pr = null;
            vals = null;
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            Matcher matcher = new Matcher(BuildConsolidatedPath(method, path));

            _Lock.EnterReadLock();
            try
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    bool catchAllPass = pass == 1;

                    foreach (KeyValuePair<ParameterRoute, Func<HttpContextBase, Task>> route in _Routes)
                    {
                        UrlPattern pattern = route.Key.Pattern;
                        if (pattern == null || pattern.IsCatchAll != catchAllPass) continue;

                        if (matcher.Match(pattern, out vals))
                        {
                            pr = route.Key;
                            return route.Value;
                        }
                    }
                }
            }
            finally
            {
                _Lock.ExitReadLock();
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private string BuildConsolidatedPath(HttpMethod method, string path)
        {
            return method.ToString() + " " + path;
        }

        #endregion
    }
}
