# OpenAPI `oneOf` Implementation Plan

This plan covers the Watson Webserver enhancement needed to emit true OpenAPI
`oneOf` schemas, including a discriminator and a component schema registry.

Progress convention:

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked or needs decision

## Current Findings

### Watson Webserver 7

Repository examined: `C:\Code\dotnet\watsonwebserver-7.0`.

Relevant files:

- `src/WatsonWebserver/Core/OpenApi/OpenApiSchemaMetadata.cs`
- `src/WatsonWebserver/Core/OpenApi/OpenApiDocumentGenerator.cs`
- `src/WatsonWebserver/Core/OpenApi/OpenApiSettings.cs`
- `src/Test.OpenApi/Program.cs`
- `src/Test.Shared/LegacyCoverageSuite.cs`

Findings:

- `OpenApiSchemaMetadata` currently supports primitive schema fields, arrays,
  object properties, required lists, `$ref`, examples, defaults, enums, numeric
  bounds, string lengths, and patterns.
- `OpenApiSchemaMetadata` does not model `oneOf`, `anyOf`, `allOf`, `not`, or
  `discriminator`.
- `OpenApiSchemaMetadata.CreateRef("User")` can create a component reference,
  but `OpenApiSettings` has no `Schemas` or `ComponentSchemas` dictionary, and
  `OpenApiDocumentGenerator.BuildComponents()` currently emits only
  `securitySchemes`.
- `OpenApiDocumentGenerator.BuildSchema()` manually converts
  `OpenApiSchemaMetadata` into `Dictionary<string, object>`. Any new schema
  keyword must be added there; adding properties to the metadata class alone is
  not enough.
- `BuildSchema()` returns immediately when `Ref` is set. Keep this behavior for
  OpenAPI 3.0 compatibility because `$ref` siblings are poorly supported by
  tooling.

## Desired OpenAPI Shape

A representative target schema using `oneOf` plus a discriminator looks like:

```yaml
animal:
  oneOf:
    - $ref: '#/components/schemas/Cat'
    - $ref: '#/components/schemas/Dog'
  discriminator:
    propertyName: kind
    mapping:
      cat: '#/components/schemas/Cat'
      dog: '#/components/schemas/Dog'
```

Each branch schema should include the discriminator property with a
single-value `enum`. OpenAPI 3.0 does not have JSON Schema `const`, so a
single-value `enum` is the compatible way to pin the discriminator value.

## Phase 1 - Watson Schema Metadata

Owner:
Started:
Completed:
Notes:

- [ ] Add `OpenApiDiscriminatorMetadata` in
  `src/WatsonWebserver/Core/OpenApi/OpenApiDiscriminatorMetadata.cs`.
  - [ ] `PropertyName`: `string`, JSON property name `propertyName`.
  - [ ] `Mapping`: `Dictionary<string, string>`, JSON property name `mapping`,
    ignored when null.
  - [ ] Add XML docs matching existing Watson style.
- [ ] Extend `OpenApiSchemaMetadata`.
  - [ ] Add `List<OpenApiSchemaMetadata> OneOf`, JSON property name `oneOf`,
    ignored when null.
  - [ ] Add `OpenApiDiscriminatorMetadata Discriminator`, JSON property name
    `discriminator`, ignored when null.
  - [ ] Add factory `CreateOneOf(params OpenApiSchemaMetadata[] schemas)`.
  - [ ] Add fluent helper `WithDiscriminator(string propertyName,
    Dictionary<string, string> mapping = null)`.
  - [ ] Add fluent helper `WithDescription(string description)` only if useful
    for route metadata ergonomics; otherwise keep the existing property style.
- [ ] Optional but recommended: add `AdditionalProperties`.
  - [ ] Decide representation: `object AdditionalProperties`, accepting
    `bool` or `OpenApiSchemaMetadata`.
  - [ ] Emit OpenAPI `additionalProperties` in `BuildSchema()`.
  - [ ] Use this later in Tempo branch schemas if strict branch objects are
    desired.

Acceptance criteria:

- [ ] Existing code using `OpenApiSchemaMetadata.String()`, `.Integer()`,
  `.CreateArray()`, and `.CreateRef()` compiles without change.
- [ ] A schema can be built with `CreateOneOf(CreateRef("A"), CreateRef("B"))`.
- [ ] A schema can attach a discriminator with property name and mapping.

## Phase 2 - Watson Components Schemas

Owner:
Started:
Completed:
Notes:

`oneOf` can be emitted with inline schemas, but practical client generators work
better when each branch is a named component schema. Watson already has
`CreateRef()`, so the missing piece is a component schema registry.

- [ ] Extend `OpenApiSettings`.
  - [ ] Add `Dictionary<string, OpenApiSchemaMetadata> Schemas`.
  - [ ] Initialize it to an empty dictionary.
  - [ ] XML doc: "Reusable component schemas emitted under
    `components.schemas`."
- [ ] Extend `OpenApiDocumentGenerator.BuildComponents()`.
  - [ ] If `settings.Schemas` has entries, add `components["schemas"]`.
  - [ ] For each schema, emit through `BuildSchema()` rather than relying on raw
    serializer output.
  - [ ] Preserve existing `securitySchemes` behavior.
- [ ] Add convenience methods only if they match Watson style.
  - [ ] `OpenApiSettings.WithSchema(string name, OpenApiSchemaMetadata schema)`
    is optional; direct dictionary assignment is enough for first merge.

Acceptance criteria:

- [ ] `CreateRef("User")` can point to a real
  `components.schemas.User` entry.
- [ ] Existing documents without component schemas are unchanged except for
  stable formatting.
- [ ] Documents with both `securitySchemes` and `schemas` include both sections.

## Phase 3 - Watson Document Generation

Owner:
Started:
Completed:
Notes:

- [ ] Update `OpenApiDocumentGenerator.BuildSchema()`.
  - [ ] Keep the early return for `$ref`.
  - [ ] Emit existing scalar fields as today.
  - [ ] Emit `oneOf` when `schema.OneOf` has entries:
    `schemaObj["oneOf"] = schema.OneOf.Select(BuildSchema).ToList()`.
  - [ ] Emit `discriminator` when set:
    `propertyName` is required, `mapping` is optional.
  - [ ] If `AdditionalProperties` is added, emit bool values directly and
    schema values through `BuildSchema()`.
  - [ ] Skip null branch schemas defensively or reject them with
    `ArgumentException`; choose one behavior and test it.
- [ ] Decide validation level.
  - [ ] Minimum: generator emits what metadata describes.
  - [ ] Stronger: `CreateOneOf()` throws if no branch schemas are supplied.
  - [ ] Stronger: discriminator helper throws if `propertyName` is empty.

Acceptance criteria:

- [ ] Generated JSON contains `oneOf` as an array of schema objects.
- [ ] Generated JSON contains `discriminator.propertyName`.
- [ ] Generated JSON contains `discriminator.mapping` when provided.
- [ ] `$ref` branch schemas serialize as `{ "$ref": "..." }`.

## Phase 4 - Watson Tests

Owner:
Started:
Completed:
Notes:

- [ ] Add focused generator coverage.
  - [ ] Preferred location: a new shared OpenAPI schema composition test helper
    used by automated/xUnit coverage.
  - [ ] Minimum acceptable location: `Test.OpenApi` plus assertions in the
    existing OpenAPI shared coverage.
- [ ] Test component schema registration.
  - [ ] Configure `OpenApiSettings.Schemas["Cat"]` and `["Dog"]`.
  - [ ] Generate document.
  - [ ] Parse with `JsonDocument`.
  - [ ] Assert `components.schemas.Cat` and `components.schemas.Dog` exist.
- [ ] Test `oneOf`.
  - [ ] Add a route request body or response body with
    `OpenApiSchemaMetadata.CreateOneOf(OpenApiSchemaMetadata.CreateRef("Cat"),
    OpenApiSchemaMetadata.CreateRef("Dog"))`.
  - [ ] Assert the generated schema contains `oneOf` length `2`.
  - [ ] Assert branch `0` is `$ref: "#/components/schemas/Cat"`.
  - [ ] Assert branch `1` is `$ref: "#/components/schemas/Dog"`.
- [ ] Test discriminator.
  - [ ] Attach discriminator property `kind`.
  - [ ] Add mapping `cat -> #/components/schemas/Cat`, `dog ->
    #/components/schemas/Dog`.
  - [ ] Assert `discriminator.propertyName == "kind"`.
  - [ ] Assert mappings are present and exact.
- [ ] Regression test existing OpenAPI output.
  - [ ] Existing `/openapi.json` still returns valid JSON.
  - [ ] Existing `/swagger` still returns HTML.
  - [ ] Existing simple schemas still emit `type`, `format`, `properties`,
    `required`, `enum`, `minimum`, and `maximum`.

Suggested validation commands from `C:\Code\dotnet\watsonwebserver-7.0`:

```powershell
dotnet build .\src\WatsonWebserver.sln
dotnet run --project .\src\Test.Automated\Test.Automated.csproj -f net8.0
dotnet test .\src\Test.XUnit\Test.XUnit.csproj -f net8.0
```

Also run `net10.0` targets where available:

```powershell
dotnet run --project .\src\Test.Automated\Test.Automated.csproj -f net10.0
dotnet test .\src\Test.XUnit\Test.XUnit.csproj -f net10.0
```

## Phase 5 - Watson Documentation

Owner:
Started:
Completed:
Notes:

- [ ] Update README OpenAPI section.
  - [ ] Show `openApi.Schemas["Cat"] = ...`.
  - [ ] Show `CreateOneOf(CreateRef("Cat"), CreateRef("Dog"))`.
  - [ ] Show discriminator mapping.
- [ ] Update XML docs generated by the project.
- [ ] Add a changelog entry for OpenAPI schema composition support.

Acceptance criteria:

- [ ] A developer can discover component schemas and `oneOf` from README.
- [ ] Public APIs have XML comments.

## Phase 6 - Rollout Notes

Owner:
Started:
Completed:
Notes:

- [ ] Confirm Swagger UI renders the `oneOf` branches correctly.
- [ ] Confirm generated clients handle a discriminator mapping as expected.
- [ ] If a target client generator ignores discriminator mapping, document the
  generator limitation and keep server-side validation as the source of truth.

## Risks And Decisions

- [ ] Decide whether Watson should add only `oneOf` or a broader composition set
  (`oneOf`, `anyOf`, `allOf`, `not`). Initial scope is `oneOf` only.
- [ ] Decide whether `OpenApiSettings.Schemas` should be named `Schemas`,
  `ComponentSchemas`, or nested under a future `Components` metadata object.
  `Schemas` is the smallest change; `Components` is more extensible.
- [ ] Decide whether Watson should validate discriminator mappings point to
  registered schemas. The simple implementation should not, because external
  refs may be valid.

## Completion Definition

- [ ] Watson can emit `components.schemas`.
- [ ] Watson can emit schema `oneOf`.
- [ ] Watson can emit `discriminator.propertyName` and `mapping`.
- [ ] Watson tests cover the generated JSON shape.
- [ ] Swagger UI and `/openapi.json` remain usable.
