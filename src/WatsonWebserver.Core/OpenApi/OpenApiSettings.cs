namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;

    /// <summary>
    /// Settings for OpenAPI document generation.
    /// </summary>
    public class OpenApiSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether to enable OpenAPI documentation.
        /// When false, no OpenAPI endpoints will be registered.
        /// Default is true.
        /// </summary>
        public bool EnableOpenApi { get; set; } = true;

        /// <summary>
        /// API information.
        /// </summary>
        public OpenApiInfo Info { get; set; } = new OpenApiInfo();

        /// <summary>
        /// List of servers where the API is hosted.
        /// If empty, the current server is used.
        /// </summary>
        public List<OpenApiServer> Servers { get; set; } = new List<OpenApiServer>();

        /// <summary>
        /// Tags for grouping operations.
        /// </summary>
        public List<OpenApiTag> Tags { get; set; } = new List<OpenApiTag>();

        /// <summary>
        /// Path to serve the OpenAPI JSON document.
        /// Default is "/openapi.json".
        /// </summary>
        public string DocumentPath { get; set; } = "/openapi.json";

        /// <summary>
        /// Path to serve the Swagger UI.
        /// Default is "/swagger".
        /// </summary>
        public string SwaggerUiPath { get; set; } = "/swagger";

        /// <summary>
        /// Whether to enable Swagger UI.
        /// Default is true.
        /// </summary>
        public bool EnableSwaggerUi { get; set; } = true;

        /// <summary>
        /// Whether to include routes from PreAuthentication group.
        /// Default is true.
        /// </summary>
        public bool IncludePreAuthRoutes { get; set; } = true;

        /// <summary>
        /// Whether to include routes from PostAuthentication group.
        /// Default is true.
        /// </summary>
        public bool IncludePostAuthRoutes { get; set; } = true;

        /// <summary>
        /// Whether to include content routes (file serving) in documentation.
        /// Default is false.
        /// </summary>
        public bool IncludeContentRoutes { get; set; } = false;

        /// <summary>
        /// Security definitions for the API.
        /// </summary>
        public Dictionary<string, OpenApiSecurityScheme> SecuritySchemes { get; set; } = new Dictionary<string, OpenApiSecurityScheme>();

        /// <summary>
        /// Global security requirements that apply to all operations.
        /// </summary>
        public List<Dictionary<string, List<string>>> Security { get; set; } = new List<Dictionary<string, List<string>>>();

        /// <summary>
        /// External documentation reference.
        /// </summary>
        public OpenApiExternalDocs ExternalDocs { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiSettings()
        {
        }

        /// <summary>
        /// Instantiate the object with API title and version.
        /// </summary>
        /// <param name="title">API title.</param>
        /// <param name="version">API version.</param>
        public OpenApiSettings(string title, string version)
        {
            Info.Title = title;
            Info.Version = version;
        }

        #endregion
    }
}
