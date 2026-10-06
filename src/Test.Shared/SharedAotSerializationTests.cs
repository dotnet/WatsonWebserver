namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;
    using Test.Shared.Aot;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Health;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Coverage for native AOT and trimming support: the source-generated serialization path of
    /// <see cref="DefaultSerializationHelper"/> and of OpenAPI document generation. Each source-generated
    /// path runs without reflection, as it does under native AOT, and is compared against the
    /// reflection-based path so the two stay equivalent. A native AOT publish of a real application is
    /// covered separately by the Test.Aot project.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class SharedAotSerializationTests
    {
        #region Public-Methods

        /// <summary>
        /// Get the AOT serialization tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("Serializer :: Parameterless constructor uses reflection where it is enabled", TestParameterlessUsesReflection));
            tests.Add(CreateSync("Serializer :: Webserver default serializer uses reflection where it is enabled", TestWebserverDefaultUsesReflection));
            tests.Add(CreateSync("Serializer :: Resolver constructor rejects null", TestResolverConstructorRejectsNull));
            tests.Add(CreateSync("Serializer :: Resolver constructor never uses reflection", TestResolverConstructorNeverUsesReflection));
            tests.Add(CreateSync("Serializer :: Watson types serialize with no application resolver", TestWatsonTypesWithEmptyResolver));
            tests.Add(CreateSync("Serializer :: ApiErrorResponse output matches reflection", TestApiErrorResponseMatchesReflection));
            tests.Add(CreateSync("Serializer :: HealthCheckResult output matches reflection", TestHealthCheckResultMatchesReflection));
            tests.Add(CreateSync("Serializer :: Application type output matches reflection", TestApplicationTypeMatchesReflection));
            tests.Add(CreateSync("Serializer :: Application type round-trips", TestApplicationTypeRoundTrips));
            tests.Add(CreateSync("Serializer :: Enums are numbers without UseStringEnumConverter", TestEnumsAreNumbersWithoutStringEnumOption));
            tests.Add(CreateSync("Serializer :: Registered exception output matches reflection", TestRegisteredExceptionMatchesReflection));
            tests.Add(CreateSync("Serializer :: Exception data without metadata is written as text", TestExceptionDataWithoutMetadataWrittenAsText));
            tests.Add(CreateSync("Serializer :: Unregistered type fails serialization with guidance", TestUnregisteredSerializeFailsWithGuidance));
            tests.Add(CreateSync("Serializer :: Unregistered type fails deserialization with guidance", TestUnregisteredDeserializeFailsWithGuidance));
            tests.Add(CreateSync("Serializer :: Malformed JSON still raises JsonException", TestMalformedJsonRaisesJsonException));

            tests.Add(CreateSync("OpenAPI :: Document serializes without reflection and matches reflection output", TestOpenApiWithoutReflectionMatches));
            tests.Add(CreateSync("OpenAPI :: Application example type resolves through settings", TestOpenApiExampleThroughSettingsResolver));
            tests.Add(CreateSync("OpenAPI :: Unregistered example type fails without reflection", TestOpenApiUnregisteredExampleFails));
            tests.Add(CreateSync("OpenAPI :: Replacing SerializerOptions takes effect", TestOpenApiReplacingSerializerOptions));

            tests.Add(CreateAsync("HTTP :: API routes serve application types through the resolver", TestHttpApiRoutesThroughResolverAsync));
            tests.Add(CreateAsync("HTTP :: Typed request body deserializes through the resolver", TestHttpTypedBodyThroughResolverAsync));
            tests.Add(CreateAsync("HTTP :: Structured errors, timeouts, and auth failures serialize through the resolver", TestHttpErrorsThroughResolverAsync));
            tests.Add(CreateAsync("HTTP :: Health check serializes through the resolver", TestHttpHealthThroughResolverAsync));
            tests.Add(CreateAsync("HTTP :: Unregistered response type returns a JSON 500 naming the type", TestHttpUnregisteredResponseTypeAsync));

            return tests;
        }

        #endregion

        #region Serializer-Tests

        private static void TestParameterlessUsesReflection()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper();
            AssertTrue(serializer.UsesReflection, "The parameterless constructor should use reflection in a JIT application.");
        }

        private static void TestWebserverDefaultUsesReflection()
        {
            using (Webserver server = new Webserver(new WebserverSettings("127.0.0.1", 1), NoOpAsync))
            {
                DefaultSerializationHelper serializer = server.Serializer as DefaultSerializationHelper;
                AssertTrue(serializer != null, "The default serializer should be a DefaultSerializationHelper.");
                AssertTrue(serializer.UsesReflection, "The default serializer should use reflection in a JIT application.");
            }
        }

        private static void TestResolverConstructorRejectsNull()
        {
            bool threw = false;
            try
            {
                new DefaultSerializationHelper((IJsonTypeInfoResolver)null);
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }

            AssertTrue(threw, "A null resolver should throw ArgumentNullException.");
        }

        private static void TestResolverConstructorNeverUsesReflection()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            AssertTrue(!serializer.UsesReflection, "The resolver constructor should never use reflection.");
        }

        private static void TestWatsonTypesWithEmptyResolver()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(JsonTypeInfoResolver.Combine());
            string json = serializer.SerializeJson(new ApiErrorResponse { Error = ApiResultEnum.NotFound, Message = "missing" }, false);
            AssertEquals(
                "{\"Error\":\"NotFound\",\"StatusCode\":404,\"Description\":\"The requested resource was not found.\",\"Message\":\"missing\"}",
                json,
                "ApiErrorResponse should serialize from Watson's own metadata.");
        }

        private static void TestApiErrorResponseMatchesReflection()
        {
            List<ApiErrorResponse> cases = new List<ApiErrorResponse>();
            cases.Add(new ApiErrorResponse { Error = ApiResultEnum.NotAuthorized });
            cases.Add(new ApiErrorResponse { Error = ApiResultEnum.RequestTimeout, Message = "The request timed out." });
            cases.Add(new ApiErrorResponse { Error = ApiResultEnum.DeserializationError, Message = "bad json" });
            cases.Add(new ApiErrorResponse
            {
                Error = ApiResultEnum.Conflict,
                Message = "conflict \"quoted\" <tag>",
                Data = new Dictionary<string, object>
                {
                    { "id", Guid.Parse("aaaaaaaa-2222-3333-4444-555555555555") },
                    { "count", 3 },
                    { "big", 12345678901L },
                    { "ratio", 0.5 },
                    { "ok", true },
                    { "when", new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc) },
                    { "tags", new List<object> { "a", 1, null } },
                    { "names", new List<string> { "x", "y" } },
                    { "nested", new Dictionary<string, object> { { "k", "v" } } },
                    { "nul", null }
                }
            });
            cases.Add(new ApiErrorResponse { Error = ApiResultEnum.BadRequest, Data = new AotTestItem { Number = 5, Shade = AotTestShade.Dark } });

            AssertMatchesReflection(cases);
        }

        private static void TestHealthCheckResultMatchesReflection()
        {
            List<HealthCheckResult> cases = new List<HealthCheckResult>();
            cases.Add(new HealthCheckResult());
            cases.Add(new HealthCheckResult
            {
                Status = HealthStatusEnum.Degraded,
                Description = "degraded",
                Data = new Dictionary<string, object> { { "uptime", 12.5 }, { "ok", false }, { "name", "svc" }, { "connections", 4 } }
            });

            AssertMatchesReflection(cases);
        }

        private static void TestApplicationTypeMatchesReflection()
        {
            List<AotTestDto> cases = new List<AotTestDto>();
            cases.Add(new AotTestDto());
            AssertMatchesReflection(cases);
        }

        private static void TestApplicationTypeRoundTrips()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            AotTestItem item = serializer.DeserializeJson<AotTestItem>("{\"Number\":9,\"Shade\":\"Dark\"}");
            AssertEquals(9, item.Number, "Number should deserialize.");
            AssertEquals(AotTestShade.Dark, item.Shade, "A string enum should deserialize with UseStringEnumConverter.");

            string json = serializer.SerializeJson(item, false);
            AssertEquals("{\"Number\":9,\"Shade\":\"Dark\"}", json, "The item should serialize back to the same JSON.");
        }

        private static void TestEnumsAreNumbersWithoutStringEnumOption()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestNumericEnumJsonContext.Default);
            string json = serializer.SerializeJson(new AotTestItem { Number = 1, Shade = AotTestShade.Dark }, false);
            AssertEquals("{\"Number\":1,\"Shade\":1}", json, "Without UseStringEnumConverter an application enum should be written as a number.");

            string error = serializer.SerializeJson(new ApiErrorResponse { Error = ApiResultEnum.Conflict }, false);
            AssertContains(error, "\"Error\":\"Conflict\"", "Watson's own enums should still be written as strings.");
        }

        private static void TestRegisteredExceptionMatchesReflection()
        {
            Exception thrown;
            try
            {
                throw new InvalidOperationException("outer", new ArgumentException("inner"));
            }
            catch (Exception e)
            {
                thrown = e;
            }

            thrown.Data["key"] = "value";
            thrown.Data[42] = "int-key";
            thrown.HelpLink = "https://help.example.com";

            List<Exception> cases = new List<Exception>();
            cases.Add(thrown);
            cases.Add(new InvalidOperationException("unthrown"));
            AssertMatchesReflection(cases);
        }

        private static void TestExceptionDataWithoutMetadataWrittenAsText()
        {
            InvalidOperationException exception = new InvalidOperationException("data");
            exception.Data["custom"] = new AotTestUnregistered { Value = 4 };

            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            string json = serializer.SerializeJson(exception, false);
            AssertContains(json, "\"custom\":\"unregistered:4\"", "An exception data value without metadata should be written as its string form.");
        }

        private static void TestUnregisteredSerializeFailsWithGuidance()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            InvalidOperationException caught = null;
            try
            {
                serializer.SerializeJson(new AotTestUnregistered(), false);
            }
            catch (InvalidOperationException e)
            {
                caught = e;
            }

            AssertTrue(caught != null, "Serializing an unregistered type should throw InvalidOperationException.");
            AssertContains(caught.Message, typeof(AotTestUnregistered).FullName, "The message should name the type.");
            AssertContains(caught.Message, "[JsonSerializable(typeof(AotTestUnregistered))]", "The message should show the registration attribute.");
            AssertContains(caught.Message, "new DefaultSerializationHelper(", "The message should show how to assign the serializer.");
        }

        private static void TestUnregisteredDeserializeFailsWithGuidance()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            InvalidOperationException caught = null;
            try
            {
                serializer.DeserializeJson<AotTestUnregistered>("{\"Value\":1}");
            }
            catch (InvalidOperationException e)
            {
                caught = e;
            }

            AssertTrue(caught != null, "Deserializing an unregistered type should throw InvalidOperationException.");
            AssertContains(caught.Message, typeof(AotTestUnregistered).FullName, "The message should name the type.");
        }

        private static void TestMalformedJsonRaisesJsonException()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            bool threw = false;
            try
            {
                serializer.DeserializeJson<AotTestItem>("{\"Number\":");
            }
            catch (JsonException)
            {
                threw = true;
            }

            AssertTrue(threw, "Malformed JSON should raise JsonException so API routes still return 400.");
        }

        #endregion

        #region OpenApi-Tests

        private static void TestOpenApiWithoutReflectionMatches()
        {
            foreach (OpenApiVersionEnum version in new OpenApiVersionEnum[] { OpenApiVersionEnum.V3_0, OpenApiVersionEnum.V3_1, OpenApiVersionEnum.V3_2 })
            {
                using (Webserver server = CreateOpenApiServer())
                {
                    OpenApiSettings settings = CreateOpenApiSettings(version);

                    string reflection = new OpenApiDocumentGenerator().Generate(server.Routes, settings);

                    OpenApiDocumentGenerator metadataOnly = new OpenApiDocumentGenerator();
                    metadataOnly.SerializerOptions = CreateOpenApiOptions(JsonTypeInfoResolver.Combine());
                    string metadata = metadataOnly.Generate(server.Routes, settings);

                    AssertEquals(reflection, metadata, "The " + version + " document should be identical without reflection.");
                    AssertContains(metadata, "\"openapi\"", "The " + version + " document should be generated.");
                }
            }
        }

        private static void TestOpenApiExampleThroughSettingsResolver()
        {
            using (Webserver server = CreateOpenApiServer())
            {
                OpenApiSettings settings = CreateOpenApiSettings(OpenApiVersionEnum.V3_0);
                settings.Schemas["Example"] = new OpenApiSchemaMetadata { Type = "object", Example = new AotTestExample() };

                string reflection = new OpenApiDocumentGenerator().Generate(server.Routes, settings);

                settings.TypeInfoResolver = AotTestJsonContext.Default;
                OpenApiDocumentGenerator metadataOnly = new OpenApiDocumentGenerator();
                metadataOnly.SerializerOptions = CreateOpenApiOptions(JsonTypeInfoResolver.Combine());
                string metadata = metadataOnly.Generate(server.Routes, settings);

                AssertEquals(reflection, metadata, "An example of an application type should match reflection output when resolved through settings.");
                AssertContains(metadata, "\"displayName\": \"example\"", "The example should use the configured camel-case naming policy.");
            }
        }

        private static void TestOpenApiUnregisteredExampleFails()
        {
            using (Webserver server = CreateOpenApiServer())
            {
                OpenApiSettings settings = CreateOpenApiSettings(OpenApiVersionEnum.V3_0);
                settings.Schemas["Example"] = new OpenApiSchemaMetadata { Type = "object", Example = new AotTestUnregistered() };

                OpenApiDocumentGenerator metadataOnly = new OpenApiDocumentGenerator();
                metadataOnly.SerializerOptions = CreateOpenApiOptions(JsonTypeInfoResolver.Combine());

                bool threw = false;
                try
                {
                    metadataOnly.Generate(server.Routes, settings);
                }
                catch (NotSupportedException e)
                {
                    threw = e.Message.Contains(nameof(AotTestUnregistered));
                }

                AssertTrue(threw, "An example type with no metadata should fail generation and name the type when reflection is unavailable.");
            }
        }

        private static void TestOpenApiReplacingSerializerOptions()
        {
            using (Webserver server = CreateOpenApiServer())
            {
                OpenApiSettings settings = CreateOpenApiSettings(OpenApiVersionEnum.V3_0);
                OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();

                string indented = generator.Generate(server.Routes, settings);
                AssertContains(indented, "\n", "The default options should write indented JSON.");

                JsonSerializerOptions compact = CreateOpenApiOptions(null);
                compact.WriteIndented = false;
                generator.SerializerOptions = compact;
                string compactJson = generator.Generate(server.Routes, settings);
                AssertTrue(!compactJson.Contains("\n"), "Assigning new options should take effect on the next generation.");

                generator.SerializerOptions = null;
                string defaults = generator.Generate(server.Routes, settings);
                AssertTrue(defaults.Contains("\"openapi\""), "Null options should fall back to default options.");
            }
        }

        #endregion

        #region Http-Tests

        private static async Task TestHttpApiRoutesThroughResolverAsync()
        {
            await RunResolverServerAsync(async (HttpClient client) =>
            {
                string reflection = new DefaultSerializationHelper().SerializeJson(new AotTestDto(), false);

                HttpResponseMessage response = await client.GetAsync("/dto").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.OK, response.StatusCode, "GET /dto should succeed. Body: " + body);
                AssertEquals("application/json", response.Content.Headers.ContentType?.MediaType, "GET /dto should return JSON.");
                AssertEquals(reflection, body, "The response should match the reflection-based serializer.");

                response = await client.GetAsync("/text").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals("plain", body, "A string result should be sent as text.");

                response = await client.GetAsync("/number").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals("42", body, "A primitive result should be sent as text.");
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpTypedBodyThroughResolverAsync()
        {
            await RunResolverServerAsync(async (HttpClient client) =>
            {
                StringContent content = new StringContent("{\"Number\":7,\"Shade\":\"Dark\"}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/items", content).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.Created, response.StatusCode, "POST /items should succeed. Body: " + body);
                AssertEquals("{\"Number\":8,\"Shade\":\"Dark\"}", body, "The typed body should deserialize and the result serialize through the resolver.");

                content = new StringContent("{\"Number\":", Encoding.UTF8, "application/json");
                response = await client.PostAsync("/items", content).ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.BadRequest, response.StatusCode, "Malformed JSON should return 400. Body: " + body);
                // DeserializationError and BadRequest share value 400, so the enum is written by its first name,
                // exactly as the reflection-based serializer writes it.
                AssertContains(body, "\"Error\":\"BadRequest\",\"StatusCode\":400", "The 400 should be a structured error.");
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpErrorsThroughResolverAsync()
        {
            await RunResolverServerAsync(async (HttpClient client) =>
            {
                HttpResponseMessage response = await client.GetAsync("/conflict").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.Conflict, response.StatusCode, "The WebserverException should set the status. Body: " + body);
                AssertEquals(
                    "{\"Error\":\"Conflict\",\"StatusCode\":409,\"Description\":\"The request conflicts with the current state of the resource.\",\"Message\":\"locked\",\"Data\":{\"orderId\":17,\"reason\":\"locked\"}}",
                    body,
                    "The structured error should serialize through Watson's metadata.");

                response = await client.GetAsync("/slow").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.RequestTimeout, response.StatusCode, "The timeout should return 408. Body: " + body);
                AssertContains(body, "\"Error\":\"RequestTimeout\"", "The timeout should be a structured error.");

                response = await client.GetAsync("/secure").ConfigureAwait(false);
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.Unauthorized, response.StatusCode, "The auth failure should return 401. Body: " + body);
                AssertContains(body, "\"Error\":\"NotAuthorized\"", "The auth failure should be a structured error.");
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpHealthThroughResolverAsync()
        {
            await RunResolverServerAsync(async (HttpClient client) =>
            {
                HttpResponseMessage response = await client.GetAsync("/health").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.OK, response.StatusCode, "The health check should succeed. Body: " + body);
                AssertEquals("{\"Status\":\"Healthy\",\"Description\":\"ok\",\"Data\":{\"connections\":3}}", body, "The health result should serialize through Watson's metadata.");
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpUnregisteredResponseTypeAsync()
        {
            await RunResolverServerAsync(async (HttpClient client) =>
            {
                HttpResponseMessage response = await client.GetAsync("/unregistered").ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                AssertEquals(HttpStatusCode.InternalServerError, response.StatusCode, "An unregistered response type should return 500. Body: " + body);
                AssertContains(body, "\"Error\":\"InternalError\"", "The 500 should be a structured error.");
                AssertContains(body, nameof(AotTestUnregistered), "The 500 should name the unregistered type.");
            }).ConfigureAwait(false);
        }

        #endregion

        #region Helpers

        private static void AssertMatchesReflection<T>(List<T> cases)
        {
            DefaultSerializationHelper reflection = new DefaultSerializationHelper();
            DefaultSerializationHelper metadata = new DefaultSerializationHelper(AotTestJsonContext.Default);

            for (int i = 0; i < cases.Count; i++)
            {
                foreach (bool pretty in new bool[] { false, true })
                {
                    string expected = reflection.SerializeJson(cases[i], pretty);
                    string actual = metadata.SerializeJson(cases[i], pretty);
                    AssertEquals(expected, actual, typeof(T).Name + " case " + i + (pretty ? " (pretty)" : " (compact)") + " should match reflection output.");
                }
            }
        }

        private static async Task RunResolverServerAsync(Func<HttpClient, Task> test)
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureResolverServer))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = host.BaseAddress;
                    await test(client).ConfigureAwait(false);
                }
            }
        }

        private static void ConfigureResolverServer(Webserver server)
        {
            server.Serializer = new DefaultSerializationHelper(AotTestJsonContext.Default);
            server.Settings.Timeout.DefaultTimeout = TimeSpan.FromMilliseconds(500);

            server.Get("/dto", async (ApiRequest req) => new AotTestDto());
            server.Get("/text", async (ApiRequest req) => "plain");
            server.Get("/number", async (ApiRequest req) => 42);
            server.Get("/unregistered", async (ApiRequest req) => new AotTestUnregistered());

            server.Post<AotTestItem>("/items", async (ApiRequest req) =>
            {
                AotTestItem item = req.GetData<AotTestItem>();
                item.Number++;
                req.Http.Response.StatusCode = 201;
                return item;
            });

            server.Get("/conflict", async (ApiRequest req) =>
            {
                throw new WebserverException(ApiResultEnum.Conflict, "locked")
                {
                    Data = new Dictionary<string, object> { { "orderId", 17 }, { "reason", "locked" } }
                };
            });

            server.Get("/slow", async (ApiRequest req) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), req.CancellationToken).ConfigureAwait(false);
                return "late";
            });

            server.Routes.AuthenticateApiRequest = (HttpContextBase ctx) =>
            {
                return Task.FromResult(new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                });
            };

            server.Get("/secure", async (ApiRequest req) => "secret", auth: true);

            server.UseHealthCheck(health =>
            {
                health.CustomCheck = (token) => Task.FromResult(new HealthCheckResult
                {
                    Status = HealthStatusEnum.Healthy,
                    Description = "ok",
                    Data = new Dictionary<string, object> { { "connections", 3 } }
                });
            });
        }

        private static Webserver CreateOpenApiServer()
        {
            Webserver server = new Webserver(new WebserverSettings("127.0.0.1", 1), NoOpAsync);

            server.Get("/users/{id}", async (ApiRequest req) => "user", openApi: metadata =>
            {
                metadata.Summary = "Get user";
                metadata.Tags = new List<string> { "Users" };
                metadata.OperationId = "getUser";
                metadata.Deprecated = true;
                metadata.Parameters = new List<OpenApiParameterMetadata>
                {
                    OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()),
                    new OpenApiParameterMetadata { Name = "q", In = ParameterLocation.Query, Example = 12.75, AllowEmptyValue = true, Schema = OpenApiSchemaMetadata.Number() }
                };
                metadata.Responses = new Dictionary<string, OpenApiResponseMetadata>
                {
                    ["200"] = OpenApiResponseMetadata.Json("ok", OpenApiSchemaMetadata.CreateRef("User")),
                    ["404"] = OpenApiResponseMetadata.NotFound()
                };
                metadata.Security = new List<string> { "ApiKey" };
            });

            server.Post<AotTestItem>("/users", async (ApiRequest req) => "created", openApi: metadata =>
            {
                metadata.Summary = "Create";
                metadata.RequestBody = new OpenApiRequestBodyMetadata
                {
                    Required = true,
                    Description = "body",
                    Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                    {
                        ["application/json"] = new OpenApiMediaTypeMetadata
                        {
                            Schema = OpenApiSchemaMetadata.CreateRef("User"),
                            Example = new Dictionary<string, object> { ["id"] = 1, ["name"] = "bob" },
                            Examples = new Dictionary<string, OpenApiExampleMetadata>
                            {
                                ["one"] = new OpenApiExampleMetadata { Summary = "s", Value = new List<object> { 1, 2.5, "x", true } }
                            }
                        }
                    }
                };
                metadata.Responses = new Dictionary<string, OpenApiResponseMetadata> { ["201"] = OpenApiResponseMetadata.Created(OpenApiSchemaMetadata.CreateRef("User")) };
            });

            server.Delete("/files/{*path}", async (ApiRequest req) => null);
            server.Routes.PreAuthentication.Content.Add("/static", true);
            return server;
        }

        private static OpenApiSettings CreateOpenApiSettings(OpenApiVersionEnum version)
        {
            OpenApiSettings settings = new OpenApiSettings();
            settings.Version = version;
            settings.Info.Title = "AOT <API> & 'docs'";
            settings.Info.Version = "1.2.3";
            settings.Info.Description = "Unicode: héllo — ✓";
            if (version != OpenApiVersionEnum.V3_0) settings.Info.Summary = "summary";
            settings.Info.Contact = new OpenApiContact { Name = "c", Email = "e@example.com", Url = "https://example.com" };
            settings.Info.License = version == OpenApiVersionEnum.V3_0
                ? new OpenApiLicense { Name = "MIT", Url = "https://opensource.org/licenses/MIT" }
                : new OpenApiLicense { Name = "MIT", Identifier = "MIT" };
            settings.Servers.Add(new OpenApiServer
            {
                Url = "https://{env}.example.com",
                Description = "server",
                Variables = new Dictionary<string, OpenApiServerVariableMetadata>
                {
                    ["env"] = new OpenApiServerVariableMetadata { Default = "prod", Enum = new List<string> { "prod", "dev" } }
                }
            });
            settings.Tags.Add(new OpenApiTag { Name = "Users", Description = "users" });
            settings.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme { Type = "apiKey", Name = "X-API-Key", In = "header" };
            settings.SecuritySchemes["OAuth"] = new OpenApiSecurityScheme
            {
                Type = "oauth2",
                Flows = new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow { TokenUrl = "https://example.com/token", Scopes = new Dictionary<string, string> { ["read"] = "Read" } }
                }
            };
            settings.Security.Add(new Dictionary<string, List<string>> { ["ApiKey"] = new List<string>() });
            settings.Schemas["User"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["id"] = OpenApiSchemaMetadata.Integer(),
                    ["name"] = new OpenApiSchemaMetadata { Type = "string", MinLength = 1, MaxLength = 50, Pattern = "^[a-z]+$", Example = "bob" },
                    ["score"] = new OpenApiSchemaMetadata { Type = "number", Minimum = 0, Maximum = 100.5, ExclusiveMaximum = 101, Default = 50, Nullable = true },
                    ["role"] = new OpenApiSchemaMetadata { Type = "string", Enum = new List<object> { "admin", "user", 3, true, 2.5m } },
                    ["meta"] = new OpenApiSchemaMetadata { Type = "object", Example = new Dictionary<string, object> { ["k"] = "v", ["n"] = 1L, ["list"] = new List<object> { 1, "a" } } },
                    ["blob"] = new OpenApiSchemaMetadata { Type = "string", Format = "binary" }
                },
                Required = new List<string> { "id" }
            };
            return settings;
        }

        private static JsonSerializerOptions CreateOpenApiOptions(IJsonTypeInfoResolver resolver)
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.WriteIndented = true;
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.TypeInfoResolver = resolver;
            return options;
        }

        private static Task NoOpAsync(HttpContextBase ctx)
        {
            return Task.CompletedTask;
        }

        private static SharedNamedTestCase CreateSync(string name, Action action)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SharedNamedTestCase(name, delegate
            {
                action();
                return Task.CompletedTask;
            });
        }

        private static SharedNamedTestCase CreateAsync(string name, Func<Task> func)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return new SharedNamedTestCase(name, func);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
            }
        }

        private static void AssertContains(string haystack, string needle, string message)
        {
            if (haystack == null || !haystack.Contains(needle))
            {
                throw new InvalidOperationException(message + " Expected to find: " + needle + " In: " + haystack);
            }
        }

        #endregion
    }
}
