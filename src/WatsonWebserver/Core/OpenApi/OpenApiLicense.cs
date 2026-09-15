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
        /// An SPDX license expression identifying the license (for example <c>MIT</c> or
        /// <c>Apache-2.0</c>). Added in OpenAPI 3.1 and mutually exclusive with <see cref="Url"/>;
        /// setting both fails document generation. Ignored when targeting OpenAPI 3.0.
        /// </summary>
        public string Identifier { get; set; } = null;

        /// <summary>
        /// A URL to the license. Mutually exclusive with <see cref="Identifier"/> when targeting
        /// OpenAPI 3.1 or later.
        /// </summary>
        public string Url { get; set; } = null;
    }
}
