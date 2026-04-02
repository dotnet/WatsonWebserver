namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI media type metadata for content negotiation.
    /// </summary>
    public class OpenApiMediaTypeMetadata
    {
        /// <summary>
        /// The schema defining the content type.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata Schema { get; set; } = null;

        /// <summary>
        /// Example of the media type content.
        /// </summary>
        [JsonPropertyName("example")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Example { get; set; } = null;

        /// <summary>
        /// Examples of the media type content, keyed by example name.
        /// </summary>
        [JsonPropertyName("examples")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiExampleMetadata> Examples { get; set; } = null;
    }
}
