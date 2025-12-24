namespace WatsonWebserver.Core.OpenApi
{
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
}
