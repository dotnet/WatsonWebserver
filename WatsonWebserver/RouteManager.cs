using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    public class RouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager Logging;
        private List<Route> Routes;
        private readonly object RouteLock;

        #endregion

        #region Constructors-and-Factories

        public RouteManager(LoggingManager logging)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            Logging = logging;

            Routes = new List<Route>();
            RouteLock = new object();
        }

        #endregion

        #region Public-Methods

        public void Add(string verb, string path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Route r = new Route(verb, path, handler);
            Add(r);
        }

        public void Remove(string verb, string path)
        { 
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            Route r = Get(verb, path);
            if (r == null || r == default(Route))
            {
                Logging.Log("Route " + verb + " " + path + " does not exist");
                return;
            }
            else
            {
                lock (RouteLock)
                {
                    Routes.Remove(r);
                }

                Logging.Log("Route " + verb + " " + path + " removed");
                return;
            }
        }

        public Route Get(string verb, string path)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
             
            verb = verb.ToLower();
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                Route curr = Routes.FirstOrDefault(i => i.Verb == verb && i.Path == path);
                if (curr == null || curr == default(Route))
                {
                    Logging.Log("Route " + verb + " " + path + " does not exist");
                    return null;
                } 
                else
                {
                    Logging.Log("Route " + verb + " " + path + " exists, returning");
                    return curr;
                }
            }
        }

        public bool Exists(string verb, string path)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
             
            verb = verb.ToLower();
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                Route curr = Routes.FirstOrDefault(i => i.Verb == verb && i.Path == path);
                if (curr == null || curr == default(Route))
                {
                    Logging.Log("Route " + verb + " " + path + " does not exist");
                    return false;
                }
            }

            Logging.Log("Route " + verb + " " + path + " exists");
            return true;
        }

        #endregion

        #region Private-Methods

        private void Add(Route route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));

            route.Verb = route.Verb.ToLower();
            route.Path = route.Path.ToLower();
            if (!route.Path.StartsWith("/")) route.Path = "/" + route.Path;
            if (!route.Path.EndsWith("/")) route.Path = route.Path + "/";

            if (Exists(route.Verb, route.Path))
            {
                Logging.Log("*** Route already exists for " + route.Verb + " " + route.Path);
                return;
            }

            lock (RouteLock)
            {
                Routes.Add(route);
                Logging.Log("Added route for " + route.Verb + " " + route.Path);
            }
        }

        private void Remove(Route route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));

            lock (RouteLock)
            {
                Routes.Remove(route);
            }

            Logging.Log("Route " + route.Verb + " " + route.Path + " removed");
            return;
        }

        #endregion
    }
}
