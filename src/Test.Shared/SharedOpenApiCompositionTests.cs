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
