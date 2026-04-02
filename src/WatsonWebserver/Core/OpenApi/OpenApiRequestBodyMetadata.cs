namespace WatsonWebserver.Core.OpenApi
{
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
}
