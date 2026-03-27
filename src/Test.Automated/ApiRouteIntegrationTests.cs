namespace Test.Automated
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using Xunit;

    public class ApiRouteIntegrationTests : IAsyncLifetime
    {
        private Webserver _Server;
        private HttpClient _Client;
        private int _Port;
        private CancellationTokenSource _Cts;

        public async Task InitializeAsync()
        {
            _Port = GetRandomPort();
            _Cts = new CancellationTokenSource();

            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, false);
            settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(5);

            _Server = new Webserver(settings, DefaultRoute);

            // Health check
            _Server.UseHealthCheck();

            // Auth
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

            // --- Routes ---
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

            _Server.Get("/tuple", async (req) => (new { Custom = true }, 202));

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

            // Middleware
            _Server.Middleware.Add(async (ctx, next, token) =>
            {
                ctx.Response.Headers.Add("X-Middleware", "executed");
                await next();
            });

            _Server.Start(_Cts.Token);
            await Task.Delay(1000); // Let server start

            _Client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_Port}") };
        }

        public async Task DisposeAsync()
        {
            _Client?.Dispose();
            _Cts?.Cancel();
            _Server?.Dispose();
            _Cts?.Dispose();
        }

        // --- GET tests ---

        [Fact]
        public async Task Get_ReturnsJson()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/hello");
            string body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.StatusCode == HttpStatusCode.OK, $"Expected 200 but got {(int)resp.StatusCode}: {body}");
            Assert.Contains("Hello", body);
        }

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

        // --- POST tests ---

        [Fact]
        public async Task Post_DeserializesBody()
        {
            StringContent content = new StringContent("{\"Name\":\"Widget\"}", Encoding.UTF8, "application/json");
            HttpResponseMessage resp = await _Client.PostAsync("/items", content);
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Widget", body);
        }

        [Fact]
        public async Task Post_RawBody()
        {
            StringContent content = new StringContent("raw data here", Encoding.UTF8, "text/plain");
            HttpResponseMessage resp = await _Client.PostAsync("/raw", content);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("13", body); // "raw data here" is 13 chars
        }

        // --- PUT / PATCH / DELETE ---

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

        [Fact]
        public async Task Delete_Works()
        {
            Guid id = Guid.NewGuid();
            HttpResponseMessage resp = await _Client.DeleteAsync($"/items/{id}");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains(id.ToString(), body);
        }

        // --- Response type tests ---

        [Fact]
        public async Task StringReturn_TextPlain()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/text");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Equal("plain text", body);
        }

        [Fact]
        public async Task NullReturn_EmptyResponse()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/null");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        [Fact]
        public async Task TupleReturn_CustomStatusCode()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/tuple");
            string body = await resp.Content.ReadAsStringAsync();
            Assert.True(resp.StatusCode == (HttpStatusCode)202, $"Expected 202 but got {(int)resp.StatusCode}: {body}");
            Assert.Contains("Custom", body);
        }

        // --- Error handling ---

        [Fact]
        public async Task WebserverException_ReturnsStructuredError()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/error");
            Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("NotFound", body);
            Assert.Contains("Item not found", body);
        }

        [Fact]
        public async Task UnmatchedRoute_Returns401_WithAuthEnabled()
        {
            // With structured auth enabled, unmatched pre-auth routes fall through to auth,
            // which returns 401 for unauthenticated requests
            HttpResponseMessage resp = await _Client.GetAsync("/nonexistent");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

        // --- Timeout ---

        [Fact]
        public async Task Timeout_Returns408()
        {
            using HttpClient client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_Port}"), Timeout = TimeSpan.FromSeconds(15) };
            HttpResponseMessage resp = await client.GetAsync("/slow");
            Assert.Equal((HttpStatusCode)408, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("RequestTimeout", body);
        }

        // --- Authentication ---

        [Fact]
        public async Task ProtectedRoute_Returns401_WithoutToken()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/protected");
            Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        }

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

        // --- Middleware ---

        [Fact]
        public async Task Middleware_AddsHeader()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/hello");
            Assert.True(resp.Headers.Contains("X-Middleware"));
        }

        // --- Health check ---

        [Fact]
        public async Task HealthCheck_Returns200()
        {
            HttpResponseMessage resp = await _Client.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("Healthy", body);
        }

        // --- Helpers ---

        private static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send("{\"Error\":\"NotFound\"}");
        }

        private static int GetRandomPort()
        {
            System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    public class TestBody
    {
        public string Name { get; set; }
    }
}
