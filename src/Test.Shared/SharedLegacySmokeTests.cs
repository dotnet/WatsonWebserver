namespace Test.Shared
{
    using System;
    using System.Text;
    using System.Net.Http;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared legacy-style smoke tests executed by both runners.
    /// </summary>
    public static class SharedLegacySmokeTests
    {
        /// <summary>
        /// Verify a basic HTTP/1.1 GET request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicGetAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/get")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 GET request to succeed.");
                    }

                    if (!String.Equals(body, "GET response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 GET response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 POST request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicPostAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("test data", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/post"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 POST request to succeed.");
                    }

                    if (!String.Equals(body, "POST response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 POST response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 DELETE request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicDeleteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Delete, new Uri(host.BaseAddress, "/test/delete"));

                    using (request)
                    {
                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected HTTP/1.1 DELETE request to succeed.");
                        }

                        if (!String.Equals(body, "DELETE response", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 DELETE response body.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 parameter route resolves and returns the parameterized value.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ParameterRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/users/42")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 parameter route request to succeed.");
                    }

                    if (!String.Equals(body, "User 42", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 parameter route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 query-string route resolves and returns the query value.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11QueryStringRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/query?name=alpha")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 query-string route request to succeed.");
                    }

                    if (!String.Equals(body, "Query alpha", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 query-string route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an unmatched HTTP/1.1 route returns the default 404 response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11NotFoundRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/does-not-exist")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if ((int)response.StatusCode != 404)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 unmatched route to return 404.");
                    }

                    if (!String.Equals(body, "not-found", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 unmatched route response body.");
                    }
                }
            }
        }

        private static void ConfigureBasicRoutes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/get", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("GET response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/post", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("POST response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.DELETE, "/test/delete", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("DELETE response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Parameter.Add(CoreHttpMethod.GET, "/users/{id}", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("User " + context.Request.Url.Parameters["id"], context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/query", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Query " + context.Request.RetrieveQueryValue("name"), context.Token).ConfigureAwait(false);
            });
        }

        private static HttpClient CreateHttpClient(Version version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }
    }
}
