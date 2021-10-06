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

        /// <summary>
        /// Base directory for files and directories accessible via content routes.
        /// </summary>
        public string BaseDirectory
        {
            get
            {
                return _BaseDirectory;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) _BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                else
                {
                    if (!Directory.Exists(value)) throw new DirectoryNotFoundException("The requested directory '" + value + "' was not found or not accessible.");
                    _BaseDirectory = value;
                }
            }
        }

        #endregion

        #region Private-Members

        private List<ContentRoute> _Routes = new List<ContentRoute>();
        private readonly object _Lock = new object();
        private string _BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ContentRouteManager()
        { 

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="isDirectory">True if the path represents a directory.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public void Add(string path, bool isDirectory, string guid = null, object metadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path)); 
            Add(new ContentRoute(path, isDirectory, guid, metadata));
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="path">URL path.</param>
        public void Remove(string path)
        { 
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            ContentRoute r = Get(path);
            if (r == null) return;

            lock (_Lock)
            {
                _Routes.Remove(r);
            }
                 
            return;
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

        /// <summary>
        /// Retrieve a content route.
        /// </summary>
        /// <param name="path">URL path.</param>
        /// <param name="route">Matching route.</param>
        /// <returns>True if a match exists.</returns>
        public bool Match(string path, out ContentRoute route)
        {
            route = null;
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            path = path.ToLower(); 
            string dirPath = path;
            if (!dirPath.EndsWith("/")) dirPath = dirPath + "/";

            lock (_Lock)
            {
                foreach (ContentRoute curr in _Routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (dirPath.StartsWith(curr.Path.ToLower()))
                        {
                            route = curr;
                            return true;
                        }
                    }
                    else
                    {
                        if (path.Equals(curr.Path.ToLower()))
                        {
                            route = curr;
                            return true;
                        }
                    }
                }

                return false;
            }
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
