# Watson OpenAPI

Watson generates OpenAPI documents for the routes you register, and serves Swagger UI, with no extra
package and no external dependency. As of 7.2.0 you choose which specification version it emits — 3.0,
3.1, or 3.2 — from a single setting. The default stays 3.0 so existing consumers see the exact document
they saw before; the newer versions are opt-in.

## Two lines to turn it on

```csharp
Webserver server = new Webserver(new WebserverSettings("127.0.0.1", 8080, false), DefaultRoute);
server.UseOpenApi(openApi => openApi.Info.Title = "Example API");
```

That registers `/openapi.json` and `/swagger`. Every route you document with `openApiMetadata` shows up in
the document; undocumented routes still appear with a minimal stub so the surface is complete.

## Choosing a version

```csharp
server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "Example API";
    openApi.Version = OpenApiVersionEnum.V3_1;   // V3_0 (default), V3_1, or V3_2
});
```

The version you pick changes how the document is encoded, not just the string in the `openapi` field.

| Setting | Emitted `openapi` | What changes |
|---|---|---|
| `V3_0` (default) | `3.0.3` | The original Watson encoding. Byte-compatible with pre-7.2 output. |
| `V3_1` | `3.1.1` | Schema Object follows JSON Schema 2020-12. |
| `V3_2` | `3.2.0` | Everything in 3.1, plus the 3.2 additions below. |

If you need to pin a specific patch release, set `VersionString` (for example `"3.1.0"`); it overrides the
string without changing the encoding rules, which follow `Version`.

## What moves between 3.0 and 3.1

The jump from 3.0 to 3.1 is the one that actually rewrites schemas, because 3.1 aligns the Schema Object with
JSON Schema 2020-12. If you maintain a client generated against a 3.0 document, these are the shapes that will
look different after you switch.

Nullability is the big one. Under 3.0 a nullable string is `{"type":"string","nullable":true}`. Under 3.1 the
`nullable` keyword is gone; the null is folded into the type:

```jsonc
// 3.0
{ "type": "string", "nullable": true }

// 3.1
{ "type": ["string", "null"] }
```

A nullable `$ref` cannot carry a sibling `nullable`, so 3.1 wraps it:

```jsonc
// 3.1
{ "anyOf": [ { "$ref": "#/components/schemas/Cat" }, { "type": "null" } ] }
```

Binary payloads move off `format`. What was `{"type":"string","format":"binary"}` becomes
`{"type":"string","contentMediaType":"application/octet-stream"}`, and `format: byte` becomes
`contentEncoding: base64`. Schema-level examples move from the singular `example` to an `examples` array.
Watson performs all of these rewrites for you from the same `OpenApiSchemaMetadata` you already write — you do
not maintain two schema trees.

The 3.1 document object also gains fields Watson populates when you set them: `info.summary`,
`license.identifier` (an SPDX expression, mutually exclusive with `license.url`), a top-level `webhooks` object,
`jsonSchemaDialect`, `mutualTLS` security schemes, and OAuth2 flows. When webhooks are present the `paths`
object may be omitted, which Watson does automatically.

## What 3.2 adds

3.2 is additive on top of 3.1. Watson emits the following when you target `V3_2` and set the corresponding
values:

- `$self` on the document (`OpenApiSettings.Self`)
- the `query` HTTP method as a first-class path operation, plus `additionalOperations` for any other
  non-standard method, both driven by `OpenApiSettings.AdditionalOperations`
- streaming media via `itemSchema` on a media type (`OpenApiMediaTypeMetadata.ItemSchema`), for formats like
  `application/jsonl` and `text/event-stream`
- hierarchical tags: `summary`, `parent`, and `kind` on `OpenApiTag`
- a `name` on servers
- the OAuth2 device authorization flow and `oauth2MetadataUrl`

Requesting any 3.2-only construct while `Version` is lower than `V3_2` is a hard error rather than a silently
invalid document — see the validation section.

## Putting the document behind authentication

By default `/openapi.json` and `/swagger` are registered ahead of authentication, so anyone who can reach the
server can read them. Set `RequireAuthentication` to move both endpoints behind whatever authentication you
have configured:

```csharp
server.Routes.AuthenticateApiRequest = async ctx =>
{
    string key = ctx.Request.RetrieveHeaderValue("X-API-Key");
    return key == "secret"
        ? new AuthResult { AuthenticationResult = AuthenticationResultEnum.Success, AuthorizationResult = AuthorizationResultEnum.Permitted }
        : new AuthResult { AuthenticationResult = AuthenticationResultEnum.NotFound, AuthorizationResult = AuthorizationResultEnum.DeniedImplicit };
};

server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "Example API";
    openApi.RequireAuthentication = true;   // /openapi.json and /swagger now require auth
});
```

With `RequireAuthentication = true` an unauthenticated request to `/openapi.json` gets the server's standard
authentication-failure response and never the document body. The flag only decides where the two endpoints
live; `IncludePreAuthRoutes` and `IncludePostAuthRoutes` still control which of your other routes appear inside
the document.

## Validation

Watson refuses to emit a document it knows is invalid for the selected version, throwing
`OpenApiValidationException` from `Generate`. The checks that fire:

- a license with both `url` and `identifier` set
- `identifier`, `webhooks`, `jsonSchemaDialect`, `mutualTLS`, or a `null` type in a `type` array under a
  version that does not support it
- additional operations, `$self`, server `name`, tag hierarchy, or the device flow under a version below 3.2
- an `oauth2` scheme with no flows
- a nullable schema under 3.1+ that has no base type to attach `null` to
- a duplicate `operationId` anywhere in the document

Catching these at generation time keeps a broken document from reaching a client generator, where the failure
would be far less obvious.

## Documenting a route

Route metadata is the same regardless of version; the generator adapts the output.

```csharp
server.Routes.PreAuthentication.Parameter.Add(
    HttpMethod.GET,
    "/users/{id}",
    GetUserHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Get a user", "Users")
        .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()))
        .WithResponse(200, OpenApiResponseMetadata.Json("User found", OpenApiSchemaMetadata.CreateRef("User")))
        .WithResponse(404, OpenApiResponseMetadata.NotFound()));
```

Reusable schemas live under `OpenApiSettings.Schemas` and are referenced with
`OpenApiSchemaMetadata.CreateRef("User")`, exactly as before.

## Swagger UI and offline use

The `/swagger` page loads swagger-ui-dist assets from the unpkg CDN, so it needs internet access at page load;
a browser with no route to unpkg.com renders a blank page. The pinned version is configurable through
`OpenApiSettings.SwaggerUiVersion` (default `5.17.14`) if you need to move it. Bundling the assets for fully
offline operation is not something Watson does today.

## Verifying it yourself

Point any OpenAPI validator at the generated document to confirm it is well formed under the version you chose.
From the sample project:

```
dotnet run --project src/Test.OpenApi/Test.OpenApi.csproj -- 3.1
curl http://localhost:8080/openapi.json | jq .openapi   # "3.1.1"
```

Pass `3.2` to see the newer surface, and add `secure` as a second argument to watch the document move behind an
`X-API-Key` check. The shared test suite exercises each version's encoding and every validation rule in
`src/Test.Shared/SharedOpenApiCompositionTests.cs`, and the endpoint behavior in
`src/Test.Shared/SharedOpenApiEndpointTests.cs`.
