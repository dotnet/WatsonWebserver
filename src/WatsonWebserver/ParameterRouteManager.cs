using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UrlMatcher;

namespace WatsonWebserver
{
    /// <summary>
    /// Parameter route manager.  Parameter routes are used for requests using any HTTP method to any path where parameters are defined in the URL.
    /// For example, /{version}/api.
    /// For a matching URL, the HttpRequest.Url.Parameters will contain a key called 'version' with the value found in the URL.
    /// </summary>
    public class ParameterRouteManager
    {
        #region Public-Members

        /// <summary>
        /// Directly access the underlying URL matching library.
        /// This is helpful in case you want to specify the matching behavior should multiple matches exist.
        /// </summary>
        public Matcher Matcher
        {
            get
            {
                return _Matcher;
            }
        }

        #endregion

        #region Private-Members

        private Matcher _Matcher = new Matcher();
        private readonly object _Lock = new object();
        private Dictionary<ParameterRoute, Func<HttpContext, Task>> _Routes = new Dictionary<ParameterRoute, Func<HttpContext, Task>>();

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
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="handler">Method to invoke.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public void Add(HttpMethod method, string path, Func<HttpContext, Task> handler, string guid = null, object metadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_Lock)
            {
                ParameterRoute pr = new ParameterRoute(method, path, handler, guid, metadata);
                _Routes.Add(pr, handler);
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

            lock (_Lock)
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

            lock (_Lock)
            {
                if (_Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path)))
                {
                    return _Routes.First(r => r.Key.Method == method && r.Key.Path.Equals(path)).Key;
                }
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

            lock (_Lock)
            {
                return _Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path));
            }
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <param name="vals">Values extracted from the URL.</param>
        /// <param name="pr">Matching route.</param>
        /// <returns>True if match exists.</returns>
        public Func<HttpContext, Task> Match(HttpMethod method, string path, out Dictionary<string, string> vals, out ParameterRoute pr)
        {
            pr = null;
            vals = null;
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            string consolidatedPath = BuildConsolidatedPath(method, path);

            lock (_Lock)
            {
                foreach (KeyValuePair<ParameterRoute, Func<HttpContext, Task>> route in _Routes)
                {
                    if (_Matcher.Match(
                        consolidatedPath,
                        BuildConsolidatedPath(route.Key.Method, route.Key.Path), 
                        out vals))
                    {
                        pr = route.Key;
                        return route.Value;
                    }
                }
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
