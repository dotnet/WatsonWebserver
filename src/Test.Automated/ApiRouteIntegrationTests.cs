namespace Test.Automated
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using Xunit;

    /// <summary>
    /// Integration tests covering API-route behavior.
    /// </summary>
    public class ApiRouteIntegrationTests : IAsyncLifetime
    {
        private Webserver _Server;
        private HttpClient _Client;
        private int _Port;
        private CancellationTokenSource _Cts;

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            _Port = GetRandomPort();
            _Cts = new CancellationTokenSource();

            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, false);
            settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(5);

            _Server = new Webserver(settings, DefaultRoute);

            _Server.UseHealthCheck();

            _Server.Routes.AuthenticateApiRequest = async (ctx) =>
            {
                string auth = ctx.Request.RetrieveHeaderValue("Authorization");
                if (auth == "Bearer valid-token")
                {
                    return new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = AuthorizationResultEnum.Permitted,
                        Metadata = new { Role = "Admin" }
                    };
                }
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                };
            };

            _Server.Get("/hello", async (req) => new { Message = "Hello" });

            _Server.Get("/items/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                int detail = req.Query.GetInt("detail", 0);
                return new { Id = id, Detail = detail };
            });

            _Server.Post<TestBody>("/items", async (req) =>
            {
                TestBody body = req.GetData<TestBody>();
                req.Http.Response.StatusCode = 201;
                return new { Created = true, Name = body?.Name };
            });

            _Server.Post("/raw", async (req) =>
            {
                string raw = req.Http.Request.DataAsString;
                return new { Length = raw?.Length ?? 0 };
            });

            _Server.Put<TestBody>("/items/{id}", async (req) =>
            {
                TestBody body = req.GetData<TestBody>();
                Guid id = req.Parameters.GetGuid("id");
                return new { Updated = true, Id = id, Name = body?.Name };
            });

            _Server.Patch<TestBody>("/items/{id}", async (req) =>
            {
                TestBody body = req.GetData<TestBody>();
                return new { Patched = true, Name = body?.Name };
            });

            _Server.Delete("/items/{id}", async (req) =>
            {
                Guid id = req.Parameters.GetGuid("id");
                return new { Deleted = true, Id = id };
            });

            _Server.Get("/text", async (req) => "plain text");

            _Server.Get("/null", async (req) => null);

            _Server.Get("/tuple", async (req) =>
            {
                req.Http.Response.StatusCode = 202;
                return new { Custom = true };
            });

            _Server.Get("/error", async (req) =>
            {
                throw new WebserverException(ApiResultEnum.NotFound, "Item not found");
            });

            _Server.Get("/slow", async (req) =>
            {
                await Task.Delay(30000, req.CancellationToken);
                return "done";
            });

            _Server.Get("/protected", async (req) =>
            {
                return new { Secure = true, Metadata = req.Metadata };
            }, auth: true);

            _Server.Middleware.Add(async (ctx, next, token) =>
            {
                ctx.Response.Headers.Add("X-Middleware", "executed");
                await next();
            });

            _Server.Start(_Cts.Token);
            await Task.Delay(1000);

            _Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_Port}") };
        }

        /// <inheritdoc />
        public async Task DisposeAsync()
        {
            _Client?.Dispose();
            _Cts?.Cancel();
            _Server?.Dispose();
            _Cts?.Dispose();
        }

        /// <summary>
        /// Verifies that a basic API route returns JSON successfully.
        /// </summary>
        [Fact]
        public async Task Get_ReturnsJson()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/hello");
            string body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {(int)resp.StatusCode}: {body}");
            Assert.Contains("Hello", body);
        }

        /// <summary>
        /// Verifies parameter and query extraction for API routes.
        /// </summary>
        [Fact]
        public async Task Get_ExtractsParameters()
        {
            Guid id = Guid.NewGuid();
            HttpResponseMessage resp = await _Client.GetAsync($"/items/{id}?detail=5");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains(id.ToString(), body);
            Assert.Contains("5", body);
        }

        /// <summary>
        /// Verifies typed request-body deserialization for POST routes.
        /// </summary>
        [Fact]
        public async Task Post_DeserializesBody()
        {
            StringContent content = new StringContent("{\"Name\":\"Widget\"}", Encoding.UTF8, "application/json");
            HttpResponseMessage resp = await _Client.PostAsync("/items", content);
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Widget", body);
        }

        /// <summary>
        /// Verifies raw body access for non-generic POST routes.
        /// </summary>
        [Fact]
        public async Task Post_RawBody()
        {
            StringContent content = new StringContent("raw data here", Encoding.UTF8, "text/plain");
            HttpResponseMessage resp = await _Client.PostAsync("/raw", content);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("13", body);
        }

        /// <summary>
        /// Verifies PUT request handling.
        /// </summary>
        [Fact]
        public async Task Put_Works()
        {
            Guid id = Guid.NewGuid();
            StringContent content = new StringContent("{\"Name\":\"Updated\"}", Encoding.UTF8, "application/json");
            HttpResponseMessage resp = await _Client.PutAsync($"/items/{id}", content);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Updated", body);
        }

        /// <summary>
        /// Verifies PATCH request handling.
        /// </summary>
        [Fact]
        public async Task Patch_Works()
        {
            Guid id = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod("PATCH"), $"/items/{id}");
            request.Content = new StringContent("{\"Name\":\"Patched\"}", Encoding.UTF8, "application/json");
            HttpResponseMessage resp = await _Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Patched", body);
        }

        /// <summary>
        /// Verifies DELETE request handling.
        /// </summary>
        [Fact]
        public async Task Delete_Works()
        {
            Guid id = Guid.NewGuid();
            HttpResponseMessage resp = await _Client.DeleteAsync($"/items/{id}");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains(id.ToString(), body);
        }

        /// <summary>
        /// Verifies plain-text responses from string-returning routes.
        /// </summary>
        [Fact]
        public async Task StringReturn_TextPlain()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/text");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("plain text", body);
        }

        /// <summary>
        /// Verifies null results serialize to an empty successful response.
        /// </summary>
        [Fact]
        public async Task NullReturn_EmptyResponse()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/null");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        /// <summary>
        /// Verifies explicit response-status changes are preserved.
        /// </summary>
        [Fact]
        public async Task ExplicitStatusCodeReturn_CustomStatusCode()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/tuple");
            string body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.StatusCode == (HttpStatusCode)202, $"Expected 202 but got {(int)resp.StatusCode}: {body}");
            Assert.Contains("Custom", body);
        }

        /// <summary>
        /// Verifies webserver exceptions produce structured API errors.
        /// </summary>
        [Fact]
        public async Task WebserverException_ReturnsStructuredError()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/error");
            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("NotFound", body);
            Assert.Contains("Item not found", body);
        }

        /// <summary>
        /// Verifies unmatched routes still enforce API authentication behavior.
        /// </summary>
        [Fact]
        public async Task UnmatchedRoute_Returns401_WithAuthEnabled()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/nonexistent");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        /// <summary>
        /// Verifies timed-out API handlers return 408.
        /// </summary>
        [Fact]
        public async Task Timeout_Returns408()
        {
            using (HttpClient client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_Port}"), Timeout = TimeSpan.FromSeconds(15) })
            {
                HttpResponseMessage resp = await client.GetAsync("/slow");
                Assert.Equal((HttpStatusCode)408, resp.StatusCode);
                string body = await resp.Content.ReadAsStringAsync();
                Assert.Contains("RequestTimeout", body);
            }
        }

        /// <summary>
        /// Verifies protected routes reject requests without a bearer token.
        /// </summary>
        [Fact]
        public async Task ProtectedRoute_Returns401_WithoutToken()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/protected");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        /// <summary>
        /// Verifies protected routes accept requests with a valid bearer token.
        /// </summary>
        [Fact]
        public async Task ProtectedRoute_Returns200_WithValidToken()
        {
            HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, "/protected");
            request.Headers.Add("Authorization", "Bearer valid-token");
            HttpResponseMessage resp = await _Client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Secure", body);
        }

        /// <summary>
        /// Verifies middleware can mutate outgoing response headers.
        /// </summary>
        [Fact]
        public async Task Middleware_AddsHeader()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/hello");
            Assert.True(resp.Headers.Contains("X-Middleware"));
        }

        /// <summary>
        /// Verifies the health-check endpoint is available.
        /// </summary>
        [Fact]
        public async Task HealthCheck_Returns200()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", body);
        }

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send("{\"Error\":\"NotFound\"}");
        }

        private static int GetRandomPort()
        {
            using (System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                return port;
            }
        }
    }
}
