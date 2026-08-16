namespace Test.Shared
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;

    /// <summary>
    /// Shared integration coverage for FastAPI-style API routes registered directly on
    /// <see cref="WebserverBase"/>. Each case spins up an isolated loopback server, exercises the
    /// behavior over a real HTTP client, and tears the server down again.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class SharedApiRouteTests
    {
        #region Public-Methods

        /// <summary>
        /// Verify that a basic API route returns JSON successfully.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestGetReturnsJsonAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/hello").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "Hello");
            });
        }

        /// <summary>
        /// Verify parameter and query extraction for API routes.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestGetExtractsParametersAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                Guid id = Guid.NewGuid();
                HttpResponseMessage response = await client.GetAsync("/items/" + id.ToString() + "?detail=5").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, id.ToString());
                AssertContains(body, "5");
            });
        }

        /// <summary>
        /// Verify typed request-body deserialization for POST routes.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPostDeserializesBodyAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                StringContent content = new StringContent("{\"Name\":\"Widget\"}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/items", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.Created, response.StatusCode, body);
                AssertContains(body, "Widget");
            });
        }

        /// <summary>
        /// Verify raw body access for non-generic POST routes.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPostRawBodyAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                StringContent content = new StringContent("raw data here", Encoding.UTF8, "text/plain");
                HttpResponseMessage response = await client.PostAsync("/raw", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "13");
            });
        }

        /// <summary>
        /// Verify PUT request handling.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPutWorksAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                Guid id = Guid.NewGuid();
                StringContent content = new StringContent("{\"Name\":\"Updated\"}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync("/items/" + id.ToString(), content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "Updated");
            });
        }

        /// <summary>
        /// Verify PATCH request handling.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPatchWorksAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                Guid id = Guid.NewGuid();
                HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod("PATCH"), "/items/" + id.ToString());
                request.Content = new StringContent("{\"Name\":\"Patched\"}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "Patched");
            });
        }

        /// <summary>
        /// Verify DELETE request handling.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestDeleteWorksAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                Guid id = Guid.NewGuid();
                HttpResponseMessage response = await client.DeleteAsync("/items/" + id.ToString()).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, id.ToString());
            });
        }

        /// <summary>
        /// Verify plain-text responses from string-returning routes.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestStringReturnTextPlainAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/text").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertEqual("plain text", body);
            });
        }

        /// <summary>
        /// Verify null results serialize to an empty successful response.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestNullReturnEmptyResponseAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/null").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
            });
        }

        /// <summary>
        /// Verify explicit response-status changes are preserved.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestExplicitStatusCodeReturnAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/status").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus((HttpStatusCode)202, response.StatusCode, body);
                AssertContains(body, "Custom");
            });
        }

        /// <summary>
        /// Verify webserver exceptions produce structured API errors.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestWebserverExceptionReturnsStructuredErrorAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/error").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.NotFound, response.StatusCode, body);
                AssertContains(body, "NotFound");
                AssertContains(body, "Item not found");
            });
        }

        /// <summary>
        /// Verify a malformed JSON request body on a typed route yields a structured HTTP 400
        /// rather than crashing the handler. Exercises the System.Text.Json deserialization path.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPostMalformedJsonReturns400Async()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                StringContent content = new StringContent("{ this is not valid json", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/items", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // DeserializationError maps to HTTP 400. The enum name is intentionally not asserted:
                // ApiResultEnum.DeserializationError and ApiResultEnum.BadRequest share value 400, so the
                // serialized name is ambiguous; the status code is the stable contract.
                AssertStatus(HttpStatusCode.BadRequest, response.StatusCode, body);
                AssertContains(body, "400");
            });
        }

        /// <summary>
        /// Verify an unhandled (non-Webserver) exception thrown from an API handler produces a
        /// structured HTTP 500 InternalError response rather than an unstructured failure.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestUnhandledExceptionReturns500Async()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/throw").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.InternalServerError, response.StatusCode, body);
                AssertContains(body, "InternalError");
            });
        }

        /// <summary>
        /// Verify an empty request body on a typed POST route deserializes to a null model and the
        /// handler still completes successfully (no deserialization is attempted for an empty body).
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPostEmptyBodyTypedRouteAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                StringContent content = new StringContent(String.Empty, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/items", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.Created, response.StatusCode, body);
                AssertContains(body, "Created");
            });
        }

        /// <summary>
        /// Verify a valid JSON body carrying unknown properties still deserializes the known members
        /// (unknown members are ignored by default) and the typed route succeeds.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestPostUnknownFieldsIgnoredAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                StringContent content = new StringContent("{\"Name\":\"Widget\",\"Unknown\":42,\"Nested\":{\"X\":1}}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/items", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.Created, response.StatusCode, body);
                AssertContains(body, "Widget");
            });
        }

        /// <summary>
        /// Verify unmatched routes still enforce API authentication behavior.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestUnmatchedRouteReturns401Async()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/nonexistent").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.Unauthorized, response.StatusCode, body);
            });
        }

        /// <summary>
        /// Verify timed-out API handlers return HTTP 408.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestTimeoutReturns408Async()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/slow").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus((HttpStatusCode)408, response.StatusCode, body);
                AssertContains(body, "RequestTimeout");
            });
        }

        /// <summary>
        /// Verify protected routes reject requests without a bearer token.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestProtectedRouteReturns401WithoutTokenAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/protected").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.Unauthorized, response.StatusCode, body);
            });
        }

        /// <summary>
        /// Verify protected routes accept requests with a valid bearer token.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestProtectedRouteReturns200WithValidTokenAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/protected");
                request.Headers.Add("Authorization", "Bearer valid-token");
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "Secure");
            });
        }

        /// <summary>
        /// Verify middleware can mutate outgoing response headers.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestMiddlewareAddsHeaderAsync()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/hello").ConfigureAwait(false);
                if (!response.Headers.Contains("X-Middleware"))
                {
                    throw new Exception("Expected middleware header 'X-Middleware' was not present on the response.");
                }
            });
        }

        /// <summary>
        /// Verify the health-check endpoint is available.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestHealthCheckReturns200Async()
        {
            return RunAsync(async delegate (HttpClient client)
            {
                HttpResponseMessage response = await client.GetAsync("/health").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertStatus(HttpStatusCode.OK, response.StatusCode, body);
                AssertContains(body, "Healthy");
            });
        }

        #endregion

        #region Private-Methods

        private static async Task RunAsync(Func<HttpClient, Task> body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            LoopbackServerHost host = new LoopbackServerHost(
                false,
                false,
                false,
                ConfigureRoutes,
                delegate (WebserverSettings settings)
                {
                    settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(5);
                });

            try
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = host.BaseAddress;
                    client.Timeout = TimeSpan.FromSeconds(20);
                    await body(client).ConfigureAwait(false);
                }
            }
            finally
            {
                host.Dispose();
            }
        }

        private static void ConfigureRoutes(Webserver server)
        {
            server.UseHealthCheck();

            server.Routes.AuthenticateApiRequest = delegate (HttpContextBase ctx)
            {
                string auth = ctx.Request.RetrieveHeaderValue("Authorization");
                if (String.Equals(auth, "Bearer valid-token", StringComparison.Ordinal))
                {
                    return Task.FromResult(new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = AuthorizationResultEnum.Permitted,
                        Metadata = new { Role = "Admin" }
                    });
                }

                return Task.FromResult(new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                });
            };

            server.Get("/hello", async (req) => new { Message = "Hello" });

            server.Get("/items/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                int detail = req.Query.GetInt("detail", 0);
                return new { Id = id, Detail = detail };
            });

            server.Post<TestBody>("/items", async (req) =>
            {
                TestBody data = req.GetData<TestBody>();
                req.Http.Response.StatusCode = 201;
                return new { Created = true, Name = data != null ? data.Name : null };
            });

            server.Post("/raw", async (req) =>
            {
                string raw = req.Http.Request.DataAsString;
                return new { Length = raw != null ? raw.Length : 0 };
            });

            server.Put<TestBody>("/items/{id}", async (req) =>
            {
                TestBody data = req.GetData<TestBody>();
                Guid id = req.Parameters.GetGuid("id");
                return new { Updated = true, Id = id, Name = data != null ? data.Name : null };
            });

            server.Patch<TestBody>("/items/{id}", async (req) =>
            {
                TestBody data = req.GetData<TestBody>();
                return new { Patched = true, Name = data != null ? data.Name : null };
            });

            server.Delete("/items/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                return new { Deleted = true, Id = id };
            });

            server.Get("/text", async (req) => "plain text");

            server.Get("/null", async (req) => (object)null);

            server.Get("/status", async (req) =>
            {
                req.Http.Response.StatusCode = 202;
                return new { Custom = true };
            });

            server.Get("/error", async (req) =>
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Item not found");
            });

            server.Get("/throw", async (req) =>
            {
                throw new InvalidOperationException("Unhandled handler failure");
            });

            server.Get("/slow", async (req) =>
            {
                await Task.Delay(30000, req.CancellationToken).ConfigureAwait(false);
                return "done";
            });

            server.Get("/protected", async (req) =>
            {
                return new { Secure = true, Metadata = req.Metadata };
            }, auth: true);

            server.Middleware.Add(async (ctx, next, token) =>
            {
                ctx.Response.Headers.Add("X-Middleware", "executed");
                await next().ConfigureAwait(false);
            });
        }

        private static void AssertStatus(HttpStatusCode expected, HttpStatusCode actual, string body)
        {
            if (expected != actual)
            {
                throw new Exception("Expected HTTP " + ((int)expected).ToString() + " but received HTTP " + ((int)actual).ToString() + ". Body: " + body);
            }
        }

        private static void AssertContains(string body, string expectedSubstring)
        {
            if (body == null || body.IndexOf(expectedSubstring, StringComparison.Ordinal) < 0)
            {
                throw new Exception("Expected response body to contain '" + expectedSubstring + "'. Body: " + (body ?? "<null>"));
            }
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new Exception("Expected response body '" + expected + "' but received '" + (actual ?? "<null>") + "'.");
            }
        }

        #endregion
    }
}
