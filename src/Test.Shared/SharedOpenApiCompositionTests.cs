namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.Routing;

    /// <summary>
    /// Shared OpenAPI schema-composition tests covering the
    /// <c>oneOf</c>, <c>discriminator</c>, and <c>components.schemas</c>
    /// emission paths in <see cref="OpenApiDocumentGenerator"/>.
    /// </summary>
    public static class SharedOpenApiCompositionTests
    {
        /// <summary>
        /// Get the shared OpenAPI composition test cases.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("OpenApiSchemaMetadata :: CreateOneOf populates branches", TestCreateOneOfPopulatesBranches));
            tests.Add(CreateSync("OpenApiSchemaMetadata :: CreateOneOf rejects empty input", TestCreateOneOfRejectsEmptyInput));
            tests.Add(CreateSync("OpenApiSchemaMetadata :: CreateOneOf rejects null branch", TestCreateOneOfRejectsNullBranch));
            tests.Add(CreateSync("OpenApiSchemaMetadata :: WithDiscriminator sets property and mapping", TestWithDiscriminatorSetsPropertyAndMapping));
            tests.Add(CreateSync("OpenApiSchemaMetadata :: WithDiscriminator rejects empty property name", TestWithDiscriminatorRejectsEmptyPropertyName));
            tests.Add(CreateSync("OpenApiDiscriminatorMetadata :: PropertyName setter rejects null", TestDiscriminatorPropertyNameRejectsNull));

            tests.Add(CreateSync("OpenApiDocumentGenerator :: components.schemas emitted from settings", TestComponentSchemasEmitted));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: components.schemas omitted when empty", TestComponentSchemasOmittedWhenEmpty));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: components.schemas coexist with securitySchemes", TestComponentSchemasCoexistWithSecuritySchemes));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: oneOf emitted with $ref branches", TestOneOfEmittedWithRefBranches));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: discriminator emitted with mapping", TestDiscriminatorEmittedWithMapping));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: discriminator without mapping omits mapping key", TestDiscriminatorWithoutMappingOmitsMappingKey));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: existing scalar fields still emit", TestExistingScalarFieldsStillEmit));
            tests.Add(CreateSync("OpenApiDocumentGenerator :: $ref short-circuit preserved alongside oneOf metadata", TestRefShortCircuitPreserved));

            // Version selection and 3.1 / 3.2 emission (positive).
            tests.Add(CreateSync("OpenApi 3.0 :: default version emits 3.0.3 and nullable:true", TestV30DefaultNullableRegression));
            tests.Add(CreateSync("OpenApi 3.1 :: nullable becomes a type array", TestV31NullableTypeArray));
            tests.Add(CreateSync("OpenApi 3.1 :: nullable $ref becomes anyOf with null", TestV31NullableRefAnyOf));
            tests.Add(CreateSync("OpenApi 3.1 :: schema example becomes examples array", TestV31SchemaExamplesArray));
            tests.Add(CreateSync("OpenApi 3.1 :: binary uses contentMediaType", TestV31BinaryContentMediaType));
            tests.Add(CreateSync("OpenApi 3.1 :: info.summary and license.identifier emitted", TestV31InfoSummaryAndLicenseIdentifier));
            tests.Add(CreateSync("OpenApi 3.1 :: webhooks emitted and paths optional", TestV31WebhooksAndOptionalPaths));
            tests.Add(CreateSync("OpenApi 3.1 :: oauth2, openIdConnect, and mutualTLS schemes emitted", TestV31SecuritySchemesEmitted));
            tests.Add(CreateSync("OpenApi 3.2 :: version, self, tags, servers, query, itemSchema", TestV32SurfaceEmitted));

            // Version selection and validation (negative).
            tests.Add(CreateSync("OpenApi 3.1 :: license url + identifier rejected", TestV31LicenseUrlAndIdentifierRejected));
            tests.Add(CreateSync("OpenApi 3.0 :: webhooks rejected", TestV30WebhooksRejected));
            tests.Add(CreateSync("OpenApi 3.1 :: additional operations rejected", TestV31AdditionalOperationsRejected));
            tests.Add(CreateSync("OpenApi 3.1 :: nullable without base type rejected", TestV31NullableWithoutBaseTypeRejected));
            tests.Add(CreateSync("OpenApi :: oauth2 scheme without flows rejected", TestOAuth2WithoutFlowsRejected));
            tests.Add(CreateSync("OpenApi :: duplicate operationId rejected", TestDuplicateOperationIdRejected));
            tests.Add(CreateSync("OpenApi :: $ref wins over sibling type under 3.0", TestRefWinsOverSiblingType));

            return tests.ToArray();
        }

        #region Helpers

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

        private static JsonDocument GenerateDocument(WebserverRoutes routes, OpenApiSettings settings)
        {
            OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();
            string json = generator.Generate(routes, settings);
            AssertTrue(!String.IsNullOrEmpty(json), "Generator produced empty JSON.");
            return JsonDocument.Parse(json);
        }

        private static OpenApiSettings BuildSettings()
        {
            return new OpenApiSettings("Composition Tests", "1.0.0");
        }

        private static WebserverRoutes BuildRoutes()
        {
            return new WebserverRoutes();
        }

        private static OpenApiSchemaMetadata BuildAnimalBranch(string discriminatorValue, string namedField)
        {
            OpenApiSchemaMetadata schema = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["kind"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Enum = new List<object> { discriminatorValue }
                    },
                    [namedField] = OpenApiSchemaMetadata.String()
                },
                Required = new List<string> { "kind", namedField }
            };
            return schema;
        }

        private static void RegisterRouteWithSchema(WebserverRoutes routes, string path, OpenApiSchemaMetadata responseSchema)
        {
            OpenApiRouteMetadata metadata = OpenApiRouteMetadata
                .Create("Returns an animal", "animals")
                .WithResponse(200, OpenApiResponseMetadata.Json("OK", responseSchema));

            routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                path,
                delegate (HttpContextBase ctx) { return Task.CompletedTask; },
                openApiMetadata: metadata);
        }

        #endregion

        #region Metadata Tests

        private static void TestCreateOneOfPopulatesBranches()
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.CreateOneOf(
                OpenApiSchemaMetadata.CreateRef("Cat"),
                OpenApiSchemaMetadata.CreateRef("Dog"));

            AssertTrue(schema.OneOf != null, "OneOf should be populated.");
            AssertEquals(2, schema.OneOf.Count, "OneOf should contain two branches.");
            AssertEquals("#/components/schemas/Cat", schema.OneOf[0].Ref, "First branch should reference Cat.");
            AssertEquals("#/components/schemas/Dog", schema.OneOf[1].Ref, "Second branch should reference Dog.");
        }

        private static void TestCreateOneOfRejectsEmptyInput()
        {
            try
            {
                OpenApiSchemaMetadata.CreateOneOf();
                throw new InvalidOperationException("CreateOneOf should reject empty input.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static void TestCreateOneOfRejectsNullBranch()
        {
            try
            {
                OpenApiSchemaMetadata.CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"), null);
                throw new InvalidOperationException("CreateOneOf should reject a null branch.");
            }
            catch (ArgumentException)
            {
            }
        }

        private static void TestWithDiscriminatorSetsPropertyAndMapping()
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>
            {
                ["cat"] = "#/components/schemas/Cat",
                ["dog"] = "#/components/schemas/Dog"
            };

            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata
                .CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"), OpenApiSchemaMetadata.CreateRef("Dog"))
                .WithDiscriminator("kind", mapping);

            AssertTrue(schema.Discriminator != null, "Discriminator should be populated.");
            AssertEquals("kind", schema.Discriminator.PropertyName, "PropertyName should be set.");
            AssertTrue(schema.Discriminator.Mapping != null, "Mapping should be populated.");
            AssertEquals(2, schema.Discriminator.Mapping.Count, "Mapping should contain two entries.");
            AssertEquals("#/components/schemas/Cat", schema.Discriminator.Mapping["cat"], "Mapping for cat should be Cat ref.");
            AssertEquals("#/components/schemas/Dog", schema.Discriminator.Mapping["dog"], "Mapping for dog should be Dog ref.");
        }

        private static void TestWithDiscriminatorRejectsEmptyPropertyName()
        {
            OpenApiSchemaMetadata schema = OpenApiSchemaMetadata.CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"));

            try
            {
                schema.WithDiscriminator(String.Empty);
                throw new InvalidOperationException("WithDiscriminator should reject an empty propertyName.");
            }
            catch (ArgumentNullException)
            {
            }
        }

        private static void TestDiscriminatorPropertyNameRejectsNull()
        {
            OpenApiDiscriminatorMetadata discriminator = new OpenApiDiscriminatorMetadata("kind");

            try
            {
                discriminator.PropertyName = null;
                throw new InvalidOperationException("Setting PropertyName to null should throw.");
            }
            catch (ArgumentNullException)
            {
            }
        }

        #endregion

        #region Generator Tests

        private static void TestComponentSchemasEmitted()
        {
            OpenApiSettings settings = BuildSettings();
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");
            settings.Schemas["Dog"] = BuildAnimalBranch("dog", "breed");

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                JsonElement root = doc.RootElement;
                AssertTrue(root.TryGetProperty("components", out JsonElement components), "components should exist.");
                AssertTrue(components.TryGetProperty("schemas", out JsonElement schemas), "components.schemas should exist.");
                AssertTrue(schemas.TryGetProperty("Cat", out JsonElement cat), "components.schemas.Cat should exist.");
                AssertTrue(schemas.TryGetProperty("Dog", out JsonElement dog), "components.schemas.Dog should exist.");
                AssertEquals("object", cat.GetProperty("type").GetString(), "Cat type should be object.");
                AssertEquals("object", dog.GetProperty("type").GetString(), "Dog type should be object.");
            }
        }

        private static void TestComponentSchemasOmittedWhenEmpty()
        {
            OpenApiSettings settings = BuildSettings();

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("components", out JsonElement components))
                {
                    AssertTrue(!components.TryGetProperty("schemas", out _), "components.schemas should not be emitted when empty.");
                }
            }
        }

        private static void TestComponentSchemasCoexistWithSecuritySchemes()
        {
            OpenApiSettings settings = BuildSettings();
            settings.SecuritySchemes["bearerAuth"] = new OpenApiSecurityScheme
            {
                Type = "http",
                Scheme = "bearer"
            };
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                JsonElement components = doc.RootElement.GetProperty("components");
                AssertTrue(components.TryGetProperty("securitySchemes", out _), "securitySchemes should still be emitted.");
                AssertTrue(components.TryGetProperty("schemas", out JsonElement schemas), "schemas should be emitted alongside securitySchemes.");
                AssertTrue(schemas.TryGetProperty("Cat", out _), "Cat schema should be present.");
            }
        }

        private static void TestOneOfEmittedWithRefBranches()
        {
            OpenApiSettings settings = BuildSettings();
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");
            settings.Schemas["Dog"] = BuildAnimalBranch("dog", "breed");

            WebserverRoutes routes = BuildRoutes();
            OpenApiSchemaMetadata animalSchema = OpenApiSchemaMetadata.CreateOneOf(
                OpenApiSchemaMetadata.CreateRef("Cat"),
                OpenApiSchemaMetadata.CreateRef("Dog"));

            RegisterRouteWithSchema(routes, "/animals", animalSchema);

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = doc.RootElement
                    .GetProperty("paths")
                    .GetProperty("/animals")
                    .GetProperty("get")
                    .GetProperty("responses")
                    .GetProperty("200")
                    .GetProperty("content")
                    .GetProperty("application/json")
                    .GetProperty("schema");

                AssertTrue(schema.TryGetProperty("oneOf", out JsonElement oneOf), "Schema should contain oneOf.");
                AssertEquals(JsonValueKind.Array, oneOf.ValueKind, "oneOf should be an array.");
                AssertEquals(2, oneOf.GetArrayLength(), "oneOf should contain two branches.");
                AssertEquals("#/components/schemas/Cat", oneOf[0].GetProperty("$ref").GetString(), "Branch 0 should reference Cat.");
                AssertEquals("#/components/schemas/Dog", oneOf[1].GetProperty("$ref").GetString(), "Branch 1 should reference Dog.");
            }
        }

        private static void TestDiscriminatorEmittedWithMapping()
        {
            OpenApiSettings settings = BuildSettings();
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");
            settings.Schemas["Dog"] = BuildAnimalBranch("dog", "breed");

            Dictionary<string, string> mapping = new Dictionary<string, string>
            {
                ["cat"] = "#/components/schemas/Cat",
                ["dog"] = "#/components/schemas/Dog"
            };

            OpenApiSchemaMetadata animalSchema = OpenApiSchemaMetadata
                .CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"), OpenApiSchemaMetadata.CreateRef("Dog"))
                .WithDiscriminator("kind", mapping);

            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/animals", animalSchema);

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = doc.RootElement
                    .GetProperty("paths")
                    .GetProperty("/animals")
                    .GetProperty("get")
                    .GetProperty("responses")
                    .GetProperty("200")
                    .GetProperty("content")
                    .GetProperty("application/json")
                    .GetProperty("schema");

                AssertTrue(schema.TryGetProperty("discriminator", out JsonElement discriminator), "Schema should contain discriminator.");
                AssertEquals("kind", discriminator.GetProperty("propertyName").GetString(), "Discriminator propertyName should be kind.");
                AssertTrue(discriminator.TryGetProperty("mapping", out JsonElement mappingElement), "Discriminator should contain mapping.");
                AssertEquals("#/components/schemas/Cat", mappingElement.GetProperty("cat").GetString(), "cat mapping should reference Cat.");
                AssertEquals("#/components/schemas/Dog", mappingElement.GetProperty("dog").GetString(), "dog mapping should reference Dog.");
            }
        }

        private static void TestDiscriminatorWithoutMappingOmitsMappingKey()
        {
            OpenApiSettings settings = BuildSettings();
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");

            OpenApiSchemaMetadata animalSchema = OpenApiSchemaMetadata
                .CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"))
                .WithDiscriminator("kind");

            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/animals", animalSchema);

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = doc.RootElement
                    .GetProperty("paths")
                    .GetProperty("/animals")
                    .GetProperty("get")
                    .GetProperty("responses")
                    .GetProperty("200")
                    .GetProperty("content")
                    .GetProperty("application/json")
                    .GetProperty("schema");

                JsonElement discriminator = schema.GetProperty("discriminator");
                AssertEquals("kind", discriminator.GetProperty("propertyName").GetString(), "Discriminator propertyName should be kind.");
                AssertTrue(!discriminator.TryGetProperty("mapping", out _), "mapping should be omitted when not provided.");
            }
        }

        private static void TestExistingScalarFieldsStillEmit()
        {
            OpenApiSettings settings = BuildSettings();
            OpenApiSchemaMetadata schema = new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["count"] = new OpenApiSchemaMetadata
                    {
                        Type = "integer",
                        Format = "int32",
                        Minimum = 1.0,
                        Maximum = 10.0
                    },
                    ["status"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Enum = new List<object> { "open", "closed" }
                    }
                },
                Required = new List<string> { "count" }
            };

            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/items", schema);

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement emitted = doc.RootElement
                    .GetProperty("paths")
                    .GetProperty("/items")
                    .GetProperty("get")
                    .GetProperty("responses")
                    .GetProperty("200")
                    .GetProperty("content")
                    .GetProperty("application/json")
                    .GetProperty("schema");

                AssertEquals("object", emitted.GetProperty("type").GetString(), "Schema type should be object.");
                JsonElement properties = emitted.GetProperty("properties");
                JsonElement count = properties.GetProperty("count");
                AssertEquals("integer", count.GetProperty("type").GetString(), "Count type should be integer.");
                AssertEquals("int32", count.GetProperty("format").GetString(), "Count format should be int32.");
                AssertEquals(1.0, count.GetProperty("minimum").GetDouble(), "Minimum should be 1.");
                AssertEquals(10.0, count.GetProperty("maximum").GetDouble(), "Maximum should be 10.");

                JsonElement status = properties.GetProperty("status");
                JsonElement statusEnum = status.GetProperty("enum");
                AssertEquals(2, statusEnum.GetArrayLength(), "Enum should have two entries.");

                JsonElement required = emitted.GetProperty("required");
                AssertEquals(1, required.GetArrayLength(), "Required should have one entry.");
                AssertEquals("count", required[0].GetString(), "Required entry should be count.");
            }
        }

        private static void TestRefShortCircuitPreserved()
        {
            OpenApiSettings settings = BuildSettings();
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");

            OpenApiSchemaMetadata responseSchema = new OpenApiSchemaMetadata
            {
                Ref = "#/components/schemas/Cat",
                OneOf = new List<OpenApiSchemaMetadata>
                {
                    OpenApiSchemaMetadata.CreateRef("Dog")
                }
            };

            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/cat", responseSchema);

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = doc.RootElement
                    .GetProperty("paths")
                    .GetProperty("/cat")
                    .GetProperty("get")
                    .GetProperty("responses")
                    .GetProperty("200")
                    .GetProperty("content")
                    .GetProperty("application/json")
                    .GetProperty("schema");

                AssertEquals("#/components/schemas/Cat", schema.GetProperty("$ref").GetString(), "Schema should serialize as $ref only.");
                AssertTrue(!schema.TryGetProperty("oneOf", out _), "oneOf should not appear alongside $ref.");
            }
        }

        #endregion

        #region Version Tests

        private static void TestV30DefaultNullableRegression()
        {
            OpenApiSettings settings = new OpenApiSettings("Composition Tests", "1.0.0");
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/x", new OpenApiSchemaMetadata { Type = "string", Nullable = true });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                AssertEquals("3.0.3", doc.RootElement.GetProperty("openapi").GetString(), "Default version should be 3.0.3.");
                JsonElement schema = SchemaOf(doc, "/x");
                AssertTrue(schema.TryGetProperty("nullable", out JsonElement nullable), "nullable should be present under 3.0.");
                AssertEquals(true, nullable.GetBoolean(), "nullable should be true under 3.0.");
                AssertEquals("string", schema.GetProperty("type").GetString(), "type should remain a scalar string under 3.0.");
            }
        }

        private static void TestV31NullableTypeArray()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/x", new OpenApiSchemaMetadata { Type = "string", Nullable = true });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                AssertEquals("3.1.1", doc.RootElement.GetProperty("openapi").GetString(), "Version should be 3.1.1.");
                JsonElement schema = SchemaOf(doc, "/x");
                JsonElement type = schema.GetProperty("type");
                AssertEquals(JsonValueKind.Array, type.ValueKind, "type should be an array under 3.1.");
                AssertEquals(2, type.GetArrayLength(), "type array should have two entries.");
                AssertTrue(ArrayContainsString(type, "string"), "type array should contain 'string'.");
                AssertTrue(ArrayContainsString(type, "null"), "type array should contain 'null'.");
                AssertTrue(!schema.TryGetProperty("nullable", out _), "nullable must not appear under 3.1.");
            }
        }

        private static void TestV31NullableRefAnyOf()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/cat", new OpenApiSchemaMetadata { Ref = "#/components/schemas/Cat", Nullable = true });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = SchemaOf(doc, "/cat");
                AssertTrue(!schema.TryGetProperty("$ref", out _), "Nullable $ref should not emit a bare $ref under 3.1.");
                JsonElement anyOf = schema.GetProperty("anyOf");
                AssertEquals(2, anyOf.GetArrayLength(), "anyOf should contain two branches.");
                AssertEquals("#/components/schemas/Cat", anyOf[0].GetProperty("$ref").GetString(), "Branch 0 should reference Cat.");
                AssertEquals("null", anyOf[1].GetProperty("type").GetString(), "Branch 1 should be the null type.");
            }
        }

        private static void TestV31SchemaExamplesArray()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/x", new OpenApiSchemaMetadata { Type = "string", Example = "hello" });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = SchemaOf(doc, "/x");
                AssertTrue(!schema.TryGetProperty("example", out _), "Singular 'example' should not appear under 3.1.");
                JsonElement examples = schema.GetProperty("examples");
                AssertEquals(JsonValueKind.Array, examples.ValueKind, "examples should be an array.");
                AssertEquals("hello", examples[0].GetString(), "examples[0] should be the example value.");
            }
        }

        private static void TestV31BinaryContentMediaType()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/x", new OpenApiSchemaMetadata { Type = "string", Format = "binary" });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = SchemaOf(doc, "/x");
                AssertTrue(!schema.TryGetProperty("format", out _), "format:binary should not appear under 3.1.");
                AssertEquals("application/octet-stream", schema.GetProperty("contentMediaType").GetString(), "contentMediaType should be emitted under 3.1.");
            }
        }

        private static void TestV31InfoSummaryAndLicenseIdentifier()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            settings.Info.Summary = "Short summary";
            settings.Info.License = new OpenApiLicense { Name = "MIT", Identifier = "MIT" };

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                JsonElement info = doc.RootElement.GetProperty("info");
                AssertEquals("Short summary", info.GetProperty("summary").GetString(), "info.summary should be emitted under 3.1.");
                JsonElement license = info.GetProperty("license");
                AssertEquals("MIT", license.GetProperty("identifier").GetString(), "license.identifier should be emitted under 3.1.");
                AssertTrue(!license.TryGetProperty("url", out _), "license.url should be absent when identifier is set.");
            }
        }

        private static void TestV31WebhooksAndOptionalPaths()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            OpenApiRouteMetadata op = OpenApiRouteMetadata.Create("New pet notification")
                .WithResponse(200, OpenApiResponseMetadata.Create("Acknowledged"));
            settings.Webhooks["newPet"] = new OpenApiWebhookMetadata("post", op);

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                AssertTrue(!doc.RootElement.TryGetProperty("paths", out _), "paths should be omitted when empty and webhooks are present under 3.1.");
                JsonElement webhooks = doc.RootElement.GetProperty("webhooks");
                AssertTrue(webhooks.TryGetProperty("newPet", out JsonElement newPet), "webhooks.newPet should exist.");
                AssertTrue(newPet.TryGetProperty("post", out _), "webhooks.newPet.post should exist.");
            }
        }

        private static void TestV31SecuritySchemesEmitted()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);

            OpenApiOAuthFlow authCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = "https://example.com/authorize",
                TokenUrl = "https://example.com/token"
            };
            authCode.Scopes["read"] = "Read access";
            settings.SecuritySchemes["oauth"] = new OpenApiSecurityScheme
            {
                Type = "oauth2",
                Flows = new OpenApiOAuthFlows { AuthorizationCode = authCode }
            };
            settings.SecuritySchemes["oidc"] = new OpenApiSecurityScheme
            {
                Type = "openIdConnect",
                OpenIdConnectUrl = "https://example.com/.well-known/openid-configuration"
            };
            settings.SecuritySchemes["mtls"] = new OpenApiSecurityScheme { Type = "mutualTLS" };

            using (JsonDocument doc = GenerateDocument(BuildRoutes(), settings))
            {
                JsonElement schemes = doc.RootElement.GetProperty("components").GetProperty("securitySchemes");

                JsonElement oauth = schemes.GetProperty("oauth");
                AssertEquals("oauth2", oauth.GetProperty("type").GetString(), "oauth scheme type should be oauth2.");
                JsonElement flow = oauth.GetProperty("flows").GetProperty("authorizationCode");
                AssertEquals("https://example.com/authorize", flow.GetProperty("authorizationUrl").GetString(), "authorizationUrl should be emitted.");
                AssertTrue(flow.TryGetProperty("scopes", out _), "scopes should be emitted.");

                JsonElement oidc = schemes.GetProperty("oidc");
                AssertEquals("https://example.com/.well-known/openid-configuration", oidc.GetProperty("openIdConnectUrl").GetString(), "openIdConnectUrl should be emitted.");

                JsonElement mtls = schemes.GetProperty("mtls");
                AssertEquals("mutualTLS", mtls.GetProperty("type").GetString(), "mutualTLS scheme type should be emitted.");
            }
        }

        private static void TestV32SurfaceEmitted()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_2);
            settings.Self = "https://example.com/openapi.json";
            settings.Tags.Add(new OpenApiTag { Name = "pets", Summary = "Pet operations", Parent = "root", Kind = "nav", Description = "Pets" });
            settings.Servers.Add(new OpenApiServer { Url = "https://api.example.com", Name = "prod" });

            OpenApiRouteMetadata queryOp = OpenApiRouteMetadata.Create("Search").WithResponse(200, OpenApiResponseMetadata.Create("ok"));
            OpenApiRouteMetadata purgeOp = OpenApiRouteMetadata.Create("Purge").WithResponse(200, OpenApiResponseMetadata.Create("ok"));
            Dictionary<string, OpenApiRouteMetadata> searchOps = new Dictionary<string, OpenApiRouteMetadata>
            {
                ["QUERY"] = queryOp,
                ["PURGE"] = purgeOp
            };
            settings.AdditionalOperations["/search"] = searchOps;

            OpenApiResponseMetadata streamResponse = new OpenApiResponseMetadata
            {
                Description = "Stream",
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/jsonl"] = new OpenApiMediaTypeMetadata { ItemSchema = OpenApiSchemaMetadata.String() }
                }
            };
            WebserverRoutes routes = BuildRoutes();
            routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/stream",
                delegate (HttpContextBase ctx) { return Task.CompletedTask; },
                openApiMetadata: OpenApiRouteMetadata.Create("Stream").WithResponse(200, streamResponse));

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                AssertEquals("3.2.0", doc.RootElement.GetProperty("openapi").GetString(), "Version should be 3.2.0.");
                AssertEquals("https://example.com/openapi.json", doc.RootElement.GetProperty("$self").GetString(), "$self should be emitted under 3.2.");

                JsonElement tag = doc.RootElement.GetProperty("tags")[0];
                AssertEquals("Pet operations", tag.GetProperty("summary").GetString(), "tag summary should be emitted under 3.2.");
                AssertEquals("root", tag.GetProperty("parent").GetString(), "tag parent should be emitted under 3.2.");
                AssertEquals("nav", tag.GetProperty("kind").GetString(), "tag kind should be emitted under 3.2.");

                JsonElement server = doc.RootElement.GetProperty("servers")[0];
                AssertEquals("prod", server.GetProperty("name").GetString(), "server name should be emitted under 3.2.");

                JsonElement search = doc.RootElement.GetProperty("paths").GetProperty("/search");
                AssertTrue(search.TryGetProperty("query", out _), "QUERY should be emitted as the first-class 'query' field.");
                AssertTrue(search.GetProperty("additionalOperations").TryGetProperty("PURGE", out _), "PURGE should be emitted under additionalOperations.");

                JsonElement itemSchema = doc.RootElement
                    .GetProperty("paths").GetProperty("/stream").GetProperty("get")
                    .GetProperty("responses").GetProperty("200")
                    .GetProperty("content").GetProperty("application/jsonl")
                    .GetProperty("itemSchema");
                AssertEquals("string", itemSchema.GetProperty("type").GetString(), "itemSchema should be emitted under 3.2.");
            }
        }

        private static void TestV31LicenseUrlAndIdentifierRejected()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            settings.Info.License = new OpenApiLicense { Name = "MIT", Url = "https://opensource.org/licenses/MIT", Identifier = "MIT" };
            AssertThrowsValidation(delegate { GenerateJson(BuildRoutes(), settings); }, "License url + identifier should be rejected.");
        }

        private static void TestV30WebhooksRejected()
        {
            OpenApiSettings settings = new OpenApiSettings("Composition Tests", "1.0.0");
            settings.Webhooks["newPet"] = new OpenApiWebhookMetadata("post", OpenApiRouteMetadata.Create("x"));
            AssertThrowsValidation(delegate { GenerateJson(BuildRoutes(), settings); }, "Webhooks should be rejected under 3.0.");
        }

        private static void TestV31AdditionalOperationsRejected()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            settings.AdditionalOperations["/x"] = new Dictionary<string, OpenApiRouteMetadata>
            {
                ["QUERY"] = OpenApiRouteMetadata.Create("x")
            };
            AssertThrowsValidation(delegate { GenerateJson(BuildRoutes(), settings); }, "Additional operations should be rejected under 3.1.");
        }

        private static void TestV31NullableWithoutBaseTypeRejected()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/x", new OpenApiSchemaMetadata { Nullable = true });
            AssertThrowsValidation(delegate { GenerateJson(routes, settings); }, "Nullable without a base type should be rejected under 3.1.");
        }

        private static void TestOAuth2WithoutFlowsRejected()
        {
            OpenApiSettings settings = BuildSettings(OpenApiVersionEnum.V3_1);
            settings.SecuritySchemes["oauth"] = new OpenApiSecurityScheme { Type = "oauth2" };
            AssertThrowsValidation(delegate { GenerateJson(BuildRoutes(), settings); }, "OAuth2 without flows should be rejected.");
        }

        private static void TestDuplicateOperationIdRejected()
        {
            OpenApiSettings settings = new OpenApiSettings("Composition Tests", "1.0.0");
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithMetadata(routes, "/a", new OpenApiRouteMetadata { OperationId = "dup" });
            RegisterRouteWithMetadata(routes, "/b", new OpenApiRouteMetadata { OperationId = "dup" });
            AssertThrowsValidation(delegate { GenerateJson(routes, settings); }, "Duplicate operationId should be rejected.");
        }

        private static void TestRefWinsOverSiblingType()
        {
            OpenApiSettings settings = new OpenApiSettings("Composition Tests", "1.0.0");
            settings.Schemas["Cat"] = BuildAnimalBranch("cat", "whiskers");
            WebserverRoutes routes = BuildRoutes();
            RegisterRouteWithSchema(routes, "/cat", new OpenApiSchemaMetadata { Ref = "#/components/schemas/Cat", Type = "object" });

            using (JsonDocument doc = GenerateDocument(routes, settings))
            {
                JsonElement schema = SchemaOf(doc, "/cat");
                AssertEquals("#/components/schemas/Cat", schema.GetProperty("$ref").GetString(), "$ref should win over a sibling type under 3.0.");
                AssertTrue(!schema.TryGetProperty("type", out _), "sibling type should not appear alongside $ref under 3.0.");
            }
        }

        #endregion

        #region Version-Test-Helpers

        private static OpenApiSettings BuildSettings(OpenApiVersionEnum version)
        {
            OpenApiSettings settings = new OpenApiSettings("Composition Tests", "1.0.0");
            settings.Version = version;
            return settings;
        }

        private static void RegisterRouteWithMetadata(WebserverRoutes routes, string path, OpenApiRouteMetadata metadata)
        {
            routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                path,
                delegate (HttpContextBase ctx) { return Task.CompletedTask; },
                openApiMetadata: metadata);
        }

        private static JsonElement SchemaOf(JsonDocument doc, string path)
        {
            return doc.RootElement
                .GetProperty("paths")
                .GetProperty(path)
                .GetProperty("get")
                .GetProperty("responses")
                .GetProperty("200")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema");
        }

        private static bool ArrayContainsString(JsonElement array, string value)
        {
            foreach (JsonElement element in array.EnumerateArray())
            {
                if (String.Equals(element.GetString(), value, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string GenerateJson(WebserverRoutes routes, OpenApiSettings settings)
        {
            OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();
            return generator.Generate(routes, settings);
        }

        private static void AssertThrowsValidation(Action action, string message)
        {
            try
            {
                action();
            }
            catch (OpenApiValidationException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        #endregion

        #region Assertion Helpers

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

        #endregion
    }
}
