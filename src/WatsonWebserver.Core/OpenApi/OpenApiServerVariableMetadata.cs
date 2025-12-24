namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI server variable metadata.
    /// </summary>
    public class OpenApiServerVariableMetadata
    {
        /// <summary>
        /// The default value to use for substitution.
        /// </summary>
        [JsonPropertyName("default")]
        public string Default { get; set; } = null;

        /// <summary>
        /// A description for the server variable.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// An enumeration of allowed values.
        /// </summary>
        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Enum { get; set; } = null;
    }
}
