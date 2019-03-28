using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    public class StaticRoute
    {
        #region Public-Members

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.
        /// </summary>
        public string Path;

        /// <summary>
        /// The 
        /// </summary>
        public Func<HttpRequest, HttpResponse> Handler;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        public StaticRoute(HttpMethod method, string path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Method = method;
            
            Path = path.ToLower();
            if (!Path.StartsWith("/")) Path = "/" + Path;
            if (!Path.EndsWith("/")) Path = Path + "/";

            Handler = handler;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
