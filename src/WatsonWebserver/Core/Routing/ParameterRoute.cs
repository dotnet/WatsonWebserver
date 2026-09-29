namespace WatsonWebserver.Core.Routing
{
    using WatsonWebserver.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using UrlMatcher;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path containing parameters.
    /// </summary>
    public class ParameterRoute
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
        public HttpMethod Method
        {
            get
            {
                return _Method;
            }
            set
            {
                _Pattern = CompilePattern(value, _Path);
                _Method = value;
            }
        }

        /// <summary>
        /// The pattern against which the raw URL should be matched.
        /// Parameters are written {name} and capture one segment.  A final catch-all written {*name} captures zero or more remaining segments.
        /// Setting an invalid pattern (a catch-all that is not the entire last segment, or more than one catch-all) throws ArgumentException.
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                _Pattern = CompilePattern(_Method, value);
                _Path = value;
            }
        }

        /// <summary>
        /// The handler for the parameter route.
        /// </summary>
        [JsonIgnore]
        public Func<HttpContextBase, Task> Handler { get; set; } = null;

        /// <summary>
        /// The handler to invoke when exceptions are raised.
        /// </summary>
        [JsonIgnore]
        public Func<HttpContextBase, Exception, Task> ExceptionHandler { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        /// <summary>
        /// OpenAPI documentation metadata for this route.
        /// </summary>
        [JsonPropertyOrder(998)]
        public OpenApiRouteMetadata OpenApiMetadata { get; set; } = null;

        /// <summary>
        /// The method-prefixed pattern ("GET /users/{id}") parsed once when Method or Path is set.
        /// Null when Path is null or empty.
        /// </summary>
        internal UrlPattern Pattern
        {
            get
            {
                return _Pattern;
            }
        }

        #endregion

        #region Private-Members

        private HttpMethod _Method = HttpMethod.GET;
        private string _Path = null;
        private UrlPattern _Pattern = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        /// <param name="exceptionHandler">The method that should be called to handle exceptions.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        /// <param name="openApiMetadata">OpenAPI documentation metadata.</param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty, or handler is null.</exception>
        /// <exception cref="ArgumentException">Thrown when path contains an invalid catch-all.</exception>
        public ParameterRoute(
            HttpMethod method,
            string path,
            Func<HttpContextBase, Task> handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            Guid guid = default(Guid),
            object metadata = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Method = method;
            Path = path;
            Handler = handler;
            ExceptionHandler = exceptionHandler;

            if (guid == default(Guid)) GUID = Guid.NewGuid();
            else GUID = guid;

            if (metadata != null) Metadata = metadata;
            if (openApiMetadata != null) OpenApiMetadata = openApiMetadata;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private static UrlPattern CompilePattern(HttpMethod method, string path)
        {
            if (String.IsNullOrEmpty(path)) return null;

            // validate the path on its own first so an ArgumentException names the caller's path rather than the method-prefixed form
            UrlPattern.Parse(path);
            return new UrlPattern(method.ToString() + " " + path);
        }

        #endregion
    }
}
