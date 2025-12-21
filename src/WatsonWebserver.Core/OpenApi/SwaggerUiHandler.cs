namespace WatsonWebserver.Core.OpenApi
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Route handler for serving Swagger UI.
    /// </summary>
    public static class SwaggerUiHandler
    {
        /// <summary>
        /// Create a route handler that serves the Swagger UI HTML page.
        /// </summary>
        /// <param name="openApiPath">Path to the OpenAPI JSON document.</param>
        /// <param name="title">Page title. Defaults to "API Documentation".</param>
        /// <returns>Route handler function.</returns>
        public static Func<HttpContextBase, Task> Create(string openApiPath, string title = "API Documentation")
        {
            if (String.IsNullOrEmpty(openApiPath)) throw new ArgumentNullException(nameof(openApiPath));

            string html = GenerateSwaggerHtml(openApiPath, title);

            return async (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                ctx.Response.Headers.Add("Pragma", "no-cache");
                ctx.Response.Headers.Add("Expires", "0");

                await ctx.Response.Send(html, ctx.Token).ConfigureAwait(false);
            };
        }

        /// <summary>
        /// Generate the Swagger UI HTML page.
        /// </summary>
        /// <param name="openApiPath">Path to the OpenAPI JSON document.</param>
        /// <param name="title">Page title.</param>
        /// <returns>HTML string.</returns>
        public static string GenerateSwaggerHtml(string openApiPath, string title = "API Documentation")
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{EscapeHtml(title)}</title>
    <link rel=""stylesheet"" href=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui.css"" />
    <style>
        html {{
            box-sizing: border-box;
            overflow: -moz-scrollbars-vertical;
            overflow-y: scroll;
        }}
        *, *:before, *:after {{
            box-sizing: inherit;
        }}
        body {{
            margin: 0;
            background: #fafafa;
        }}
        .swagger-ui .topbar {{
            background-color: #1b1b1b;
        }}
        .swagger-ui .topbar .download-url-wrapper .select-label {{
            display: flex;
            align-items: center;
            width: 100%;
            max-width: 600px;
        }}
        .swagger-ui .topbar .download-url-wrapper .select-label select {{
            flex: 2;
        }}
        .swagger-ui .topbar .download-url-wrapper .select-label .download-url-button {{
            background: #547f00;
        }}
        #swagger-ui {{
            max-width: 1460px;
            margin: 0 auto;
            padding: 20px;
        }}
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.11.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {{
            const ui = SwaggerUIBundle({{
                url: '{EscapeJs(openApiPath)}',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: 'StandaloneLayout',
                validatorUrl: null,
                supportedSubmitMethods: ['get', 'post', 'put', 'delete', 'patch', 'head', 'options'],
                defaultModelsExpandDepth: 1,
                defaultModelExpandDepth: 1,
                displayRequestDuration: true,
                docExpansion: 'list',
                filter: true,
                showExtensions: true,
                showCommonExtensions: true,
                syntaxHighlight: {{
                    activate: true,
                    theme: 'monokai'
                }}
            }});
            window.ui = ui;
        }};
    </script>
</body>
</html>";
        }

        private static string EscapeHtml(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string EscapeJs(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            return text
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }
    }
}
