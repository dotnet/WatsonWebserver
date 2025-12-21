namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI request body metadata for documenting request payloads.
    /// </summary>
    public class OpenApiRequestBodyMetadata
    {
        #region Public-Members

        /// <summary>
        /// A brief description of the request body.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the request body is required.
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Required { get; set; } = false;

        /// <summary>
        /// The content of the request body, keyed by media type (e.g., "application/json").
        /// </summary>
        [JsonPropertyName("content")]
        public Dictionary<string, OpenApiMediaTypeMetadata> Content { get; set; } = new Dictionary<string, OpenApiMediaTypeMetadata>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiRequestBodyMetadata()
        {
        }

        /// <summary>
        /// Create a JSON request body.
        /// </summary>
        /// <param name="schema">The schema for the JSON content.</param>
        /// <param name="description">Description of the request body.</param>
        /// <param name="required">Whether the request body is required.</param>
        /// <returns>OpenApiRequestBodyMetadata.</returns>
        public static OpenApiRequestBodyMetadata Json(OpenApiSchemaMetadata schema, string description = null, bool required = true)
        {
            return new OpenApiRequestBodyMetadata
            {
                Description = description,
                Required = required,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/json"] = new OpenApiMediaTypeMetadata { Schema = schema }
                }
            };
        }

        /// <summary>
        /// Create a form data request body.
        /// </summary>
        /// <param name="schema">The schema for the form data.</param>
        /// <param name="description">Description of the request body.</param>
        /// <param name="required">Whether the request body is required.</param>
        /// <returns>OpenApiRequestBodyMetadata.</returns>
        public static OpenApiRequestBodyMetadata FormData(OpenApiSchemaMetadata schema, string description = null, bool required = true)
        {
            return new OpenApiRequestBodyMetadata
            {
                Description = description,
                Required = required,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/x-www-form-urlencoded"] = new OpenApiMediaTypeMetadata { Schema = schema }
                }
            };
        }

        /// <summary>
        /// Create a multipart form data request body (for file uploads).
        /// </summary>
        /// <param name="schema">The schema for the multipart data.</param>
        /// <param name="description">Description of the request body.</param>
        /// <param name="required">Whether the request body is required.</param>
        /// <returns>OpenApiRequestBodyMetadata.</returns>
        public static OpenApiRequestBodyMetadata Multipart(OpenApiSchemaMetadata schema, string description = null, bool required = true)
        {
            return new OpenApiRequestBodyMetadata
            {
                Description = description,
                Required = required,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["multipart/form-data"] = new OpenApiMediaTypeMetadata { Schema = schema }
                }
            };
        }

        #endregion
    }

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
