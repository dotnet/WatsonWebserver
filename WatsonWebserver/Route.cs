using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied verb and path.
    /// </summary>
    public class Route
    {
        #region Public-Members

        /// <summary>
        /// The HTTP verb, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public string Verb;

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
        /// <param name="verb">The HTTP verb, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        public Route(string verb, string path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Verb = verb.ToLower();
            
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
