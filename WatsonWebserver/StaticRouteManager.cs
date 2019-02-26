using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    internal class StaticRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager Logging;
        private bool Debug;
        private List<StaticRoute> Routes;
        private readonly object RouteLock;

        #endregion

        #region Constructors-and-Factories

        public StaticRouteManager(LoggingManager logging, bool debug)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            Logging = logging;
            Debug = debug;
            Routes = new List<StaticRoute>();
            RouteLock = new object();
        }

        #endregion

        #region Public-Methods

        public void Add(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            StaticRoute r = new StaticRoute(method, path, handler);
            Add(r);
        }

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
                lock (RouteLock)
                {
                    Routes.Remove(r);
                }
                 
                return;
            }
        }

        public StaticRoute Get(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                StaticRoute curr = Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
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

        public bool Exists(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
             
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                StaticRoute curr = Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
                if (curr == null || curr == default(StaticRoute))
                { 
                    return false;
                }
            }
             
            return true;
        }

        public Func<HttpRequest, HttpResponse> Match(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                StaticRoute curr = Routes.FirstOrDefault(i => i.Method == method && i.Path == path);
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

            lock (RouteLock)
            {
                Routes.Add(route); 
            }
        }

        private void Remove(StaticRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));

            lock (RouteLock)
            {
                Routes.Remove(route);
            }
             
            return;
        }

        #endregion
    }
}
