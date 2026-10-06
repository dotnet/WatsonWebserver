namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization.Metadata;

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
        /// The OpenAPI specification version to emit.
        /// Default is <see cref="OpenApiVersionEnum.V3_0"/>, which keeps the emitted document
        /// byte-compatible with earlier Watson releases. Select <see cref="OpenApiVersionEnum.V3_1"/>
        /// or <see cref="OpenApiVersionEnum.V3_2"/> to opt into the newer encodings and fields.
        /// </summary>
        public OpenApiVersionEnum Version { get; set; } = OpenApiVersionEnum.V3_0;

        /// <summary>
        /// An explicit version string to place in the document's <c>openapi</c> field, overriding
        /// the default derived from <see cref="Version"/> (<c>3.0.3</c>, <c>3.1.1</c>, or
        /// <c>3.2.0</c>). Leave null to use the default. Use this only to pin a specific patch
        /// release; it does not change the encoding rules, which follow <see cref="Version"/>.
        /// </summary>
        public string VersionString
        {
            get
            {
                return _VersionString;
            }
            set
            {
                _VersionString = String.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }

        /// <summary>
        /// Whether the OpenAPI document and Swagger UI endpoints require authentication.
        /// When false (the default), the endpoints are registered ahead of authentication and are
        /// publicly reachable. When true, they are registered behind authentication and unauthenticated
        /// callers receive the server's standard authentication-failure response instead of the document.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

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
        /// The version of the swagger-ui-dist assets loaded by the Swagger UI page from the public
        /// CDN. Default is <c>5.17.14</c>. The Swagger UI page has no offline story: the assets are
        /// fetched from unpkg.com at page load, so a browser without internet access cannot render it.
        /// May not be null or empty.
        /// </summary>
        public string SwaggerUiVersion
        {
            get
            {
                return _SwaggerUiVersion;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(SwaggerUiVersion));
                _SwaggerUiVersion = value;
            }
        }

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
        /// Reusable component schemas emitted under <c>components.schemas</c>.
        /// Keys are the schema names; values are the schema metadata used by
        /// <see cref="OpenApiSchemaMetadata.CreateRef(string)"/> references.
        /// </summary>
        public Dictionary<string, OpenApiSchemaMetadata> Schemas { get; set; } = new Dictionary<string, OpenApiSchemaMetadata>();

        /// <summary>
        /// Global security requirements that apply to all operations.
        /// </summary>
        public List<Dictionary<string, List<string>>> Security { get; set; } = new List<Dictionary<string, List<string>>>();

        /// <summary>
        /// External documentation reference.
        /// </summary>
        public OpenApiExternalDocs ExternalDocs { get; set; } = null;

        /// <summary>
        /// Webhooks the API defines, keyed by webhook name. Added in OpenAPI 3.1; supplying webhooks
        /// while targeting OpenAPI 3.0 fails document generation. When webhooks are present the
        /// <c>paths</c> object may be omitted from the document.
        /// </summary>
        public Dictionary<string, OpenApiWebhookMetadata> Webhooks { get; set; } = new Dictionary<string, OpenApiWebhookMetadata>();

        /// <summary>
        /// The default JSON Schema dialect (a URI) applied to schemas that do not declare their own.
        /// Added in OpenAPI 3.1 and emitted only when targeting that version or later.
        /// </summary>
        public string JsonSchemaDialect { get; set; } = null;

        /// <summary>
        /// A URI that identifies this OpenAPI document (the <c>$self</c> field). Added in OpenAPI 3.2
        /// and emitted only when targeting that version or later.
        /// </summary>
        public string Self { get; set; } = null;

        /// <summary>
        /// Additional path operations that use HTTP methods beyond the fixed OpenAPI set. The outer
        /// key is the path template, the inner key is the upper-cased method name, and the value is the
        /// operation metadata. The <c>QUERY</c> method is emitted as the first-class <c>query</c>
        /// path-item field; every other method is emitted under the path item's
        /// <c>additionalOperations</c> object. Added in OpenAPI 3.2; supplying entries while targeting
        /// an earlier version fails document generation.
        /// </summary>
        public Dictionary<string, Dictionary<string, OpenApiRouteMetadata>> AdditionalOperations { get; set; } = new Dictionary<string, Dictionary<string, OpenApiRouteMetadata>>();

        /// <summary>
        /// Resolver for application types used as OpenAPI example, default, or enum values, typically a
        /// source-generated <c>JsonSerializerContext</c>. Strings, numbers, booleans, and
        /// <c>List&lt;object&gt;</c> and <c>Dictionary&lt;string, object&gt;</c> trees of them need no
        /// resolver. Under native AOT and trimming, where reflection-based serialization is disabled, other
        /// value types must be declared here. Default is null, which uses reflection-based serialization for
        /// such values where it is enabled.
        /// </summary>
        public IJsonTypeInfoResolver TypeInfoResolver { get; set; } = null;

        #endregion

        #region Private-Members

        private string _VersionString = null;
        private string _SwaggerUiVersion = "5.17.14";

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
