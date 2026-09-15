namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Generates OpenAPI specification documents from WatsonWebserver routes. The emitted version
    /// (3.0, 3.1, or 3.2) is selected through <see cref="OpenApiSettings.Version"/>; the default is
    /// OpenAPI 3.0 so documents produced without an explicit selection stay byte-compatible with
    /// earlier Watson releases. Instances are stateless and safe to reuse across concurrent
    /// requests: each call to <see cref="Generate(WebserverRoutes, OpenApiSettings)"/> builds its
    /// document through a fresh <see cref="OpenApiDocumentBuilder"/>.
    /// </summary>
    public class OpenApiDocumentGenerator
    {
        #region Public-Members

        /// <summary>
        /// JSON serializer options used for generating the OpenAPI document. The default writes
        /// indented JSON with camel-cased property names and omits null values. Dictionary keys
        /// (the OpenAPI field names) are emitted verbatim.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiDocumentGenerator()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate an OpenAPI JSON document from the webserver routes.
        /// </summary>
        /// <param name="routes">The webserver routes. May not be null.</param>
        /// <param name="settings">OpenAPI settings, including the target version. May not be null.</param>
        /// <returns>OpenAPI JSON string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="routes"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="OpenApiValidationException">Thrown when the settings are inconsistent with one another or with the selected version.</exception>
        public string Generate(WebserverRoutes routes, OpenApiSettings settings)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            OpenApiDocumentBuilder builder = new OpenApiDocumentBuilder(settings);
            Dictionary<string, object> document = builder.Build(routes);
            return JsonSerializer.Serialize(document, SerializerOptions);
        }

        #endregion
    }
}
