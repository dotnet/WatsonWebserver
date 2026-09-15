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

        /// <summary>
        /// Schema describing each item in a sequential (streaming) media type such as
        /// <c>application/jsonl</c> or <c>text/event-stream</c>. Added in OpenAPI 3.2 and emitted
        /// only when targeting that version or later.
        /// </summary>
        [JsonPropertyName("itemSchema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata ItemSchema { get; set; } = null;
    }
}
