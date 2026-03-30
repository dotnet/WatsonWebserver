namespace WatsonWebserver.Core.Routing
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver.Core.Middleware;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Extension methods on <see cref="RoutingGroup"/> providing FastAPI-like route registration
    /// with automatic serialization, typed parameters, and middleware integration.
    /// </summary>
    public static class RoutingGroupApiExtensions
    {
        #region GET

        /// <summary>
        /// Add a GET route with an API request handler.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path, e.g. /users/{id}.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Get(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.GET, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region POST

        /// <summary>
        /// Add a POST route without automatic body deserialization.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path, e.g. /items.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Post(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.POST, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        /// <summary>
        /// Add a POST route with automatic body deserialization.
        /// </summary>
        /// <typeparam name="T">Request body type.</typeparam>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path, e.g. /items.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Post<T>(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null) where T : class
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapWithBody<T>(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.POST, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region PUT

        /// <summary>
        /// Add a PUT route without automatic body deserialization.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Put(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.PUT, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        /// <summary>
        /// Add a PUT route with automatic body deserialization.
        /// </summary>
        /// <typeparam name="T">Request body type.</typeparam>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Put<T>(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null) where T : class
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapWithBody<T>(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.PUT, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region PATCH

        /// <summary>
        /// Add a PATCH route without automatic body deserialization.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Patch(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.PATCH, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        /// <summary>
        /// Add a PATCH route with automatic body deserialization.
        /// </summary>
        /// <typeparam name="T">Request body type.</typeparam>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Patch<T>(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null) where T : class
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapWithBody<T>(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.PATCH, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region DELETE

        /// <summary>
        /// Add a DELETE route without automatic body deserialization.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Delete(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.DELETE, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        /// <summary>
        /// Add a DELETE route with automatic body deserialization.
        /// </summary>
        /// <typeparam name="T">Request body type.</typeparam>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Delete<T>(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null) where T : class
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapWithBody<T>(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.DELETE, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region HEAD

        /// <summary>
        /// Add a HEAD route with an API request handler.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Head(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.HEAD, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion

        #region OPTIONS

        /// <summary>
        /// Add an OPTIONS route with an API request handler.
        /// </summary>
        /// <param name="group">The routing group.</param>
        /// <param name="path">URL path.</param>
        /// <param name="handler">Async handler that receives an ApiRequest and returns an object to serialize.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="middleware">Middleware pipeline.</param>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="exceptionHandler">Optional exception handler.</param>
        /// <param name="openApiMetadata">Optional OpenAPI metadata.</param>
        public static void Options(
            this RoutingGroup group,
            string path,
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            OpenApiRouteMetadata openApiMetadata = null)
        {
            Func<HttpContextBase, Task> wrapped = ApiRouteHandler.WrapNoBody(handler, serializer, middleware, settings);
            group.Parameter.Add(HttpMethod.OPTIONS, path, wrapped, exceptionHandler, default, null, openApiMetadata);
        }

        #endregion
    }
}
