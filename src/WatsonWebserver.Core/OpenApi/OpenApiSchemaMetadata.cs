namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI schema metadata for describing data types.
    /// Used for request bodies, response bodies, and parameters.
    /// </summary>
    public class OpenApiSchemaMetadata
    {
        #region Public-Members

        /// <summary>
        /// The data type of the schema (e.g., "string", "integer", "boolean", "array", "object").
        /// </summary>
        [JsonPropertyName("type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Type { get; set; } = null;

        /// <summary>
        /// The data format (e.g., "int32", "int64", "float", "double", "date", "date-time", "email", "uri").
        /// </summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Format { get; set; } = null;

        /// <summary>
        /// A description of the schema.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the value can be null.
        /// </summary>
        [JsonPropertyName("nullable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Nullable { get; set; } = false;

        /// <summary>
        /// Schema for array items. Only applicable when Type is "array".
        /// </summary>
        [JsonPropertyName("items")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata Items { get; set; } = null;

        /// <summary>
        /// Properties of an object schema. Only applicable when Type is "object".
        /// </summary>
        [JsonPropertyName("properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiSchemaMetadata> Properties { get; set; } = null;

        /// <summary>
        /// List of required property names. Only applicable when Type is "object".
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Required { get; set; } = null;

        /// <summary>
        /// Reference to a component schema (e.g., "#/components/schemas/User").
        /// When set, other properties are typically ignored.
        /// </summary>
        [JsonPropertyName("$ref")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Ref { get; set; } = null;

        /// <summary>
        /// An example value for the schema.
        /// </summary>
        [JsonPropertyName("example")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Example { get; set; } = null;

        /// <summary>
        /// Default value for the schema.
        /// </summary>
        [JsonPropertyName("default")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Default { get; set; } = null;

        /// <summary>
        /// Enumeration of allowed values.
        /// </summary>
        [JsonPropertyName("enum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<object> Enum { get; set; } = null;

        /// <summary>
        /// Minimum value for numeric types.
        /// </summary>
        [JsonPropertyName("minimum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Minimum { get; set; } = null;

        /// <summary>
        /// Maximum value for numeric types.
        /// </summary>
        [JsonPropertyName("maximum")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Maximum { get; set; } = null;

        /// <summary>
        /// Minimum length for string types.
        /// </summary>
        [JsonPropertyName("minLength")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MinLength { get; set; } = null;

        /// <summary>
        /// Maximum length for string types.
        /// </summary>
        [JsonPropertyName("maxLength")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxLength { get; set; } = null;

        /// <summary>
        /// Regular expression pattern for string validation.
        /// </summary>
        [JsonPropertyName("pattern")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Pattern { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiSchemaMetadata()
        {
        }

        /// <summary>
        /// Create a schema for a primitive type.
        /// </summary>
        /// <param name="type">The data type.</param>
        /// <param name="format">The data format.</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata Create(string type, string format = null)
        {
            return new OpenApiSchemaMetadata
            {
                Type = type,
                Format = format
            };
        }

        /// <summary>
        /// Create a schema reference to a component.
        /// </summary>
        /// <param name="schemaName">The name of the schema in components.</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata CreateRef(string schemaName)
        {
            return new OpenApiSchemaMetadata
            {
                Ref = $"#/components/schemas/{schemaName}"
            };
        }

        /// <summary>
        /// Create an array schema.
        /// </summary>
        /// <param name="itemSchema">The schema for array items.</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata CreateArray(OpenApiSchemaMetadata itemSchema)
        {
            return new OpenApiSchemaMetadata
            {
                Type = "array",
                Items = itemSchema
            };
        }

        /// <summary>
        /// Create a string schema.
        /// </summary>
        /// <param name="format">Optional format (e.g., "date", "date-time", "email", "uri").</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata String(string format = null)
        {
            return Create("string", format);
        }

        /// <summary>
        /// Create an integer schema.
        /// </summary>
        /// <param name="format">Optional format ("int32" or "int64"). Defaults to "int32".</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata Integer(string format = "int32")
        {
            return Create("integer", format);
        }

        /// <summary>
        /// Create a number schema.
        /// </summary>
        /// <param name="format">Optional format ("float" or "double").</param>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata Number(string format = null)
        {
            return Create("number", format);
        }

        /// <summary>
        /// Create a boolean schema.
        /// </summary>
        /// <returns>OpenApiSchemaMetadata.</returns>
        public static OpenApiSchemaMetadata Boolean()
        {
            return Create("boolean");
        }

        #endregion
    }
}
