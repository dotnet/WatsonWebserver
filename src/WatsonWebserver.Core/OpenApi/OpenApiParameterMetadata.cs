namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI parameter metadata for documenting request parameters.
    /// </summary>
    public class OpenApiParameterMetadata
    {
        #region Public-Members

        /// <summary>
        /// The name of the parameter. Parameter names are case-sensitive.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = null;

        /// <summary>
        /// The location of the parameter (query, header, path, or cookie).
        /// </summary>
        [JsonPropertyName("in")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ParameterLocation In { get; set; } = ParameterLocation.Query;

        /// <summary>
        /// A brief description of the parameter.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the parameter is required.
        /// Path parameters are always required and this should be true for them.
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Required { get; set; } = false;

        /// <summary>
        /// Whether the parameter is deprecated and should be transitioned out of usage.
        /// </summary>
        [JsonPropertyName("deprecated")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// Whether empty values are allowed for this parameter.
        /// Only applies to query parameters.
        /// </summary>
        [JsonPropertyName("allowEmptyValue")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool AllowEmptyValue { get; set; } = false;

        /// <summary>
        /// The schema defining the type used for the parameter.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata Schema { get; set; } = null;

        /// <summary>
        /// Example of the parameter's potential value.
        /// </summary>
        [JsonPropertyName("example")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Example { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiParameterMetadata()
        {
        }

        /// <summary>
        /// Create a path parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="schema">Parameter schema.</param>
        /// <returns>OpenApiParameterMetadata.</returns>
        public static OpenApiParameterMetadata Path(string name, string description = null, OpenApiSchemaMetadata schema = null)
        {
            return new OpenApiParameterMetadata
            {
                Name = name,
                In = ParameterLocation.Path,
                Required = true,
                Description = description,
                Schema = schema ?? OpenApiSchemaMetadata.String()
            };
        }

        /// <summary>
        /// Create a query parameter.
        /// </summary>
        /// <param name="name">Parameter name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="required">Whether the parameter is required.</param>
        /// <param name="schema">Parameter schema.</param>
        /// <returns>OpenApiParameterMetadata.</returns>
        public static OpenApiParameterMetadata Query(string name, string description = null, bool required = false, OpenApiSchemaMetadata schema = null)
        {
            return new OpenApiParameterMetadata
            {
                Name = name,
                In = ParameterLocation.Query,
                Required = required,
                Description = description,
                Schema = schema ?? OpenApiSchemaMetadata.String()
            };
        }

        /// <summary>
        /// Create a header parameter.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="description">Parameter description.</param>
        /// <param name="required">Whether the parameter is required.</param>
        /// <param name="schema">Parameter schema.</param>
        /// <returns>OpenApiParameterMetadata.</returns>
        public static OpenApiParameterMetadata Header(string name, string description = null, bool required = false, OpenApiSchemaMetadata schema = null)
        {
            return new OpenApiParameterMetadata
            {
                Name = name,
                In = ParameterLocation.Header,
                Required = required,
                Description = description,
                Schema = schema ?? OpenApiSchemaMetadata.String()
            };
        }

        #endregion
    }
}
