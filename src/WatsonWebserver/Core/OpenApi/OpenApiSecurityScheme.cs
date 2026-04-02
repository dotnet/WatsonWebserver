namespace WatsonWebserver.Core.OpenApi
{
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
