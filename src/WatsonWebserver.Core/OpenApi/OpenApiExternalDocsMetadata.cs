namespace WatsonWebserver.Core.OpenApi
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI external documentation metadata.
    /// </summary>
    public class OpenApiExternalDocsMetadata
    {
        /// <summary>
        /// A description of the target documentation.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// The URL for the target documentation.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = null;
    }
}
