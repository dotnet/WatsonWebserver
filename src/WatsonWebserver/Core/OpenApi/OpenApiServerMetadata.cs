namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI server metadata.
    /// </summary>
    public class OpenApiServerMetadata
    {
        /// <summary>
        /// A URL to the target host.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = null;

        /// <summary>
        /// An optional short name for the server. Added in OpenAPI 3.2 and emitted only when
        /// targeting that version or later.
        /// </summary>
        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; } = null;

        /// <summary>
        /// A description of the server.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// A map of server variables for URL template substitution.
        /// </summary>
        [JsonPropertyName("variables")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiServerVariableMetadata> Variables { get; set; } = null;
    }
}
