namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Versioning;
    using System.Text.Json;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Shared HTTP-level coverage for the OpenAPI document and Swagger UI endpoints. Each case
    /// spins up an isolated loopback server, exercises the endpoints over a real HTTP client, and
    /// tears the server down again. These complement the document-generation unit tests in
    /// <see cref="SharedOpenApiCompositionTests"/> by validating registration, the authentication
    /// toggle, and version round-tripping over the wire.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class SharedOpenApiEndpointTests
    {
        #region Public-Methods

        /// <summary>
        /// Get the shared OpenAPI endpoint test cases.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(new SharedNamedTestCase("OpenApi Endpoint :: empty document path throws", TestEmptyDocumentPathThrowsAsync));
            tests.Add(new SharedNamedTestCase("OpenApi Endpoint :: public document served without auth", TestPublicDocumentServedAsync));
            tests.Add(new SharedNamedTestCase("OpenApi Endpoint :: Swagger UI served as HTML", TestSwaggerUiServedAsync));
            tests.Add(new SharedNamedTestCase("OpenApi Endpoint :: protected document requires auth", TestProtectedDocumentRequiresAuthAsync));
            tests.Add(new SharedNamedTestCase("OpenApi Endpoint :: selected version round-trips over HTTP", TestVersionRoundTripsAsync));

            return tests.ToArray();
        }

        #endregion

        #region Test-Methods

        private static Task TestEmptyDocumentPathThrowsAsync()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 18099, false);
            using (Webserver server = new Webserver(settings, DefaultRouteAsync))
            {
                OpenApiSettings openApi = new OpenApiSettings();
                openApi.DocumentPath = String.Empty;

                try
                {
                    server.UseOpenApi(openApi);
                }
                catch (ArgumentException)
                {
                    return Task.CompletedTask;
                }

                throw new Exception("Expected ArgumentException for an empty OpenAPI document path.");
            }
        }

        private static Task TestPublicDocumentServedAsync()
        {
            return RunAsync(
                delegate (Webserver server)
                {
                    server.UseOpenApi(delegate (OpenApiSettings openApi)
                    {
                        openApi.Info.Title = "Endpoint Tests";
                        openApi.Info.Version = "1.0.0";
                    });
                    RegisterDocumentedRoute(server);
                },
                async delegate (HttpClient client)
                {
                    HttpResponseMessage response = await client.GetAsync("/openapi.json").ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AssertStatus(HttpStatusCode.OK, response.StatusCode, body);

                    using (JsonDocument doc = JsonDocument.Parse(body))
                    {
                        AssertTrue(doc.RootElement.TryGetProperty("openapi", out _), "Document should contain the openapi field.");
                    }
                });
        }

        private static Task TestSwaggerUiServedAsync()
        {
            return RunAsync(
                delegate (Webserver server)
                {
                    server.UseOpenApi(delegate (OpenApiSettings openApi)
                    {
                        openApi.Info.Title = "Endpoint Tests";
                    });
                    RegisterDocumentedRoute(server);
                },
                async delegate (HttpClient client)
                {
                    HttpResponseMessage response = await client.GetAsync("/swagger").ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AssertStatus(HttpStatusCode.OK, response.StatusCode, body);

                    string mediaType = response.Content.Headers.ContentType != null ? response.Content.Headers.ContentType.MediaType : null;
                    AssertEqual("text/html", mediaType);
                    AssertContains(body, "swagger-ui");
                });
        }

        private static Task TestProtectedDocumentRequiresAuthAsync()
        {
            return RunAsync(
                delegate (Webserver server)
                {
                    ConfigureBearerAuth(server);
                    server.UseOpenApi(delegate (OpenApiSettings openApi)
                    {
                        openApi.Info.Title = "Endpoint Tests";
                        openApi.RequireAuthentication = true;
                    });
                    RegisterDocumentedRoute(server);
                },
                async delegate (HttpClient client)
                {
                    HttpResponseMessage anonymous = await client.GetAsync("/openapi.json").ConfigureAwait(false);
                    string anonymousBody = await anonymous.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AssertStatus(HttpStatusCode.Unauthorized, anonymous.StatusCode, anonymousBody);
                    AssertNotContains(anonymousBody, "\"openapi\"");

                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/openapi.json");
                    request.Headers.Add("Authorization", "Bearer valid-token");
                    HttpResponseMessage authorized = await client.SendAsync(request).ConfigureAwait(false);
                    string authorizedBody = await authorized.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AssertStatus(HttpStatusCode.OK, authorized.StatusCode, authorizedBody);
                    AssertContains(authorizedBody, "\"openapi\"");
                });
        }

        private static Task TestVersionRoundTripsAsync()
        {
            return RunAsync(
                delegate (Webserver server)
                {
                    server.UseOpenApi(delegate (OpenApiSettings openApi)
                    {
                        openApi.Info.Title = "Endpoint Tests";
                        openApi.Version = OpenApiVersionEnum.V3_1;
                    });
                    RegisterDocumentedRoute(server);
                },
                async delegate (HttpClient client)
                {
                    HttpResponseMessage response = await client.GetAsync("/openapi.json").ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    AssertStatus(HttpStatusCode.OK, response.StatusCode, body);

                    using (JsonDocument doc = JsonDocument.Parse(body))
                    {
                        AssertEqual("3.1.1", doc.RootElement.GetProperty("openapi").GetString());
                    }
                });
        }

        #endregion

        #region Private-Methods

        private static async Task RunAsync(Action<Webserver> configure, Func<HttpClient, Task> body)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            if (body == null) throw new ArgumentNullException(nameof(body));

            LoopbackServerHost host = new LoopbackServerHost(
                false,
                false,
                false,
                configure,
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

        private static void ConfigureBearerAuth(Webserver server)
        {
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
        }

        private static void RegisterDocumentedRoute(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(
                WatsonWebserver.Core.HttpMethod.GET,
                "/ping",
                PingHandler,
                openApiMetadata: OpenApiRouteMetadata.Create("Ping", "System")
                    .WithResponse(200, OpenApiResponseMetadata.Text("pong")));
        }

        private static async Task PingHandler(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("pong", ctx.Token).ConfigureAwait(false);
        }

        private static async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            await ctx.Response.Send("not-found", ctx.Token).ConfigureAwait(false);
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

        private static void AssertNotContains(string body, string unexpectedSubstring)
        {
            if (body != null && body.IndexOf(unexpectedSubstring, StringComparison.Ordinal) >= 0)
            {
                throw new Exception("Expected response body to NOT contain '" + unexpectedSubstring + "'. Body: " + body);
            }
        }

        private static void AssertEqual(string expected, string actual)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new Exception("Expected '" + expected + "' but received '" + (actual ?? "<null>") + "'.");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        #endregion
    }
}
