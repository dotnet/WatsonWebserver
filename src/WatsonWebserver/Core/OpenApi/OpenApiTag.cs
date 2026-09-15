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
        /// A short summary of the tag. Added in OpenAPI 3.2 and emitted only when targeting that
        /// version or later.
        /// </summary>
        public string Summary { get; set; } = null;

        /// <summary>
        /// A description for the tag.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// The name of a parent tag, forming a tag hierarchy. Added in OpenAPI 3.2 and emitted only
        /// when targeting that version or later.
        /// </summary>
        public string Parent { get; set; } = null;

        /// <summary>
        /// A machine-readable classification for the tag (for example <c>nav</c> or <c>badge</c>).
        /// Added in OpenAPI 3.2 and emitted only when targeting that version or later.
        /// </summary>
        public string Kind { get; set; } = null;

        /// <summary>
        /// External documentation for the tag.
        /// </summary>
        public OpenApiExternalDocs ExternalDocs { get; set; } = null;
    }
}
