using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Static route manager.  Static routes are used for requests using any HTTP method to a specific path.
    /// </summary>
    public class StaticRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager _Logging;
        private bool _Debug;
        private List<StaticRoute> _Routes;
        private readonly object _Lock;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="logging">Logging instance.</param>
        /// <param name="debug">Enable or disable debugging.</param>
        public StaticRouteManager(LoggingManager logging, bool debug)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Logging = logging;
            _Debug = debug;
            _Routes = new List<StaticRoute>();
            _Lock = new object();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="handler">Method to invoke.</param>
        public void Add(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            StaticRoute r = new StaticRoute(method, path, handler);
            Add(r);
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod method, string path)
        { 
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            StaticRoute r = Get(method, path);
            if (r == null || r == default(StaticRoute))
            { 
                return;
            }
            else
            {
                lock (_Lock)
                {
                    _Routes.Remove(r);
                }
                 
                return;
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
            
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (_Lock)
            {
                StaticRoute curr = _Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
                if (curr == null || curr == default(StaticRoute))
                {
                    return null;
                }
                else
                {
                    return curr;
                }
            }
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
             
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (_Lock)
            {
                StaticRoute curr = _Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
                if (curr == null || curr == default(StaticRoute))
                { 
                    return false;
                }
            }
             
            return true;
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpRequest, HttpResponse> Match(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (_Lock)
            {
                StaticRoute curr = _Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
                if (curr == null || curr == default(StaticRoute))
                {
                    return null;
                }
                else
                {
                    return curr.Handler;
                }
            }
        }

        #endregion

        #region Private-Methods

        private void Add(StaticRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            
            route.Path = route.Path.ToLower();
            if (!route.Path.StartsWith("/")) route.Path = "/" + route.Path;
            if (!route.Path.EndsWith("/")) route.Path = route.Path + "/";

            if (Exists(route.Method, route.Path))
            { 
                return;
            }

            lock (_Lock)
            {
                _Routes.Add(route); 
            }
        }

        private void Remove(StaticRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));

            lock (_Lock)
            {
                _Routes.Remove(route);
            }
             
            return;
        }

        #endregion
    }
}
