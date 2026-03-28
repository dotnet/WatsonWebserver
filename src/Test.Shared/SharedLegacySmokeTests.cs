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
        /// Verify a basic HTTP/1.1 POST body can be read and echoed by the route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BodyEchoAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("echo-body", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo-body"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 body echo request to succeed.");
                    }

                    if (!String.Equals(body, "echo-body", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 body echo response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 PUT request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicPutAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("put-data", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PutAsync(new Uri(host.BaseAddress, "/test/put"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 PUT request to succeed.");
                    }

                    if (!String.Equals(body, "PUT response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 PUT response body.");
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

        /// <summary>
        /// Verify a static content route returns the expected content.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11StaticContentRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/static/test")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 static content route to succeed.");
                    }

                    if (!String.Equals(body, "Static route response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 static content route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify request headers are echoed back with values preserved.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11HeaderEchoAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/test/header-echo"));
                    request.Headers.TryAddWithoutValidation("X-Test-Colon", "value1:value2:value3");

                    using (request)
                    {
                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected HTTP/1.1 header echo request to succeed.");
                        }

                        if (!body.Contains("X-Test-Colon: value1:value2:value3", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 header echo response body.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 response delivers all chunks and advertises chunked transfer encoding.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedTransferEncodingAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/chunked")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked transfer response to succeed.");
                    }

                    for (int i = 1; i <= 5; i++)
                    {
                        if (!body.Contains("Chunk " + i.ToString(), StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Missing expected chunk content in HTTP/1.1 chunked response.");
                        }
                    }

                    if (response.Headers.TransferEncodingChunked != true)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked response header to advertise chunked transfer encoding.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify chunked edge-case responses succeed.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedEdgeCasesAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/chunked-edge")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked edge-case response to succeed.");
                    }

                    if (!body.Contains("single-byte", StringComparison.Ordinal)
                        || !body.Contains("large-chunk", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 chunked edge-case response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsBytes</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyDataAsBytesAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo", "Hello, chunked world!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsString</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyDataAsStringAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo-string", "Hello, chunked string!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>ReadBodyAsync</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyReadBodyAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo-async", "Hello, async chunked!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through manual chunk reads.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyManualReadChunkAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-manual", "Hello, manual chunks!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a large binary chunked HTTP/1.1 request body round-trips successfully.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11LargeChunkedRequestBodyAsync()
        {
            byte[] body = new byte[65536 + 1024];
            Random random = new Random(42);
            random.NextBytes(body);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri(host.BaseAddress, "/test/chunked-echo")))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    request.Content = new StreamContent(new System.IO.MemoryStream(body));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    request.Headers.TransferEncodingChunked = true;

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    byte[] echoed = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 large chunked request body to succeed.");
                    }

                    if (echoed.Length != body.Length)
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 large chunked request body length.");
                    }

                    for (int i = 0; i < body.Length; i++)
                    {
                        if (echoed[i] != body[i])
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 large chunked request body content.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a simple HTTP/1.1 echo request preserves a plain-text payload exactly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DataPreservationHelloAsync()
        {
            await TestBodyEchoAsync("hello").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a simple HTTP/1.1 echo request preserves a payload containing CRLF exactly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DataPreservationHelloCrLfAsync()
        {
            await TestBodyEchoAsync("hello\r\n").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify HTTP/1.1 server-sent events stream the expected events with the correct content type.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/sse")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string contentType = response.Content.Headers.ContentType?.ToString() ?? String.Empty;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events response to succeed.");
                    }

                    if (!body.Contains("data: Event 1", StringComparison.Ordinal)
                        || !body.Contains("data: Event 5", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 server-sent events body.");
                    }

                    if (!contentType.Contains("text/event-stream", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events content type.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify HTTP/1.1 server-sent event edge cases preserve multi-line, special-character, and unicode content.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsEdgeCasesAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/sse-edge")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events edge-case response to succeed.");
                    }

                    if (!body.Contains("data: Line1", StringComparison.Ordinal)
                        || !body.Contains("data: Line2", StringComparison.Ordinal)
                        || !body.Contains("data: Special: <>&\"'", StringComparison.Ordinal)
                        || !body.Contains("data: Unicode: 世界", StringComparison.Ordinal)
                        || !body.Contains("data: done", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 server-sent events edge-case body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a route that attempts to send twice still returns the first response without crashing the server.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DoubleSendResponseAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/double-send")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode || !String.Equals(body, "First response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 double-send response behavior.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an exception thrown from a route handler produces an HTTP 500 response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ExceptionInRouteHandlerAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/error/test")).ConfigureAwait(false);

                    if (response.StatusCode != System.Net.HttpStatusCode.InternalServerError)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 exception route to return 500.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an empty HTTP/1.1 POST body is handled without failing.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11EmptyPostBodyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (ByteArrayContent content = new ByteArrayContent(Array.Empty<byte>()))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo"), content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 empty POST body to succeed.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/1.1 OPTIONS preflight request succeeds and emits CORS headers.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11OptionsPreflightAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod("OPTIONS"), new Uri(host.BaseAddress, "/hello")))
                {
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 OPTIONS preflight request to succeed.");
                    }

                    if (!response.Headers.Contains("Access-Control-Allow-Origin")
                        || !response.Headers.Contains("Access-Control-Allow-Methods")
                        || !response.Headers.Contains("Access-Control-Allow-Headers"))
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 OPTIONS preflight response to include CORS headers.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a request with many headers is handled and echoed correctly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11RequestWithManyHeadersAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/test/header-echo")))
                {
                    for (int i = 0; i < 50; i++)
                    {
                        request.Headers.TryAddWithoutValidation("X-Custom-Header-" + i.ToString(), "value-" + i.ToString());
                    }

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode
                        || !body.Contains("X-Custom-Header-0: value-0", StringComparison.Ordinal)
                        || !body.Contains("X-Custom-Header-49: value-49", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 many-headers response behavior.");
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

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/echo-body", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/echo", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.PUT, "/test/put", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("PUT response", context.Token).ConfigureAwait(false);
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

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/static/test", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Static route response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/hello", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Hello world", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/header-echo", async (HttpContextBase context) =>
            {
                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < context.Request.Headers.Count; i++)
                {
                    string key = context.Request.Headers.GetKey(i);
                    string[] values = context.Request.Headers.GetValues(i);

                    if (values == null) continue;

                    for (int j = 0; j < values.Length; j++)
                    {
                        builder.Append(key);
                        builder.Append(": ");
                        builder.Append(values[j]);
                        builder.Append('\n');
                    }
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(builder.ToString(), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/octet-stream";
                await context.Response.Send(context.Request.DataAsBytes, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo-string", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo-async", async (HttpContextBase context) =>
            {
                byte[] body = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(body), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-manual", async (HttpContextBase context) =>
            {
                byte[] body = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(body), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/chunked", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ChunkedTransfer = true;

                for (int i = 1; i <= 5; i++)
                {
                    byte[] chunk = Encoding.UTF8.GetBytes("Chunk " + i.ToString() + "\n");
                    bool isFinal = i == 5;
                    await context.Response.SendChunk(chunk, isFinal, context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/chunked-edge", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ChunkedTransfer = true;
                await context.Response.SendChunk(Array.Empty<byte>(), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes("single-byte\n"), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes(new string('x', 1024) + "\nlarge-chunk"), true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/sse", async (HttpContextBase context) =>
            {
                context.Response.ServerSentEvents = true;
                context.Response.StatusCode = 200;

                for (int i = 1; i <= 5; i++)
                {
                    bool isFinal = i == 5;
                    ServerSentEvent sse = new ServerSentEvent();
                    sse.Id = i.ToString();
                    sse.Data = "Event " + i.ToString();
                    await context.Response.SendEvent(sse, isFinal, context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/sse-edge", async (HttpContextBase context) =>
            {
                context.Response.ServerSentEvents = true;
                context.Response.StatusCode = 200;

                ServerSentEvent multiLineEvent = new ServerSentEvent();
                multiLineEvent.Data = "Line1\nLine2\nLine3";
                await context.Response.SendEvent(multiLineEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent specialCharactersEvent = new ServerSentEvent();
                specialCharactersEvent.Data = "Special: <>&\"'";
                await context.Response.SendEvent(specialCharactersEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent unicodeEvent = new ServerSentEvent();
                unicodeEvent.Data = "Unicode: 世界";
                await context.Response.SendEvent(unicodeEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent finalEvent = new ServerSentEvent();
                finalEvent.Data = "done";
                await context.Response.SendEvent(finalEvent, true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/double-send", async (HttpContextBase context) =>
            {
                await context.Response.Send("First response", context.Token).ConfigureAwait(false);

                try
                {
                    await context.Response.Send("Second response", context.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/error/test", async (HttpContextBase context) =>
            {
                throw new Exception("Test exception");
            });

            server.Routes.Preflight = async (HttpContextBase context) =>
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                context.Response.StatusCode = 200;
                await context.Response.Send(String.Empty, context.Token).ConfigureAwait(false);
            };
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

        private static async Task TestChunkedRequestBodyAsync(string relativePath, string body, string contentType)
        {
            if (String.IsNullOrEmpty(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri(host.BaseAddress, relativePath)))
                {
                    request.Content = new StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(body)));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                    request.Headers.TransferEncodingChunked = true;

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string echoed = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked request body to succeed.");
                    }

                    if (!String.Equals(echoed, body, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 chunked request body response.");
                    }
                }
            }
        }

        private static async Task TestBodyEchoAsync(string body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent(body, Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo"), content).ConfigureAwait(false);
                    string echoed = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 echo request to succeed.");
                    }

                    if (!String.Equals(echoed, body, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 echo response body.");
                    }
                }
            }
        }
    }
}
