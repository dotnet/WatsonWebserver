using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied verb and path regex.
    /// </summary>
    internal class DynamicRoute
    {
        #region Public-Members

        /// <summary>
        /// The HTTP verb, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public string Verb;

        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        public Regex Path;

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
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        public DynamicRoute(string verb, Regex path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Verb = verb.ToLower();
            Path = path;
            Handler = handler;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
