# CLAUDE.md

This file provides repository-specific guidance for AI coding agents working in this codebase.

## Project Overview

Watson 7 is a transport-owning HTTP server with support for HTTP/1.1, HTTP/2, and HTTP/3.

The repository ships a single `Watson` package. The `WatsonWebserver.Core` namespace within that package contains the shared abstractions, routing, OpenAPI, health checks, settings, serialization, and API-route support.

This repository no longer uses `http.sys` as its primary server model.

## Solution Layout

- `src/WatsonWebserver/` - server implementation (concrete types at root, shared core types under `Core/`)
- `src/Test.Automated/` - console-based automated integration suite
- `src/Test.XUnit/` - xUnit mirror over the shared automated coverage
- `src/Test.Benchmark/` - benchmark harness for Watson 6, WatsonLite 6, Watson 7, and Kestrel
- `src/Test.*` - sample and feature-specific projects

## Core Architecture

### Shared request pipeline

The `WatsonWebserver.Core` namespace (under `src/WatsonWebserver/Core/`) owns the common semantics:

- `WebserverBase`
- `HttpContextBase`
- `HttpRequestBase`
- `HttpResponseBase`
- route managers and routing groups
- middleware execution
- API-route helpers
- OpenAPI and health-check extensions

The concrete transport work lives under `src/WatsonWebserver/`:

- HTTP/1.1
- HTTP/2
- HTTP/3

### Routing order

Requests flow through this order:

1. `Routes.Preflight`
2. `Routes.PreRouting`
3. `Routes.PreAuthentication`
4. `Routes.AuthenticateRequest`
5. `Routes.AuthenticateApiRequest`
6. `Routes.PostAuthentication`
7. `Routes.Default`
8. `Routes.PostRouting`

Within a routing group, matching order is:

1. `Static`
2. `Content`
3. `Parameter`
4. `Dynamic`

### Middleware behavior

`WebserverBase.Middleware` wraps the matched route handler and executes in registration order.

- Middleware runs for all route types, not only API routes.
- Middleware can short-circuit by not calling `next()`.
- `PostRouting` still runs after the route pipeline completes.

## Current package and protocol model

When updating docs or examples, assume:

- `Watson` is the single package consumers install (there is no separate `Watson.Core` package as of v7.0.4)
- HTTP/1.1 is enabled by default
- HTTP/2 and HTTP/3 require explicit configuration
- Alt-Svc is explicit and off by default

Do not write new guidance that describes Watson 7 as `HttpListener` or `http.sys` based.

## API Routes

Watson 7 supports FastAPI-like API routes directly on `WebserverBase`.

### Basic usage

```csharp
WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080, false);
Webserver server = new Webserver(settings, DefaultRoute);

server.Get("/users/{id}", async (req) =>
{
    Guid id = req.Parameters.GetGuid("id");
    return new UserResponse
    {
        Id = id,
        Name = "example"
    };
});

server.Post<CreateUserRequest>("/users", async (req) =>
{
    CreateUserRequest body = req.GetData<CreateUserRequest>();
    req.Http.Response.StatusCode = 201;

    return new UserResponse
    {
        Id = Guid.NewGuid(),
        Name = body.Name
    };
});
```

### Body access rules

For API routes:

- `Post<T>`, `Put<T>`, and `Patch<T>` deserialize into `ApiRequest.Data`
- `req.GetData<T>()` is the normal typed access path
- manual access is still available through `req.Http.Request`

For low-level routes:

- `ctx.Request.DataAsBytes` fully reads and caches the body on first access
- `ctx.Request.DataAsString` fully reads and caches the body on first access
- `await ctx.Request.ReadBodyAsync(ctx.Token)` is the explicit async read path when cancellation-aware body consumption is preferred

### Error handling

Use `WebserverException` from API routes for structured JSON errors.

```csharp
server.Get("/products/{id}", async (req) =>
{
    Guid id = req.Parameters.GetGuid("id");
    throw new WebserverException(ApiResultEnum.NotFound, "Product not found: " + id);
});
```

### Serializer configuration

`WebserverBase.Serializer` controls API-route request/response serialization.

- Default: `DefaultSerializationHelper`
- Replaceable by user code
- Used by `ApiRouteHandler` and `ApiResponseProcessor`

```csharp
server.Serializer = new DefaultSerializationHelper();
```

If you change API-route serialization behavior, check both:

- `src/WatsonWebserver/Core/Routing/ApiRouteHandler.cs`
- `src/WatsonWebserver/Core/Routing/ApiResponseProcessor.cs`

### Structured authentication

Use `Routes.AuthenticateApiRequest` for structured auth.

```csharp
server.Routes.AuthenticateApiRequest = async (ctx) =>
{
    string header = ctx.Request.RetrieveHeaderValue("Authorization");
    if (header == "Bearer test-token")
    {
        return new AuthResult
        {
            AuthenticationResult = AuthenticationResultEnum.Success,
            AuthorizationResult = AuthorizationResultEnum.Permitted,
            Metadata = new AuthMetadata
            {
                UserId = 1
            }
        };
    }

    return new AuthResult
    {
        AuthenticationResult = AuthenticationResultEnum.NotFound,
        AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
    };
};
```

Route helpers default to pre-authentication. Use `auth: true` for post-authentication registration.

### Health checks

Health checks are configured with `UseHealthCheck`.

```csharp
server.UseHealthCheck(health =>
{
    health.Path = "/health";
    health.RequireAuthentication = false;
    health.CustomCheck = async (token) =>
    {
        return new HealthCheckResult
        {
            Status = HealthStatusEnum.Healthy,
            Description = "OK"
        };
    };
});
```

### Timeouts

API-route timeouts are configured through `Settings.Timeout.DefaultTimeout`.

```csharp
server.Settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(30);

server.Get("/slow", async (req) =>
{
    await Task.Delay(TimeSpan.FromMinutes(1), req.CancellationToken);
    return new SlowResponse
    {
        Completed = true
    };
});
```

When the timeout fires:

- the linked request token is cancelled
- Watson returns HTTP `408`
- API routes receive a structured timeout response

### OpenAPI

Use `UseOpenApi` plus fluent route metadata.

```csharp
server.UseOpenApi(api =>
{
    api.Info.Title = "Example API";
    api.Info.Version = "7.0.0";
});

server.Get(
    "/users/{id}",
    async (req) => new UserResponse(),
    openApi: metadata =>
    {
        metadata.Summary = "Get a user";
    });
```

## Low-level routes

Traditional `Func<HttpContextBase, Task>` routes remain first-class.

```csharp
private static async Task EchoBody(HttpContextBase ctx)
{
    byte[] body = ctx.Request.DataAsBytes;

    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "application/octet-stream";
    await ctx.Response.Send(body, ctx.Token);
}
```

Important:

- every route must send a response or throw
- do not send a response from `PostRouting`
- set chunked or SSE mode before the first send call

## Protocol notes

### HTTP/1.1

- keep-alive is supported
- chunked request and response paths exist
- request parser and response writer hot paths are performance-sensitive

### HTTP/2

- h2c prior-knowledge support exists
- flow control, GOAWAY, stream lifecycle, and HPACK coverage exist
- per-stream behavior matters more than per-connection assumptions

### HTTP/3

- depends on QUIC runtime availability
- the repo intentionally handles graceful degradation when QUIC is unavailable
- Alt-Svc integration is explicit

## Testing commands

Use these commands unless the task requires a narrower scope:

```powershell
dotnet build src\WatsonWebserver.sln -c Debug
dotnet run --project src\Test.Automated\Test.Automated.csproj -c Debug -f net10.0
dotnet test src\Test.XUnit\Test.XUnit.csproj -c Debug -f net10.0
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -c Debug -f net10.0 -- --targets Watson7 --protocols http1,http2,http3 --scenarios hello,json
```

`Test.Automated` is the main integration suite.

`Test.XUnit` mirrors the automated coverage for `dotnet test`.

`Test.Benchmark` is for throughput, latency, and allocation validation, not correctness.

## Key files

- `src/WatsonWebserver/Core/WebserverBase.cs`
- `src/WatsonWebserver/Core/WebserverSettings.cs`
- `src/WatsonWebserver/Core/Settings/`
- `src/WatsonWebserver/Core/ApiRequest.cs`
- `src/WatsonWebserver/Core/RequestParameters.cs`
- `src/WatsonWebserver/Core/Routing/ApiRouteHandler.cs`
- `src/WatsonWebserver/Core/Routing/ApiResponseProcessor.cs`
- `src/WatsonWebserver/Core/Health/WebserverHealthExtensions.cs`
- `src/WatsonWebserver/Core/OpenApi/`
- `src/WatsonWebserver/Webserver.cs`
- `src/Test.RestApi/Program.cs`
- `src/Test.Automated/ApiRouteIntegrationTests.cs`
- `src/Test.Automated/LegacyCoverageSuite.cs`
- `src/Test.Benchmark/`

## Coding rules

These rules are mandatory in this repository:

- no `var`
- no tuples in new code
- use `using (...)` statements instead of `using` declarations
- keep `using` statements inside namespace blocks
- XML docs on all public members, even inside non-public types
- public members named `LikeThis`
- private members named `_LikeThis`
- one entity per file
- add null checks on setters where appropriate
- clamp configurable values to reasonable ranges where appropriate
- do not use `JsonElement` property accessors where a typed model should exist

## Practical guidance

- Prefer changing Core when behavior should be shared across HTTP/1.1, HTTP/2, and HTTP/3.
- Prefer transport-specific changes only when protocol framing or connection behavior differs.
- For behavioral fixes, add or update `Test.Automated` coverage first.
- For API-route changes, check README examples and `Test.RestApi`.
- For protocol work, update the archived implementation notes only when the repository actually proves the item.
