namespace WatsonWebserver.Core.OpenApi
{
    using System;

    /// <summary>
    /// Extension methods for adding OpenAPI support to WebserverBase.
    /// </summary>
    public static class WebserverExtensions
    {
        /// <summary>
        /// Add OpenAPI documentation endpoints to the webserver.
        /// </summary>
        /// <param name="server">The webserver instance.</param>
        /// <param name="configure">Optional configuration action.</param>
        /// <returns>The webserver instance for chaining.</returns>
        public static WebserverBase UseOpenApi(this WebserverBase server, Action<OpenApiSettings> configure = null)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            OpenApiSettings settings = new OpenApiSettings();
            configure?.Invoke(settings);

            // Register OpenAPI JSON endpoint
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                settings.DocumentPath,
                OpenApiRouteHandler.Create(() => server.Routes, settings));

            // Register Swagger UI endpoint if enabled
            if (settings.EnableSwaggerUi)
            {
                server.Routes.PreAuthentication.Static.Add(
                    HttpMethod.GET,
                    settings.SwaggerUiPath,
                    SwaggerUiHandler.Create(settings.DocumentPath, settings.Info.Title));
            }

            return server;
        }

        /// <summary>
        /// Add OpenAPI documentation endpoints to the webserver with pre-configured settings.
        /// </summary>
        /// <param name="server">The webserver instance.</param>
        /// <param name="settings">Pre-configured OpenAPI settings.</param>
        /// <returns>The webserver instance for chaining.</returns>
        public static WebserverBase UseOpenApi(this WebserverBase server, OpenApiSettings settings)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            // Register OpenAPI JSON endpoint
            server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                settings.DocumentPath,
                OpenApiRouteHandler.Create(() => server.Routes, settings));

            // Register Swagger UI endpoint if enabled
            if (settings.EnableSwaggerUi)
            {
                server.Routes.PreAuthentication.Static.Add(
                    HttpMethod.GET,
                    settings.SwaggerUiPath,
                    SwaggerUiHandler.Create(settings.DocumentPath, settings.Info.Title));
            }

            return server;
        }
    }
}
