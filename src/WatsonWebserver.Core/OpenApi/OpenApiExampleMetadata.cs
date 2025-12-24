namespace WatsonWebserver.Core.OpenApi
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI example metadata.
    /// </summary>
    public class OpenApiExampleMetadata
    {
        /// <summary>
        /// Short description for the example.
        /// </summary>
        [JsonPropertyName("summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Summary { get; set; } = null;

        /// <summary>
        /// Long description for the example.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Embedded literal example value.
        /// </summary>
        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Value { get; set; } = null;

        /// <summary>
        /// URL pointing to an external example.
        /// </summary>
        [JsonPropertyName("externalValue")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ExternalValue { get; set; } = null;
    }
}
