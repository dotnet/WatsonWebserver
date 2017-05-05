using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    internal class ContentRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager Logging;
        private bool Debug;
        private List<ContentRoute> Routes;
        private readonly object RouteLock;

        #endregion

        #region Constructors-and-Factories

        public ContentRouteManager(LoggingManager logging, bool debug)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            Logging = logging;
            Debug = debug;
            Routes = new List<ContentRoute>();
            RouteLock = new object();
        }

        #endregion

        #region Public-Methods

        public void Add(string path, bool isDirectory)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            ContentRoute r = new ContentRoute(path, isDirectory);
            Add(r);
        }

        public void Remove(string path)
        { 
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            ContentRoute r = Get(path);
            if (r == null || r == default(ContentRoute))
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

        public ContentRoute Get(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (RouteLock)
            {
                foreach (ContentRoute curr in Routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (path.StartsWith(curr.Path.ToLower())) return curr;
                    }
                    else
                    {
                        if (path.Equals(curr.Path.ToLower())) return curr;
                    }
                }

                return null;
            }
        }

        public bool Exists(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
             
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path; 

            lock (RouteLock)
            {
                foreach (ContentRoute curr in Routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (path.StartsWith(curr.Path.ToLower())) return true;
                    }
                    else
                    {
                        if (path.Equals(curr.Path.ToLower())) return true;
                    }
                }
            }
             
            return false;
        }
        
        #endregion

        #region Private-Methods

        private void Add(ContentRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            
            route.Path = route.Path.ToLower();
            if (!route.Path.StartsWith("/")) route.Path = "/" + route.Path;
            if (route.IsDirectory && !route.Path.EndsWith("/")) route.Path = route.Path + "/";

            if (Exists(route.Path))
            { 
                return;
            }

            lock (RouteLock)
            {
                Routes.Add(route); 
            }
        }

        private void Remove(ContentRoute route)
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
