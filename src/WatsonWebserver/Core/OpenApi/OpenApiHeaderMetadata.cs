namespace WatsonWebserver.Core.OpenApi
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI header metadata for response headers.
    /// </summary>
    public class OpenApiHeaderMetadata
    {
        /// <summary>
        /// A description of the header.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the header is required.
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Required { get; set; } = false;

        /// <summary>
        /// Whether the header is deprecated.
        /// </summary>
        [JsonPropertyName("deprecated")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// The schema defining the header's type.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata Schema { get; set; } = null;
    }
}
