namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Generates OpenAPI specification documents from WatsonWebserver routes. The emitted version
    /// (3.0, 3.1, or 3.2) is selected through <see cref="OpenApiSettings.Version"/>; the default is
    /// OpenAPI 3.0 so documents produced without an explicit selection stay byte-compatible with
    /// earlier Watson releases. Instances are safe to reuse across concurrent requests: each call to
    /// <see cref="Generate(WebserverRoutes, OpenApiSettings)"/> builds its document through a fresh
    /// <see cref="OpenApiDocumentBuilder"/>.
    /// <para>
    /// The document is serialized through source-generated metadata, so generation works under native
    /// AOT and trimming. Example, default, and enum values of application types are resolved through
    /// <see cref="OpenApiSettings.TypeInfoResolver"/>, then the <see cref="JsonSerializerOptions.TypeInfoResolver"/>
    /// of <see cref="SerializerOptions"/>, then reflection-based serialization where it is enabled.
    /// </para>
    /// </summary>
    public class OpenApiDocumentGenerator
    {
        #region Public-Members

        /// <summary>
        /// JSON serializer options used for generating the OpenAPI document. The default writes
        /// indented JSON with camel-cased property names and omits null values. Dictionary keys
        /// (the OpenAPI field names) are emitted verbatim. A null value uses default options.
        /// Watson reads these options on the first call to
        /// <see cref="Generate(WebserverRoutes, OpenApiSettings)"/> and caches what it derives from them,
        /// so changes made to the same instance afterwards are not observed; assign a new instance instead.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        #endregion

        #region Private-Members

        private static readonly IJsonTypeInfoResolver _ReflectionResolver = JsonReflectionFallback.CreateResolver();

        private readonly object _OptionsLock = new object();
        private JsonSerializerOptions _CachedSourceOptions = null;
        private IJsonTypeInfoResolver _CachedSettingsResolver = null;
        private JsonSerializerOptions _CachedOptions = null;

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

            JsonSerializerOptions options = GetSerializationOptions(settings.TypeInfoResolver);
            JsonTypeInfo<Dictionary<string, object>> typeInfo = (JsonTypeInfo<Dictionary<string, object>>)options.GetTypeInfo(typeof(Dictionary<string, object>));
            return JsonSerializer.Serialize(document, typeInfo);
        }

        #endregion

        #region Private-Methods

        private JsonSerializerOptions GetSerializationOptions(IJsonTypeInfoResolver settingsResolver)
        {
            JsonSerializerOptions source = SerializerOptions;

            lock (_OptionsLock)
            {
                if (_CachedOptions != null
                    && ReferenceEquals(_CachedSourceOptions, source)
                    && ReferenceEquals(_CachedSettingsResolver, settingsResolver))
                {
                    return _CachedOptions;
                }

                JsonSerializerOptions options = source != null ? new JsonSerializerOptions(source) : new JsonSerializerOptions();

                List<IJsonTypeInfoResolver> resolvers = new List<IJsonTypeInfoResolver>();
                resolvers.Add(WatsonJsonContext.Default);
                if (settingsResolver != null) resolvers.Add(settingsResolver);

                if (options.TypeInfoResolver != null)
                {
                    resolvers.Add(options.TypeInfoResolver);
                }
                else if (_ReflectionResolver != null)
                {
                    resolvers.Add(_ReflectionResolver);
                }

                options.TypeInfoResolver = JsonTypeInfoResolver.Combine(resolvers.ToArray());

                _CachedSourceOptions = source;
                _CachedSettingsResolver = settingsResolver;
                _CachedOptions = options;
                return options;
            }
        }

        #endregion
    }
}
