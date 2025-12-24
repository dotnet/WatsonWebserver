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
