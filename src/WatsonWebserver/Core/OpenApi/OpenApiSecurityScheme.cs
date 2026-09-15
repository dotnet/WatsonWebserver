namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// Security scheme definition.
    /// </summary>
    public class OpenApiSecurityScheme
    {
        /// <summary>
        /// The type of security scheme.
        /// Valid values: "apiKey", "http", "oauth2", "openIdConnect", and "mutualTLS".
        /// The "mutualTLS" type requires OpenAPI 3.1 or later.
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
        /// Applies to the "http" type when the scheme is "bearer".
        /// </summary>
        public string BearerFormat { get; set; } = null;

        /// <summary>
        /// The OAuth2 flows this scheme supports.
        /// Required for the "oauth2" type; document generation fails when the type is "oauth2" and
        /// this is null.
        /// </summary>
        public OpenApiOAuthFlows Flows { get; set; } = null;

        /// <summary>
        /// The OpenID Connect discovery URL.
        /// Required for the "openIdConnect" type.
        /// </summary>
        public string OpenIdConnectUrl { get; set; } = null;

        /// <summary>
        /// A URL to the OAuth2 authorization-server metadata document (RFC 8414). Added in
        /// OpenAPI 3.2 and only valid when targeting that version or later.
        /// </summary>
        public string OAuth2MetadataUrl { get; set; } = null;
    }
}
