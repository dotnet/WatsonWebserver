namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods for adding routes with OpenAPI documentation.
    /// </summary>
    public static class RouteManagerExtensions
    {
        /// <summary>
        /// Add a static route with OpenAPI documentation.
        /// </summary>
        /// <param name="manager">The static route manager.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="configureOpenApi">Action to configure OpenAPI metadata.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        public static void Add(
            this StaticRouteManager manager,
            HttpMethod method,
            string path,
            Func<HttpContextBase, Task> handler,
            Action<OpenApiRouteMetadata> configureOpenApi,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (configureOpenApi == null) throw new ArgumentNullException(nameof(configureOpenApi));

            OpenApiRouteMetadata metadata = new OpenApiRouteMetadata();
            configureOpenApi(metadata);

            manager.Add(method, path, handler, exceptionHandler, default, null, metadata);
        }

        /// <summary>
        /// Add a parameter route with OpenAPI documentation.
        /// </summary>
        /// <param name="manager">The parameter route manager.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">URL path with parameters (e.g., "/users/{id}").</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="configureOpenApi">Action to configure OpenAPI metadata.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        public static void Add(
            this ParameterRouteManager manager,
            HttpMethod method,
            string path,
            Func<HttpContextBase, Task> handler,
            Action<OpenApiRouteMetadata> configureOpenApi,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (configureOpenApi == null) throw new ArgumentNullException(nameof(configureOpenApi));

            OpenApiRouteMetadata metadata = new OpenApiRouteMetadata();
            configureOpenApi(metadata);

            manager.Add(method, path, handler, exceptionHandler, default, null, metadata);
        }

        /// <summary>
        /// Add a dynamic route with OpenAPI documentation.
        /// </summary>
        /// <param name="manager">The dynamic route manager.</param>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Regex pattern for the path.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="configureOpenApi">Action to configure OpenAPI metadata.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        public static void Add(
            this DynamicRouteManager manager,
            HttpMethod method,
            Regex path,
            Func<HttpContextBase, Task> handler,
            Action<OpenApiRouteMetadata> configureOpenApi,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            if (configureOpenApi == null) throw new ArgumentNullException(nameof(configureOpenApi));

            OpenApiRouteMetadata metadata = new OpenApiRouteMetadata();
            configureOpenApi(metadata);

            manager.Add(method, path, handler, exceptionHandler, default, null, metadata);
        }
    }
}
