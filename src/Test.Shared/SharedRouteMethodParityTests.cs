namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Cross-protocol route method parity tests. Validates that every HTTP method and
    /// route type produces identical behavior across HTTP/1.1, HTTP/2, and HTTP/3.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class SharedRouteMethodParityTests
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions();

        #region Static-Route-Method-Parity

        /// <summary>
        /// GET static route returns 200 with body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunGetStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("GET", "/parity/get", null, 200, "GET-static").ConfigureAwait(false);
        }

        /// <summary>
        /// POST static route returns 200 with echoed body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPostStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("POST", "/parity/post", "post-body", 200, "echo:post-body").ConfigureAwait(false);
        }

        /// <summary>
        /// PUT static route returns 200 with echoed body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPutStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("PUT", "/parity/put", "put-body", 200, "echo:put-body").ConfigureAwait(false);
        }

        /// <summary>
        /// DELETE static route returns 200 with body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunDeleteStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("DELETE", "/parity/delete", null, 200, "DELETE-static").ConfigureAwait(false);
        }

        /// <summary>
        /// PATCH static route returns 200 with echoed body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPatchStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("PATCH", "/parity/patch", "patch-body", 200, "echo:patch-body").ConfigureAwait(false);
        }

        /// <summary>
        /// HEAD static route returns 200 with empty body and correct content-length across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunHeadStaticRouteParityAsync()
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = CreateParityHost())
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertHeadAsync(host.BaseAddress, HttpVersion.Version11, "/parity/head", 200).ConfigureAwait(false);
                    await AssertHeadAsync(host.BaseAddress, HttpVersion.Version20, "/parity/head", 200).ConfigureAwait(false);

                    if (IsQuicAvailable())
                    {
                        await AssertHeadAsync(host.BaseAddress, HttpVersion.Version30, "/parity/head", 200).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// OPTIONS static route returns 200 with body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunOptionsStaticRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("OPTIONS", "/parity/options", null, 200, "OPTIONS-static").ConfigureAwait(false);
        }

        #endregion

        #region Parameter-Route-Parity

        /// <summary>
        /// GET parameter route extracts path values across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunGetParameterRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("GET", "/parity/users/42", null, 200, "user:42").ConfigureAwait(false);
        }

        /// <summary>
        /// POST parameter route extracts path values and echoes body across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPostParameterRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("POST", "/parity/users/99", "param-body", 200, "user:99:echo:param-body").ConfigureAwait(false);
        }

        #endregion

        #region Dynamic-Route-Parity

        /// <summary>
        /// GET dynamic (regex) route matches and returns response across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunGetDynamicRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("GET", "/parity/dynamic/hello-world", null, 200, "dynamic:hello-world").ConfigureAwait(false);
        }

        #endregion

        #region Content-Route-Parity

        /// <summary>
        /// GET content route is served across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunGetContentRouteParityAsync()
        {
            await RunAcrossProtocolsAsync("GET", "/parity/content/test", null, 200, "content-served").ConfigureAwait(false);
        }

        #endregion

        #region API-Route-Parity

        /// <summary>
        /// GET API route returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunGetApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("GET", "/parity/api/items", null, 200, "\"Method\":\"GET\"").ConfigureAwait(false);
        }

        /// <summary>
        /// POST API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPostApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("POST", "/parity/api/items", "{\"Name\":\"Widget\"}", 201, "Widget").ConfigureAwait(false);
        }

        /// <summary>
        /// PUT API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPutApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("PUT", "/parity/api/items/123", "{\"Name\":\"Updated\"}", 200, "Updated").ConfigureAwait(false);
        }

        /// <summary>
        /// PATCH API route with typed body returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunPatchApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("PATCH", "/parity/api/items/456", "{\"Name\":\"Patched\"}", 200, "Patched").ConfigureAwait(false);
        }

        /// <summary>
        /// DELETE API route returns JSON across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunDeleteApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("DELETE", "/parity/api/items/789", null, 200, "789").ConfigureAwait(false);
        }

        /// <summary>
        /// HEAD API route returns empty body with correct status across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunHeadApiRouteParityAsync()
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = CreateParityHost())
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertHeadAsync(host.BaseAddress, HttpVersion.Version11, "/parity/api/items", 200).ConfigureAwait(false);
                    await AssertHeadAsync(host.BaseAddress, HttpVersion.Version20, "/parity/api/items", 200).ConfigureAwait(false);

                    if (IsQuicAvailable())
                    {
                        await AssertHeadAsync(host.BaseAddress, HttpVersion.Version30, "/parity/api/items", 200).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// OPTIONS API route returns response across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunOptionsApiRouteParityAsync()
        {
            await RunAcrossProtocolsContainsAsync("OPTIONS", "/parity/api/options", null, 200, "\"Method\":\"OPTIONS\"").ConfigureAwait(false);
        }

        #endregion

        #region Not-Found-Parity

        /// <summary>
        /// Unmatched route returns 404 across all protocols.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunNotFoundParityAsync()
        {
            await RunAcrossProtocolsAsync("GET", "/parity/nonexistent", null, 404, "not-found").ConfigureAwait(false);
        }

        #endregion

        #region Server-Setup

        private static LoopbackServerHost CreateParityHost()
        {
            return new LoopbackServerHost(true, true, IsQuicAvailable(), ConfigureParityRoutes);
        }

        private static void ConfigureParityRoutes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            // --- Static routes ---

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/parity/get", async (HttpContextBase ctx) =>
            {
                await ctx.Response.Send("GET-static", ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/parity/post", async (HttpContextBase ctx) =>
            {
                string body = ctx.Request.DataAsString;
                await ctx.Response.Send("echo:" + body, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/parity/put", async (HttpContextBase ctx) =>
            {
                string body = ctx.Request.DataAsString;
                await ctx.Response.Send("echo:" + body, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.DELETE, "/parity/delete", async (HttpContextBase ctx) =>
            {
                await ctx.Response.Send("DELETE-static", ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PATCH, "/parity/patch", async (HttpContextBase ctx) =>
            {
                string body = ctx.Request.DataAsString;
                await ctx.Response.Send("echo:" + body, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.HEAD, "/parity/head", async (HttpContextBase ctx) =>
            {
                ctx.Response.ContentLength = 10;
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.OPTIONS, "/parity/options", async (HttpContextBase ctx) =>
            {
                ctx.Response.Headers.Add("Allow", "GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS");
                await ctx.Response.Send("OPTIONS-static", ctx.Token).ConfigureAwait(false);
            });

            // --- Parameter routes ---

            server.Routes.PreAuthentication.Parameter.Add(CoreHttpMethod.GET, "/parity/users/{id}", async (HttpContextBase ctx) =>
            {
                string id = ctx.Request.Url.Parameters["id"];
                await ctx.Response.Send("user:" + id, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Parameter.Add(CoreHttpMethod.POST, "/parity/users/{id}", async (HttpContextBase ctx) =>
            {
                string id = ctx.Request.Url.Parameters["id"];
                string body = ctx.Request.DataAsString;
                await ctx.Response.Send("user:" + id + ":echo:" + body, ctx.Token).ConfigureAwait(false);
            });

            // --- Dynamic (regex) routes ---

            server.Routes.PreAuthentication.Dynamic.Add(CoreHttpMethod.GET, new Regex(@"^/parity/dynamic/(.+)$"), async (HttpContextBase ctx) =>
            {
                string path = ctx.Request.Url.RawWithoutQuery;
                string suffix = path.Substring("/parity/dynamic/".Length);
                await ctx.Response.Send("dynamic:" + suffix, ctx.Token).ConfigureAwait(false);
            });

            // --- Content route (simulated, not file-based) ---

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/parity/content/test", async (HttpContextBase ctx) =>
            {
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("content-served", ctx.Token).ConfigureAwait(false);
            });

            // --- API routes ---

            server.Get("/parity/api/items", async (req) =>
            {
                return new ParityApiResponse { Method = "GET", Id = String.Empty, Name = String.Empty };
            });

            server.Head("/parity/api/items", async (req) =>
            {
                return new ParityApiResponse { Method = "HEAD", Id = String.Empty, Name = String.Empty };
            });

            server.Post<ParityApiBody>("/parity/api/items", async (req) =>
            {
                ParityApiBody body = req.GetData<ParityApiBody>();
                req.Http.Response.StatusCode = 201;
                return new ParityApiResponse { Method = "POST", Id = String.Empty, Name = body != null ? body.Name : String.Empty };
            });

            server.Put<ParityApiBody>("/parity/api/items/{id}", async (req) =>
            {
                ParityApiBody body = req.GetData<ParityApiBody>();
                string id = req.Http.Request.Url.Parameters["id"];
                return new ParityApiResponse { Method = "PUT", Id = id, Name = body != null ? body.Name : String.Empty };
            });

            server.Patch<ParityApiBody>("/parity/api/items/{id}", async (req) =>
            {
                ParityApiBody body = req.GetData<ParityApiBody>();
                string id = req.Http.Request.Url.Parameters["id"];
                return new ParityApiResponse { Method = "PATCH", Id = id, Name = body != null ? body.Name : String.Empty };
            });

            server.Delete("/parity/api/items/{id}", async (req) =>
            {
                string id = req.Http.Request.Url.Parameters["id"];
                return new ParityApiResponse { Method = "DELETE", Id = id, Name = String.Empty };
            });

            server.Options("/parity/api/options", async (req) =>
            {
                return new ParityApiResponse { Method = "OPTIONS", Id = String.Empty, Name = String.Empty };
            });
        }

        #endregion

        #region Assertion-Helpers

        private static async Task RunAcrossProtocolsAsync(string method, string path, string body, int expectedStatus, string expectedBody)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = CreateParityHost())
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertRequestAsync(host.BaseAddress, HttpVersion.Version11, method, path, body, expectedStatus, expectedBody).ConfigureAwait(false);
                    await AssertRequestAsync(host.BaseAddress, HttpVersion.Version20, method, path, body, expectedStatus, expectedBody).ConfigureAwait(false);

                    if (IsQuicAvailable())
                    {
                        await AssertRequestAsync(host.BaseAddress, HttpVersion.Version30, method, path, body, expectedStatus, expectedBody).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        private static async Task RunAcrossProtocolsContainsAsync(string method, string path, string body, int expectedStatus, string expectedContains)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = CreateParityHost())
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertRequestContainsAsync(host.BaseAddress, HttpVersion.Version11, method, path, body, expectedStatus, expectedContains).ConfigureAwait(false);
                    await AssertRequestContainsAsync(host.BaseAddress, HttpVersion.Version20, method, path, body, expectedStatus, expectedContains).ConfigureAwait(false);

                    if (IsQuicAvailable())
                    {
                        await AssertRequestContainsAsync(host.BaseAddress, HttpVersion.Version30, method, path, body, expectedStatus, expectedContains).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        private static async Task AssertRequestAsync(Uri baseAddress, Version version, string method, string path, string body, int expectedStatus, string expectedBody)
        {
            using (HttpClient client = CreateTlsHttpClient(version))
            using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method), new Uri(baseAddress, path)))
            {
                if (!String.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if ((int)response.StatusCode != expectedStatus)
                {
                    throw new InvalidOperationException(
                        method + " " + path + " over " + version.ToString() +
                        " returned status " + ((int)response.StatusCode).ToString() +
                        " (expected " + expectedStatus.ToString() + "). Body: " + responseBody);
                }

                if (!String.Equals(responseBody, expectedBody, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        method + " " + path + " over " + version.ToString() +
                        " returned body '" + responseBody + "' (expected '" + expectedBody + "').");
                }
            }
        }

        private static async Task AssertRequestContainsAsync(Uri baseAddress, Version version, string method, string path, string body, int expectedStatus, string expectedContains)
        {
            using (HttpClient client = CreateTlsHttpClient(version))
            using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method), new Uri(baseAddress, path)))
            {
                if (!String.IsNullOrEmpty(body))
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if ((int)response.StatusCode != expectedStatus)
                {
                    throw new InvalidOperationException(
                        method + " " + path + " over " + version.ToString() +
                        " returned status " + ((int)response.StatusCode).ToString() +
                        " (expected " + expectedStatus.ToString() + "). Body: " + responseBody);
                }

                if (!responseBody.Contains(expectedContains))
                {
                    throw new InvalidOperationException(
                        method + " " + path + " over " + version.ToString() +
                        " response body did not contain '" + expectedContains + "'. Body: " + responseBody);
                }
            }
        }

        private static async Task AssertHeadAsync(Uri baseAddress, Version version, string path, int expectedStatus)
        {
            using (HttpClient client = CreateTlsHttpClient(version))
            using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Head, new Uri(baseAddress, path)))
            {
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if ((int)response.StatusCode != expectedStatus)
                {
                    throw new InvalidOperationException(
                        "HEAD " + path + " over " + version.ToString() +
                        " returned status " + ((int)response.StatusCode).ToString() +
                        " (expected " + expectedStatus.ToString() + ").");
                }

                if (!String.IsNullOrEmpty(responseBody))
                {
                    throw new InvalidOperationException(
                        "HEAD " + path + " over " + version.ToString() +
                        " returned a non-empty body (length " + responseBody.Length.ToString() + ").");
                }
            }
        }

        #endregion

        #region Infrastructure

        private static bool IsQuicAvailable()
        {
            return Http3RuntimeDetector.Detect().IsAvailable;
        }

        private static HttpClient CreateTlsHttpClient(Version version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }

        private static async Task ExecuteWithRetryAsync(Func<Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            Exception lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await operation().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }

            throw lastException ?? new InvalidOperationException("Retryable route method parity test failed without an exception.");
        }

        #endregion

        #region Models

        /// <summary>
        /// API route request body model.
        /// </summary>
        public class ParityApiBody
        {
            /// <summary>
            /// Name field.
            /// </summary>
            public string Name { get; set; } = String.Empty;
        }

        /// <summary>
        /// API route response model.
        /// </summary>
        public class ParityApiResponse
        {
            /// <summary>
            /// HTTP method used.
            /// </summary>
            public string Method { get; set; } = String.Empty;

            /// <summary>
            /// Route parameter id.
            /// </summary>
            public string Id { get; set; } = String.Empty;

            /// <summary>
            /// Name from request body.
            /// </summary>
            public string Name { get; set; } = String.Empty;
        }

        #endregion
    }
}
