using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RegexMatcher;

namespace WatsonWebserver
{
    /// <summary>
    /// Dynamic route manager.  Dynamic routes are used for requests using any HTTP method to any path that can be matched by regular expression.
    /// </summary>
    public class DynamicRouteManager
    {
        #region Public-Members

        /// <summary>
        /// Directly access the underlying regular expression matching library.
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
        private Dictionary<DynamicRoute, Func<HttpContext, Task>> _Routes = new Dictionary<DynamicRoute, Func<HttpContext, Task>>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public DynamicRouteManager()
        {
            _Matcher.MatchPreference = MatchPreferenceType.LongestFirst;
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
        public void Add(HttpMethod method, Regex path, Func<HttpContext, Task> handler, string guid = null, object metadata = null)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_Lock)
            {
                DynamicRoute dr = new DynamicRoute(method, path, handler);

                _Matcher.Add(
                    new Regex(BuildConsolidatedRegex(method, path)),
                    dr);

                _Routes.Add(new DynamicRoute(method, path, handler, guid, metadata), handler);
            }
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod method, Regex path)
        { 
            if (path == null) throw new ArgumentNullException(nameof(path));

            lock (_Lock)
            {
                _Matcher.Remove(
                    new Regex(BuildConsolidatedRegex(method, path)));

                if (_Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path)))
                {
                    List<DynamicRoute> removeList = _Routes.Where(r => r.Key.Method == method && r.Key.Path.Equals(path))
                        .Select(r => r.Key)
                        .ToList();

                    foreach (DynamicRoute remove in removeList)
                    {
                        _Routes.Remove(remove);
                    }
                }
            }
        }

        /// <summary>
        /// Check if a content route exists.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(HttpMethod method, Regex path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));

            lock (_Lock)
            {
                return _Routes.Any(r => r.Key.Method == method && r.Key.Path.Equals(path));
            }
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="rawUrl">URL path.</param>
        /// <param name="dr">Matching route.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpContext, Task> Match(HttpMethod method, string rawUrl, out DynamicRoute dr)
        {
            dr = null;
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            object val = null;

            if (_Matcher.Match(
                BuildConsolidatedRegex(method, rawUrl),
                out val))
            {
                if (val == null)
                {
                    return null;
                }
                else
                {
                    lock (_Lock)
                    {
                        dr = (DynamicRoute)val;
                        return dr.Handler;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private string BuildConsolidatedRegex(HttpMethod method, string rawUrl)
        {
            rawUrl = rawUrl.Replace("^", "");
            return method.ToString() + " " + rawUrl;
        }

        private string BuildConsolidatedRegex(HttpMethod method, Regex path)
        {
            string pathString = path.ToString().Replace("^", "");
            return method.ToString() + " " + pathString;
        }

        #endregion
    }
}
