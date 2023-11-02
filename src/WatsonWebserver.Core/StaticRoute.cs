using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WatsonWebserver.Core
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    public class StaticRoute
    {
        #region Public-Members

        /// <summary>
        /// Globally-unique identifier.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        [JsonPropertyOrder(0)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Path { get; set; } = null;

        /// <summary>
        /// The handler for the static route.
        /// </summary>
        [JsonIgnore]
        public Func<HttpContextBase, Task> Handler { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

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
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public StaticRoute(HttpMethod method, string path, Func<HttpContextBase, Task> handler, Guid guid = default, object metadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Method = method;
            
            Path = path.ToLower();
            if (!Path.StartsWith("/")) Path = "/" + Path;
            if (!Path.EndsWith("/")) Path = Path + "/";

            Handler = handler;

            if (guid == default(Guid)) GUID = Guid.NewGuid();
            else GUID = guid;
            if (metadata != null) Metadata = metadata;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
