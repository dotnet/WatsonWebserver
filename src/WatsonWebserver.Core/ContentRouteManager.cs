using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver.Core
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

        /// <summary>
        /// The FileMode value to use when accessing files within a content route via a FileStream.  Default is FileMode.Open.
        /// </summary>
        public FileMode ContentFileMode { get; set; } = FileMode.Open;

        /// <summary>
        /// The FileAccess value to use when accessing files within a content route via a FileStream.  Default is FileAccess.Read.
        /// </summary>
        public FileAccess ContentFileAccess { get; set; } = FileAccess.Read;

        /// <summary>
        /// The FileShare value to use when accessing files within a content route via a FileStream.  Default is FileShare.Read.
        /// </summary>
        public FileShare ContentFileShare { get; set; } = FileShare.Read;

        /// <summary>
        /// Content route handler.
        /// </summary>
        public Func<HttpContextBase, Task> Handler
        {
            get
            {
                return _Handler;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Handler));
                _Handler = value;
            }
        }

        /// <summary>
        /// Default filenames on which to search when provided a root URL, e.g. /.
        /// </summary>
        public List<string> DefaultFiles
        {
            get
            {
                return _DefaultFiles;
            }
            set
            {
                if (value == null) value = new List<string>();
                _DefaultFiles = value;
            }
        }

        #endregion

        #region Private-Members

        private List<ContentRoute> _Routes = new List<ContentRoute>();
        private readonly object _Lock = new object();
        private string _BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private Func<HttpContextBase, Task> _Handler = null;
        private List<string> _DefaultFiles = new List<string>
        {
            "index.html",
            "index.html",
            "default.html",
            "default.htm",
            "home.html",
            "home.htm",
            "home.cgi",
            "welcome.html",
            "welcome.htm",
            "index.php",
            "default.aspx",
            "default.asp"
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ContentRouteManager()
        {
            _Handler = HandlerInternal;
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
        public void Add(string path, bool isDirectory, Guid guid = default(Guid), object metadata = null)
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

        private async Task HandlerInternal(HttpContextBase ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
            if (ctx.Response == null) throw new ArgumentNullException(nameof(ctx.Response));

            string baseDirectory = BaseDirectory;
            baseDirectory = baseDirectory.Replace("\\", "/");
            if (!baseDirectory.EndsWith("/")) baseDirectory += "/";

            if (ctx.Request.Method != HttpMethod.GET
                && ctx.Request.Method != HttpMethod.HEAD)
            {
                Set500Response(ctx);
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            string filePath = ctx.Request.Url.RawWithoutQuery;
            if (!String.IsNullOrEmpty(filePath))
            {
                while (filePath.StartsWith("/")) filePath = filePath.Substring(1);
            }

            bool isDirectory = 
                filePath.EndsWith("/") 
                || String.IsNullOrEmpty(filePath)
                || Directory.Exists(baseDirectory + filePath);

            if (isDirectory && !filePath.EndsWith("/")) filePath += "/";

            filePath = baseDirectory + filePath;
            filePath = filePath.Replace("+", " ").Replace("%20", " ");

            if (isDirectory && _DefaultFiles.Count > 0)
            {
                foreach (string defaultFile in _DefaultFiles)
                {
                    if (File.Exists(filePath + defaultFile))
                    {
                        filePath = filePath + defaultFile;
                        break;
                    }
                }
            }

            string contentType = GetContentType(filePath);

            if (!File.Exists(filePath))
            {
                Set404Response(ctx);
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            FileInfo fi = new FileInfo(filePath);
            long contentLength = fi.Length;

            if (ctx.Request.Method == HttpMethod.GET)
            {
                FileStream fs = new FileStream(filePath, ContentFileMode, ContentFileAccess, ContentFileShare);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = contentLength;
                ctx.Response.ContentType = GetContentType(filePath);
                await ctx.Response.Send(contentLength, fs, ctx.Token).ConfigureAwait(false);
                return;
            }
            else if (ctx.Request.Method == HttpMethod.HEAD)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = contentLength;
                ctx.Response.ContentType = GetContentType(filePath);
                await ctx.Response.Send(contentLength, ctx.Token).ConfigureAwait(false);
                return;
            }
            else
            {
                Set500Response(ctx);
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }
        }

        private string GetContentType(string path)
        {
            if (String.IsNullOrEmpty(path)) return "application/octet-stream";

            int idx = path.LastIndexOf(".");
            if (idx >= 0)
            {
                return MimeTypes.GetFromExtension(path.Substring(idx));
            }

            return "application/octet-stream";
        }

        private void Set404Response(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentLength = 0;
        }

        private void Set500Response(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentLength = 0;
        }

        #endregion
    }
}
