using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Content route manager.  Content routes are used for GET and HEAD requests to specific files or entire directories.
    /// </summary>
    public class ContentRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members
          
        private List<ContentRoute> _Routes;
        private readonly object _Lock;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ContentRouteManager()
        { 
            _Routes = new List<ContentRoute>();
            _Lock = new object();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="isDirectory">True if the path represents a directory.</param>
        public void Add(string path, bool isDirectory)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            ContentRoute r = new ContentRoute(path, isDirectory);
            Add(r);
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="path">URL path.</param>
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
                lock (_Lock)
                {
                    _Routes.Remove(r);
                }
                 
                return;
            }
        }

        /// <summary>
        /// Retrieve a content route.
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <returns>ContentRoute if the route exists, otherwise null.</returns>
        public ContentRoute Get(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path;
            if (!path.EndsWith("/")) path = path + "/";

            lock (_Lock)
            {
                foreach (ContentRoute curr in _Routes)
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

        /// <summary>
        /// Check if a content route exists.
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
             
            path = path.ToLower();
            if (!path.StartsWith("/")) path = "/" + path; 

            lock (_Lock)
            {
                foreach (ContentRoute curr in _Routes)
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

            lock (_Lock)
            {
                _Routes.Add(route); 
            }
        }

        private void Remove(ContentRoute route)
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
