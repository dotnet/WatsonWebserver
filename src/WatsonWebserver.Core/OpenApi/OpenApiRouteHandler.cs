namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Route handler for serving OpenAPI JSON documents.
    /// </summary>
    public static class OpenApiRouteHandler
    {
        /// <summary>
        /// Create a route handler that serves the OpenAPI JSON document.
        /// </summary>
        /// <param name="routesProvider">Function that returns the current webserver routes.</param>
        /// <param name="settings">OpenAPI settings.</param>
        /// <returns>Route handler function.</returns>
        public static Func<HttpContextBase, Task> Create(
            Func<WebserverRoutes> routesProvider,
            OpenApiSettings settings)
        {
            if (routesProvider == null) throw new ArgumentNullException(nameof(routesProvider));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();

            return async (ctx) =>
            {
                try
                {
                    WebserverRoutes routes = routesProvider();
                    string json = generator.Generate(routes, settings);

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json; charset=utf-8";
                    ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                    ctx.Response.Headers.Add("Pragma", "no-cache");
                    ctx.Response.Headers.Add("Expires", "0");
                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                    await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.Send($"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}", ctx.Token).ConfigureAwait(false);
                }
            };
        }
    }
}
