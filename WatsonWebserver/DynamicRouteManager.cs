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

        #endregion

        #region Private-Members
         
        private Matcher _Matcher;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public DynamicRouteManager()
        { 
            _Matcher = new Matcher();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="handler">Method to invoke.</param>
        public void Add(HttpMethod method, Regex path, Func<HttpContext, Task> handler)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _Matcher.Add(
                new Regex(BuildConsolidatedRegex(method, path)), 
                handler);
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod method, Regex path)
        { 
            if (path == null) throw new ArgumentNullException(nameof(path));
            _Matcher.Remove(
                new Regex(BuildConsolidatedRegex(method, path)));
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
            return _Matcher.Exists(
                new Regex(BuildConsolidatedRegex(method, path)));
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="rawUrl">URL path.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpContext, Task> Match(HttpMethod method, string rawUrl)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            object val;
            Func<HttpContext, Task> handler;
            if (_Matcher.Match(
                BuildConsolidatedRegex(method, rawUrl), 
                out val))
            { 
                if (val == null) return null;
                handler = (Func<HttpContext, Task>)val;
                return handler;
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
