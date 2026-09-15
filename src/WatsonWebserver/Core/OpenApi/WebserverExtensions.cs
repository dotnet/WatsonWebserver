namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Extension methods for adding OpenAPI support to WebserverBase.
    /// </summary>
    public static class WebserverExtensions
    {
        /// <summary>
        /// Add OpenAPI documentation endpoints to the webserver.
        /// The document endpoint (default <c>/openapi.json</c>) and the Swagger UI endpoint (default
        /// <c>/swagger</c>) are registered ahead of authentication by default, or behind it when
        /// <see cref="OpenApiSettings.RequireAuthentication"/> is set.
        /// </summary>
        /// <param name="server">The webserver instance. May not be null.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The webserver instance for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="server"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the document or Swagger UI path is null, empty, or not rooted at '/'.</exception>
        public static WebserverBase UseOpenApi(this WebserverBase server, Action<OpenApiSettings> configure = null)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            OpenApiSettings settings = new OpenApiSettings();
            configure?.Invoke(settings);

            RegisterOpenApiRoutes(server, settings);
            return server;
        }

        /// <summary>
        /// Add OpenAPI documentation endpoints to the webserver with pre-configured settings.
        /// </summary>
        /// <param name="server">The webserver instance. May not be null.</param>
        /// <param name="settings">Pre-configured OpenAPI settings. May not be null.</param>
        /// <returns>The webserver instance for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="server"/> or <paramref name="settings"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the document or Swagger UI path is null, empty, or not rooted at '/'.</exception>
        public static WebserverBase UseOpenApi(this WebserverBase server, OpenApiSettings settings)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            RegisterOpenApiRoutes(server, settings);
            return server;
        }

        private static void RegisterOpenApiRoutes(WebserverBase server, OpenApiSettings settings)
        {
            if (!settings.EnableOpenApi) return;

            if (String.IsNullOrEmpty(settings.DocumentPath))
                throw new ArgumentException("OpenAPI document path may not be null or empty.", nameof(settings));
            if (!settings.DocumentPath.StartsWith("/"))
                throw new ArgumentException("OpenAPI document path must begin with '/'.", nameof(settings));

            RoutingGroup group = settings.RequireAuthentication
                ? server.Routes.PostAuthentication
                : server.Routes.PreAuthentication;

            group.Static.Add(
                HttpMethod.GET,
                settings.DocumentPath,
                OpenApiRouteHandler.Create(() => server.Routes, settings));

            if (settings.EnableSwaggerUi)
            {
                if (String.IsNullOrEmpty(settings.SwaggerUiPath))
                    throw new ArgumentException("Swagger UI path may not be null or empty.", nameof(settings));
                if (!settings.SwaggerUiPath.StartsWith("/"))
                    throw new ArgumentException("Swagger UI path must begin with '/'.", nameof(settings));

                group.Static.Add(
                    HttpMethod.GET,
                    settings.SwaggerUiPath,
                    SwaggerUiHandler.Create(settings.DocumentPath, settings.Info.Title, settings.SwaggerUiVersion));
            }
        }
    }
}
