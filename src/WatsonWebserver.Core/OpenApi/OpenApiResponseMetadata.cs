namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI response metadata for documenting API responses.
    /// </summary>
    public class OpenApiResponseMetadata
    {
        #region Public-Members

        /// <summary>
        /// A description of the response. Required by OpenAPI specification.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = "Successful response";

        /// <summary>
        /// The content of the response, keyed by media type (e.g., "application/json").
        /// </summary>
        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiMediaTypeMetadata> Content { get; set; } = null;

        /// <summary>
        /// Headers returned with the response, keyed by header name.
        /// </summary>
        [JsonPropertyName("headers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiHeaderMetadata> Headers { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiResponseMetadata()
        {
        }

        /// <summary>
        /// Create a simple response with just a description.
        /// </summary>
        /// <param name="description">Response description.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Create(string description)
        {
            return new OpenApiResponseMetadata
            {
                Description = description
            };
        }

        /// <summary>
        /// Create a JSON response.
        /// </summary>
        /// <param name="description">Response description.</param>
        /// <param name="schema">The schema for the JSON content.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Json(string description, OpenApiSchemaMetadata schema)
        {
            return new OpenApiResponseMetadata
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/json"] = new OpenApiMediaTypeMetadata { Schema = schema }
                }
            };
        }

        /// <summary>
        /// Create a text response.
        /// </summary>
        /// <param name="description">Response description.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Text(string description)
        {
            return new OpenApiResponseMetadata
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["text/plain"] = new OpenApiMediaTypeMetadata
                    {
                        Schema = OpenApiSchemaMetadata.String()
                    }
                }
            };
        }

        /// <summary>
        /// Create a binary/file response.
        /// </summary>
        /// <param name="description">Response description.</param>
        /// <param name="mediaType">The media type (e.g., "application/octet-stream", "image/png").</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Binary(string description, string mediaType = "application/octet-stream")
        {
            return new OpenApiResponseMetadata
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    [mediaType] = new OpenApiMediaTypeMetadata
                    {
                        Schema = new OpenApiSchemaMetadata { Type = "string", Format = "binary" }
                    }
                }
            };
        }

        /// <summary>
        /// Create a 200 OK response with JSON content.
        /// </summary>
        /// <param name="schema">The schema for the JSON content.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Ok(OpenApiSchemaMetadata schema)
        {
            return Json("Successful response", schema);
        }

        /// <summary>
        /// Create a 201 Created response.
        /// </summary>
        /// <param name="schema">The schema for the created resource.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Created(OpenApiSchemaMetadata schema = null)
        {
            if (schema == null)
                return Create("Resource created successfully");

            return Json("Resource created successfully", schema);
        }

        /// <summary>
        /// Create a 204 No Content response.
        /// </summary>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata NoContent()
        {
            return Create("No content");
        }

        /// <summary>
        /// Create a 400 Bad Request response.
        /// </summary>
        /// <param name="schema">Optional error schema.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata BadRequest(OpenApiSchemaMetadata schema = null)
        {
            if (schema == null)
                return Create("Bad request");

            return Json("Bad request", schema);
        }

        /// <summary>
        /// Create a 401 Unauthorized response.
        /// </summary>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Unauthorized()
        {
            return Create("Authentication required");
        }

        /// <summary>
        /// Create a 403 Forbidden response.
        /// </summary>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata Forbidden()
        {
            return Create("Access denied");
        }

        /// <summary>
        /// Create a 404 Not Found response.
        /// </summary>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata NotFound()
        {
            return Create("Resource not found");
        }

        /// <summary>
        /// Create a 500 Internal Server Error response.
        /// </summary>
        /// <param name="schema">Optional error schema.</param>
        /// <returns>OpenApiResponseMetadata.</returns>
        public static OpenApiResponseMetadata InternalError(OpenApiSchemaMetadata schema = null)
        {
            if (schema == null)
                return Create("Internal server error");

            return Json("Internal server error", schema);
        }

        #endregion
    }

    /// <summary>
    /// OpenAPI header metadata for response headers.
    /// </summary>
    public class OpenApiHeaderMetadata
    {
        /// <summary>
        /// A description of the header.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Whether the header is required.
        /// </summary>
        [JsonPropertyName("required")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Required { get; set; } = false;

        /// <summary>
        /// Whether the header is deprecated.
        /// </summary>
        [JsonPropertyName("deprecated")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// The schema defining the header's type.
        /// </summary>
        [JsonPropertyName("schema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiSchemaMetadata Schema { get; set; } = null;
    }
}
