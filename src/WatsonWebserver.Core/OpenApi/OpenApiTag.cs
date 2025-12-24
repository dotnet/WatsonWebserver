namespace WatsonWebserver.Core.OpenApi
{
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
}
