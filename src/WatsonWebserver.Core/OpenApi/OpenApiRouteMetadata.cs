namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAPI route metadata for documenting API endpoints.
    /// Attach this to route objects to generate OpenAPI documentation.
    /// </summary>
    public class OpenApiRouteMetadata
    {
        #region Public-Members

        /// <summary>
        /// Unique string used to identify the operation.
        /// The operationId is case-sensitive and must be unique among all operations.
        /// </summary>
        [JsonPropertyName("operationId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string OperationId { get; set; } = null;

        /// <summary>
        /// A short summary of what the operation does.
        /// </summary>
        [JsonPropertyName("summary")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Summary { get; set; } = null;

        /// <summary>
        /// A verbose explanation of the operation behavior.
        /// CommonMark syntax may be used for rich text representation.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// A list of tags for API documentation control.
        /// Tags can be used for logical grouping of operations.
        /// </summary>
        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Tags { get; set; } = null;

        /// <summary>
        /// Whether this operation is deprecated and should be transitioned out of usage.
        /// </summary>
        [JsonPropertyName("deprecated")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Deprecated { get; set; } = false;

        /// <summary>
        /// A list of parameters applicable for this operation.
        /// Path parameters from the route are automatically extracted if not specified.
        /// </summary>
        [JsonPropertyName("parameters")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenApiParameterMetadata> Parameters { get; set; } = null;

        /// <summary>
        /// The request body applicable for this operation.
        /// </summary>
        [JsonPropertyName("requestBody")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiRequestBodyMetadata RequestBody { get; set; } = null;

        /// <summary>
        /// The list of possible responses, keyed by HTTP status code.
        /// Use "default" for the default response.
        /// </summary>
        [JsonPropertyName("responses")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, OpenApiResponseMetadata> Responses { get; set; } = null;

        /// <summary>
        /// A list of security scheme names required for this operation.
        /// An empty list means no security is required.
        /// </summary>
        [JsonPropertyName("security")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Security { get; set; } = null;

        /// <summary>
        /// An alternative server array to service this operation.
        /// If not specified, the default servers are used.
        /// </summary>
        [JsonPropertyName("servers")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenApiServerMetadata> Servers { get; set; } = null;

        /// <summary>
        /// Additional external documentation for this operation.
        /// </summary>
        [JsonPropertyName("externalDocs")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenApiExternalDocsMetadata ExternalDocs { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiRouteMetadata()
        {
        }

        /// <summary>
        /// Create route metadata with a summary.
        /// </summary>
        /// <param name="summary">Short summary of the operation.</param>
        /// <param name="tags">Optional tags for grouping.</param>
        /// <returns>OpenApiRouteMetadata.</returns>
        public static OpenApiRouteMetadata Create(string summary, params string[] tags)
        {
            OpenApiRouteMetadata metadata = new OpenApiRouteMetadata
            {
                Summary = summary
            };

            if (tags != null && tags.Length > 0)
            {
                metadata.Tags = new List<string>(tags);
            }

            return metadata;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a tag to the operation.
        /// </summary>
        /// <param name="tag">The tag name.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithTag(string tag)
        {
            if (Tags == null) Tags = new List<string>();
            Tags.Add(tag);
            return this;
        }

        /// <summary>
        /// Add multiple tags to the operation.
        /// </summary>
        /// <param name="tags">The tag names.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithTags(params string[] tags)
        {
            if (Tags == null) Tags = new List<string>();
            Tags.AddRange(tags);
            return this;
        }

        /// <summary>
        /// Set the operation description.
        /// </summary>
        /// <param name="description">The description.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithDescription(string description)
        {
            Description = description;
            return this;
        }

        /// <summary>
        /// Add a parameter to the operation.
        /// </summary>
        /// <param name="parameter">The parameter metadata.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithParameter(OpenApiParameterMetadata parameter)
        {
            if (Parameters == null) Parameters = new List<OpenApiParameterMetadata>();
            Parameters.Add(parameter);
            return this;
        }

        /// <summary>
        /// Set the request body for the operation.
        /// </summary>
        /// <param name="requestBody">The request body metadata.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithRequestBody(OpenApiRequestBodyMetadata requestBody)
        {
            RequestBody = requestBody;
            return this;
        }

        /// <summary>
        /// Add a response to the operation.
        /// </summary>
        /// <param name="statusCode">HTTP status code (e.g., 200, 404).</param>
        /// <param name="response">The response metadata.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithResponse(int statusCode, OpenApiResponseMetadata response)
        {
            if (Responses == null) Responses = new Dictionary<string, OpenApiResponseMetadata>();
            Responses[statusCode.ToString()] = response;
            return this;
        }

        /// <summary>
        /// Add a default response to the operation.
        /// </summary>
        /// <param name="response">The response metadata.</param>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata WithDefaultResponse(OpenApiResponseMetadata response)
        {
            if (Responses == null) Responses = new Dictionary<string, OpenApiResponseMetadata>();
            Responses["default"] = response;
            return this;
        }

        /// <summary>
        /// Mark the operation as deprecated.
        /// </summary>
        /// <returns>This instance for chaining.</returns>
        public OpenApiRouteMetadata AsDeprecated()
        {
            Deprecated = true;
            return this;
        }

        #endregion
    }

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

    /// <summary>
    /// OpenAPI external documentation metadata.
    /// </summary>
    public class OpenApiExternalDocsMetadata
    {
        /// <summary>
        /// A description of the target documentation.
        /// </summary>
        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; } = null;

        /// <summary>
        /// The URL for the target documentation.
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = null;
    }
}
