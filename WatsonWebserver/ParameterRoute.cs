using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path containing parameters.
    /// </summary>
    public class ParameterRoute
    {
        #region Public-Members

        /// <summary>
        /// Globally-unique identifier.
        /// </summary>
        [JsonProperty(Order = -1)]
        public string GUID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        [JsonProperty(Order = 0)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        [JsonProperty(Order = 1)]
        public string Path { get; set; } = null;

        /// <summary>
        /// The handler for the parameter route.
        /// </summary>
        [JsonIgnore]
        public Func<HttpContext, Task> Handler { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonProperty(Order = 999)]
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public ParameterRoute(HttpMethod method, string path, Func<HttpContext, Task> handler, string guid = null, object metadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Method = method;
            Path = path;
            Handler = handler;

            if (!String.IsNullOrEmpty(guid)) GUID = guid;
            if (metadata != null) Metadata = metadata;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
