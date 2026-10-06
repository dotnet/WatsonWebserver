namespace Test.Aot
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Watson.Clients;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using WatsonWebserver.Core.Http3;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Native AOT validation for Watson and Watson.Clients. Starts HTTP/1.1 and HTTP/2 (h2c) servers
    /// configured the way a native AOT application configures them, exercises routing, API routes, typed
    /// bodies, structured errors, timeouts, authentication, health checks, OpenAPI, middleware, chunked
    /// responses, server-sent events, the Prometheus telemetry endpoint, and WebSockets over loopback, and
    /// exits 0 when every check passes.
    /// Pass --require-native to fail when not running as a native AOT executable.
    /// </summary>
    public static class Program
    {
        #region Private-Members

        private static readonly Guid _UserId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        private static int _Passed = 0;
        private static int _Failed = 0;

        #endregion

        #region Entrypoint

        /// <summary>
        /// Entrypoint.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code: 0 when every check passes, 1 otherwise.</returns>
        public static async Task<int> Main(string[] args)
        {
            bool requireNative = args != null && Array.IndexOf(args, "--require-native") >= 0;
            bool native = !RuntimeFeature.IsDynamicCodeSupported;

            Console.WriteLine("Watson native AOT validation");
            Console.WriteLine("Runtime:    " + RuntimeInformation.FrameworkDescription + " (" + RuntimeInformation.RuntimeIdentifier + ")");
            Console.WriteLine("Native AOT: " + (native ? "yes" : "no (JIT; analyzers still verified at build time)"));
            Console.WriteLine("Reflection-based JSON enabled: " + JsonSerializer.IsReflectionEnabledByDefault);
            Console.WriteLine();

            Check("Runtime :: Running as native AOT when required", !requireNative || native, "Not a native AOT executable; publish with PublishAot.");

            CheckDefaultSerializer();
            CheckHttp3Detection();

            int http1Port = GetFreePort();
            int http2Port = GetFreePort();

            using (Webserver http1 = CreateServer(http1Port, false))
            using (Webserver http2 = CreateServer(http2Port, true))
            {
                http1.Start();
                http2.Start();
                await Task.Delay(250).ConfigureAwait(false);

                try
                {
                    await RunHttpChecksAsync("HTTP/1.1", http1Port, HttpVersion.Version11).ConfigureAwait(false);
                    await RunHttpChecksAsync("HTTP/2 h2c", http2Port, HttpVersion.Version20).ConfigureAwait(false);
                    await RunWebSocketCheckAsync(http1Port).ConfigureAwait(false);

                    Check("Statistics :: Server statistics render", !String.IsNullOrEmpty(http1.Statistics.ToString()), "Statistics rendered empty.");
                }
                finally
                {
                    http1.Stop();
                    http2.Stop();
                }
            }

            Console.WriteLine();
            Console.WriteLine("Passed: " + _Passed + "  Failed: " + _Failed);
            Console.WriteLine(_Failed == 0 ? "OVERALL PASS" : "OVERALL FAIL");
            return _Failed == 0 ? 0 : 1;
        }

        #endregion

        #region Checks

        private static void CheckDefaultSerializer()
        {
            using (Webserver server = new Webserver(new WebserverSettings("127.0.0.1", GetFreePort()), DefaultRouteAsync))
            {
                DefaultSerializationHelper serializer = server.Serializer as DefaultSerializationHelper;
                Check(
                    "Serializer :: Default serializer uses reflection only where it is enabled",
                    serializer != null && serializer.UsesReflection == JsonSerializer.IsReflectionEnabledByDefault,
                    "UsesReflection=" + (serializer != null ? serializer.UsesReflection.ToString() : "n/a"));

                string json = server.Serializer.SerializeJson(new ApiErrorResponse { Error = ApiResultEnum.NotFound }, false);
                Check(
                    "Serializer :: Default serializer writes Watson types without application metadata",
                    json == "{\"Error\":\"NotFound\",\"StatusCode\":404,\"Description\":\"The requested resource was not found.\"}",
                    json);
            }

            DefaultSerializationHelper metadata = new DefaultSerializationHelper(AotJsonContext.Default);
            AotUser user = metadata.DeserializeJson<AotUser>("{\"Name\":\"round\",\"Role\":\"Admin\",\"Tags\":[\"x\"]}");
            Check(
                "Serializer :: Application type round-trips through the context",
                user != null && user.Name == "round" && user.Role == AotUserRole.Admin && user.Tags.Count == 1,
                "Deserialized: " + (user != null ? user.Name + "/" + user.Role : "null"));
        }

        private static void CheckHttp3Detection()
        {
            Http3RuntimeAvailability availability = null;
            Exception error = null;

            try
            {
                availability = Http3RuntimeDetector.Detect();
            }
            catch (Exception e)
            {
                error = e;
            }

            Check("HTTP/3 :: QUIC runtime detection completes", availability != null && error == null, error != null ? error.Message : "No result.");
            if (availability != null) Console.WriteLine("    QUIC: " + availability.Message);
        }

        private static async Task RunHttpChecksAsync(string label, int port, Version version)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri("http://127.0.0.1:" + port + "/");
                client.DefaultRequestVersion = version;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                client.Timeout = TimeSpan.FromSeconds(15);

                HttpResponseMessage response = await client.GetAsync("plain").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Plain route", response.StatusCode == HttpStatusCode.OK && body == "hello" && response.Version == version, Describe(response, body));

                response = await client.GetAsync("users/" + _UserId).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string expectedUser = "{\"Id\":\"" + _UserId + "\",\"Name\":\"user\",\"Role\":\"Admin\",\"CreatedUtc\":\"2026-01-02T03:04:05.000000Z\",\"Tags\":[\"a\",\"b\"]}";
                Check(label + " :: API route returns an application type", response.StatusCode == HttpStatusCode.OK && body == expectedUser, Describe(response, body));
                Check(label + " :: API route sets JSON content type", response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.MediaType == "application/json", Describe(response, body));
                Check(label + " :: Middleware runs", response.Headers.Contains("X-Aot-Middleware"), Describe(response, body));

                response = await client.PostAsync("users", new StringContent("{\"Name\":\"new\",\"Role\":\"Admin\"}", Encoding.UTF8, "application/json")).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Typed request body", response.StatusCode == HttpStatusCode.Created && body.Contains("\"Name\":\"new\"") && body.Contains("\"Role\":\"Admin\""), Describe(response, body));

                response = await client.PostAsync("users", new StringContent("{\"Name\":", Encoding.UTF8, "application/json")).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Malformed JSON returns 400", response.StatusCode == HttpStatusCode.BadRequest && body.Contains("\"StatusCode\":400"), Describe(response, body));

                response = await client.GetAsync("files/a/b/c.txt").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Catch-all parameter route", response.StatusCode == HttpStatusCode.OK && body == "a/b/c.txt", Describe(response, body));

                response = await client.GetAsync("conflict").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                string expectedConflict = "{\"Error\":\"Conflict\",\"StatusCode\":409,\"Description\":\"The request conflicts with the current state of the resource.\",\"Message\":\"locked\",\"Data\":{\"orderId\":17,\"retry\":false}}";
                Check(label + " :: Structured error", response.StatusCode == HttpStatusCode.Conflict && body == expectedConflict, Describe(response, body));

                response = await client.GetAsync("slow").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Timeout returns 408", response.StatusCode == HttpStatusCode.RequestTimeout && body.Contains("\"Error\":\"RequestTimeout\""), Describe(response, body));

                response = await client.GetAsync("secure").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Authentication failure returns 401", response.StatusCode == HttpStatusCode.Unauthorized && body.Contains("\"Error\":\"NotAuthorized\""), Describe(response, body));

                response = await client.GetAsync("health").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Health check", response.StatusCode == HttpStatusCode.OK && body == "{\"Status\":\"Healthy\",\"Description\":\"ok\",\"Data\":{\"uptimeSeconds\":12.5,\"ready\":true}}", Describe(response, body));

                response = await client.GetAsync("unregistered").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Unregistered type returns a JSON 500 naming it", response.StatusCode == HttpStatusCode.InternalServerError && body.Contains(nameof(AotUnregistered)) && body.Contains("JsonSerializable"), Describe(response, body));

                response = await client.GetAsync("openapi.json").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(
                    label + " :: OpenAPI document",
                    response.StatusCode == HttpStatusCode.OK && body.Contains("\"openapi\": \"3.0") && body.Contains("\"/users/{id}\"") && body.Contains("\"name\": \"example\"") && body.Contains("\"role\": \"Admin\""),
                    Describe(response, body));

                response = await client.GetAsync("chunked").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Chunked response", response.StatusCode == HttpStatusCode.OK && body == "first,last", Describe(response, body));

                response = await client.GetAsync("events").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Server-sent events", response.StatusCode == HttpStatusCode.OK && body.Contains("event: greeting") && body.Contains("data: hello"), Describe(response, body));

                response = await client.GetAsync("metrics").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Prometheus telemetry endpoint", response.StatusCode == HttpStatusCode.OK && body.Contains("http_server_request_duration"), Describe(response, body.Length > 300 ? body.Substring(0, 300) : body));

                response = await client.GetAsync("missing").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Check(label + " :: Default route", response.StatusCode == HttpStatusCode.NotFound, Describe(response, body));
            }
        }

        private static async Task RunWebSocketCheckAsync(int port)
        {
            string received = null;
            Exception error = null;

            try
            {
                using (WatsonWebSocketClient client = new WatsonWebSocketClient(new Uri("ws://127.0.0.1:" + port + "/ws/echo")))
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(timeout.Token).ConfigureAwait(false);
                    await client.SendTextAsync("ping", timeout.Token).ConfigureAwait(false);
                    Watson.Clients.WebSocketMessage message = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
                    received = message != null ? message.Text : null;
                    await client.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            Check("WebSocket :: Watson.Clients echo round trip", received == "echo:ping", error != null ? error.GetType().Name + ": " + error.Message : "Received: " + received);
        }

        #endregion

        #region Server

        private static Webserver CreateServer(int port, bool http2)
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", port, false);
            if (http2)
            {
                settings.Protocols.EnableHttp2 = true;
                settings.Protocols.EnableHttp2Cleartext = true;
            }

            settings.Timeout.DefaultTimeout = TimeSpan.FromMilliseconds(500);
            settings.WebSockets.Enable = true;
            settings.Telemetry.Prometheus.Enable = true;

            Webserver server = new Webserver(settings, DefaultRouteAsync);
            server.Serializer = new DefaultSerializationHelper(AotJsonContext.Default);

            server.Middleware.Add(async (ctx, next, token) =>
            {
                ctx.Response.Headers.Add("X-Aot-Middleware", "1");
                await next().ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/plain", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("hello", ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/chunked", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.ChunkedTransfer = true;
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("first,"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("last"), true, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/events", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ServerSentEvents = true;
                await ctx.Response.SendEvent(new ServerSentEvent { Event = "greeting", Data = "hello" }, true, ctx.Token).ConfigureAwait(false);
            });

            server.Get("/users/{id}", (ApiRequest req) => Task.FromResult<object>(new AotUser
            {
                Id = req.Parameters.GetGuid("id"),
                Name = "user",
                Role = AotUserRole.Admin,
                Tags = new List<string> { "a", "b" }
            }));

            server.Post<AotCreateUserRequest>("/users", (ApiRequest req) =>
            {
                AotCreateUserRequest body = req.GetData<AotCreateUserRequest>();
                req.Http.Response.StatusCode = 201;
                return Task.FromResult<object>(new AotUser { Id = Guid.NewGuid(), Name = body.Name, Role = body.Role });
            });

            server.Get("/files/{*path}", (ApiRequest req) => Task.FromResult<object>(req.Parameters["path"]));

            server.Get("/conflict", (ApiRequest req) =>
            {
                throw new WebserverException(ApiResultEnum.Conflict, "locked")
                {
                    Data = new Dictionary<string, object> { { "orderId", 17 }, { "retry", false } }
                };
            });

            server.Get("/slow", async (ApiRequest req) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), req.CancellationToken).ConfigureAwait(false);
                return "late";
            });

            server.Get("/unregistered", (ApiRequest req) => Task.FromResult<object>(new AotUnregistered()));

            // Deny only /secure, so unmatched requests still reach the default route after authentication.
            server.Routes.AuthenticateApiRequest = (HttpContextBase ctx) =>
            {
                bool secure = String.Equals(ctx.Request.Url.RawWithoutQuery, "/secure", StringComparison.Ordinal);
                return Task.FromResult(new AuthResult
                {
                    AuthenticationResult = secure ? AuthenticationResultEnum.NotFound : AuthenticationResultEnum.Success,
                    AuthorizationResult = secure ? AuthorizationResultEnum.DeniedImplicit : AuthorizationResultEnum.Permitted
                });
            };

            server.Get("/secure", (ApiRequest req) => Task.FromResult<object>("secret"), auth: true);

            server.UseHealthCheck(health =>
            {
                health.CustomCheck = (CancellationToken token) => Task.FromResult(new HealthCheckResult
                {
                    Status = HealthStatusEnum.Healthy,
                    Description = "ok",
                    Data = new Dictionary<string, object> { { "uptimeSeconds", 12.5 }, { "ready", true } }
                });
            });

            server.UseOpenApi(api =>
            {
                api.Info.Title = "Watson AOT";
                api.Info.Version = "1.0.0";
                api.TypeInfoResolver = AotJsonContext.Default;
                api.Schemas["User"] = new OpenApiSchemaMetadata
                {
                    Type = "object",
                    Example = new AotUser { Name = "example", Role = AotUserRole.Admin }
                };
            });

            server.WebSocket("/ws/echo", async (HttpContextBase ctx, WebSocketSession session) =>
            {
                WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                await session.SendTextAsync("echo:" + message.Text, ctx.Token).ConfigureAwait(false);
            });

            return server;
        }

        private static async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("not found", ctx.Token).ConfigureAwait(false);
        }

        #endregion

        #region Helpers

        private static void Check(string name, bool passed, string detail)
        {
            if (passed)
            {
                _Passed++;
                Console.WriteLine("PASS  " + name);
            }
            else
            {
                _Failed++;
                Console.WriteLine("FAIL  " + name);
                Console.WriteLine("      " + detail);
            }
        }

        private static string Describe(HttpResponseMessage response, string body)
        {
            return "Status " + (int)response.StatusCode + ", HTTP/" + response.Version + ", body: " + body;
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}
