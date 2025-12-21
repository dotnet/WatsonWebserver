namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Settings for OpenAPI document generation.
    /// </summary>
    public class OpenApiSettings
    {
        #region Public-Members

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

    /// <summary>
    /// OpenAPI Info object containing API metadata.
    /// </summary>
    public class OpenApiInfo
    {
        /// <summary>
        /// The title of the API. Required.
        /// </summary>
        public string Title { get; set; } = "API Documentation";

        /// <summary>
        /// The version of the API document. Required.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// A description of the API.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// A URL to the Terms of Service for the API.
        /// </summary>
        public string TermsOfService { get; set; } = null;

        /// <summary>
        /// Contact information for the API.
        /// </summary>
        public OpenApiContact Contact { get; set; } = null;

        /// <summary>
        /// License information for the API.
        /// </summary>
        public OpenApiLicense License { get; set; } = null;
    }

    /// <summary>
    /// Contact information for the API.
    /// </summary>
    public class OpenApiContact
    {
        /// <summary>
        /// The name of the contact person/organization.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// The URL for the contact.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// The email address of the contact.
        /// </summary>
        public string Email { get; set; } = null;
    }

    /// <summary>
    /// License information for the API.
    /// </summary>
    public class OpenApiLicense
    {
        /// <summary>
        /// The license name. Required.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// A URL to the license.
        /// </summary>
        public string Url { get; set; } = null;
    }

    /// <summary>
    /// Server object representing a server URL.
    /// </summary>
    public class OpenApiServer
    {
        /// <summary>
        /// A URL to the target host. Required.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// A description of the server.
        /// </summary>
        public string Description { get; set; } = null;
    }

    /// <summary>
    /// Tag object for grouping operations.
    /// </summary>
    public class OpenApiTag
    {
        /// <summary>
        /// The name of the tag. Required.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// A description for the tag.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// External documentation for the tag.
        /// </summary>
        public OpenApiExternalDocs ExternalDocs { get; set; } = null;
    }

    /// <summary>
    /// External documentation object.
    /// </summary>
    public class OpenApiExternalDocs
    {
        /// <summary>
        /// A description of the target documentation.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// The URL for the target documentation. Required.
        /// </summary>
        public string Url { get; set; } = null;
    }

    /// <summary>
    /// Security scheme definition.
    /// </summary>
    public class OpenApiSecurityScheme
    {
        /// <summary>
        /// The type of security scheme.
        /// Valid values: "apiKey", "http", "oauth2", "openIdConnect".
        /// </summary>
        public string Type { get; set; } = "apiKey";

        /// <summary>
        /// A description for the security scheme.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// The name of the header, query, or cookie parameter.
        /// Required for apiKey type.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// The location of the API key.
        /// Valid values: "query", "header", "cookie".
        /// Required for apiKey type.
        /// </summary>
        public string In { get; set; } = "header";

        /// <summary>
        /// The name of the HTTP authorization scheme.
        /// Required for http type.
        /// </summary>
        public string Scheme { get; set; } = null;

        /// <summary>
        /// Bearer format hint for documentation.
        /// </summary>
        public string BearerFormat { get; set; } = null;
    }
}
