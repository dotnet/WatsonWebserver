namespace WatsonWebserver.Core.OpenApi
{
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
        /// A short summary of the API. Emitted only when targeting OpenAPI 3.1 or later; ignored
        /// under OpenAPI 3.0, where the Info object has no summary field.
        /// </summary>
        public string Summary { get; set; } = null;

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
}
