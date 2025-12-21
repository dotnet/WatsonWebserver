namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    public class ContentRoute
    {
        #region Public-Members

        /// <summary>
        /// Globally-unique identifier.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        [JsonPropertyOrder(0)]
        public string Path { get; set; } = null;

        /// <summary>
        /// Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.
        /// </summary>
        [JsonPropertyOrder(1)]
        public bool IsDirectory { get; set; } = false;

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

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="isDirectory">Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.</param>
        /// <param name="exceptionHandler">The method that should be called to handle exceptions.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        /// <param name="openApiMetadata">OpenAPI documentation metadata.</param>
        public ContentRoute(
            string path,
            bool isDirectory,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            Guid guid = default(Guid),
            object metadata = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            Path = path.ToLower();
            IsDirectory = isDirectory;
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

        #endregion
    }
}
