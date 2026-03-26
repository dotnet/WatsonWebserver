# SwiftStack Integration Plan for Watson Webserver 7.0

## Goal

Integrate SwiftStack's FastAPI-like REST experience directly into Watson Webserver 7.0, providing first-class support for simplified route handlers with automatic serialization, typed parameter access, middleware pipelines, and OpenAPI documentation -- without requiring a separate library.

## Design Decision: Native Integration (Not a Wrapper)

SwiftStack today wraps Watson's `ParameterRouteManager` with a layer that:
1. Converts `Func<AppRequest, Task<object>>` handlers into `Func<HttpContextBase, Task>` handlers
2. Manages two route lists (authenticated / unauthenticated)
3. Provides automatic JSON serialization of return values
4. Provides `RequestParameters` with typed accessors over `NameValueCollection`
5. Provides middleware pipeline, timeout, health check, and OpenAPI support

**Integration approach**: Add these capabilities natively to `WebserverBase` and the routing system in `WatsonWebserver.Core`, so developers can use either the low-level `Func<HttpContextBase, Task>` handler signature OR the high-level `Func<ApiRequest, Task<object>>` handler signature directly on the server instance. No separate `RestApp` or `SwiftStackApp` class needed.

---

## Phase 1: Core Types (WatsonWebserver.Core)

### 1.1 Add `RequestParameters` to Core
- [ ] Create `src/WatsonWebserver.Core/RequestParameters.cs`
- [ ] Port `SwiftStack.Rest.RequestParameters` (typed accessors: `GetInt`, `GetBool`, `GetGuid`, `GetEnum<T>`, `TryGetValue<T>`, etc.)
- [ ] Namespace: `WatsonWebserver.Core`
- [ ] Wraps `NameValueCollection` (same as SwiftStack)
- [ ] Unit-testable in isolation (no Watson dependency)

### 1.2 Add `ApiRequest` to Core
- [ ] Create `src/WatsonWebserver.Core/ApiRequest.cs`
- [ ] Equivalent to SwiftStack's `AppRequest`, renamed to fit Watson naming
- [ ] Properties:
  - `HttpContextBase Http` -- full access to raw context
  - `object Data` -- deserialized request body (null when no body or no-body route)
  - `RequestParameters Parameters` -- URL path parameters with typed accessors
  - `RequestParameters Query` -- query string parameters with typed accessors
  - `RequestParameters Headers` -- request headers with typed accessors
  - `ISerializationHelper Serializer` -- serializer instance (use Watson's existing `ISerializationHelper` / `DefaultSerializationHelper`)
  - `CancellationToken CancellationToken` -- from context or timeout
  - `object Metadata` -- pass-through from `ctx.Metadata`
- [ ] Method: `T GetData<T>() where T : class` -- cast `Data` to specific type
- [ ] Constructor takes `HttpContextBase`, `ISerializationHelper`, `object data`, `CancellationToken`

### 1.3 Add `ApiErrorResponse` and `ApiResultEnum` to Core
- [ ] Create `src/WatsonWebserver.Core/ApiResultEnum.cs`
  - Port enum values: `Success(200)`, `Created(201)`, `BadRequest(400)`, `NotAuthorized(401)`, `NotFound(404)`, `Conflict(409)`, `SlowDown(429)`, `RequestTimeout(408)`, `DeserializationError(400)`, `InternalError(500)`
- [ ] Create `src/WatsonWebserver.Core/ApiErrorResponse.cs`
  - Properties: `Error`, `StatusCode` (computed), `Description` (computed), `Message`, `Data`
- [ ] Create `src/WatsonWebserver.Core/WebserverException.cs`
  - Replaces `SwiftStackException`; same behavior: takes `ApiResultEnum`, optional message, auto-maps to HTTP status code
  - Thrown from route handlers to trigger structured error responses

### 1.4 Add Serialization Interface
- [ ] Verify that `DefaultSerializationHelper` (already in Core) supports `SerializeJson(object)` and `DeserializeJson<T>(string)` patterns needed by the API route system
- [ ] If not, extend `ISerializationHelper` or add a new `IApiSerializer` interface that `DefaultSerializationHelper` implements
- [ ] Ensure pretty-print option is available
- [ ] Must be replaceable (user provides their own serializer implementation)

### 1.5 Add Authentication Result Types
- [ ] Create `src/WatsonWebserver.Core/AuthenticationResultEnum.cs`
  - Values: `Success`, `NotFound`, `Expired`, `PermissionDenied`
- [ ] Create `src/WatsonWebserver.Core/AuthorizationResultEnum.cs`
  - Values: `Permitted`, `DeniedImplicit`, `DeniedExplicit`
- [ ] Create `src/WatsonWebserver.Core/AuthResult.cs`
  - Properties: `AuthenticationResult`, `AuthorizationResult`, `Metadata`

---

## Phase 2: Middleware Pipeline (WatsonWebserver.Core)

### 2.1 Add Middleware Delegate and Pipeline
- [ ] Create `src/WatsonWebserver.Core/Middleware/MiddlewareDelegate.cs`
  - Signature: `delegate Task MiddlewareDelegate(HttpContextBase context, Func<Task> next, CancellationToken token)`
- [ ] Create `src/WatsonWebserver.Core/Middleware/MiddlewarePipeline.cs`
  - Port from SwiftStack: ordered list, `Add()`, `HasMiddleware`, `Execute(ctx, terminalHandler, token)`
  - Thread-safe registration (middleware registered before start, immutable after)

### 2.2 Integrate Pipeline into WebserverBase
- [ ] Add `MiddlewarePipeline Middleware` property to `WebserverBase`
- [ ] In `ProcessHttpContextAsync`, invoke middleware pipeline around the matched route handler (after routing match, before handler invocation)
- [ ] Middleware executes for ALL route types (static, parameter, dynamic, content), not just API routes
- [ ] Middleware can short-circuit by not calling `next()`

---

## Phase 3: API Route Registration (WatsonWebserver.Core)

This is the core of the FastAPI-like experience. The goal is to add `.Get()`, `.Post<T>()`, etc. methods that accept `Func<ApiRequest, Task<object>>` handlers directly on the route groups.

### 3.1 Add Response Processing Logic
- [ ] Create `src/WatsonWebserver.Core/Routing/ApiResponseProcessor.cs`
  - Port `ProcessResult` from SwiftStack's `RestApp`
  - Handles: `null` -> 204, `string` -> text/plain, primitives -> text/plain, objects -> JSON serialization
  - Handles tuple returns `(object, int)` for custom status codes
  - Skips processing when `ServerSentEvents` or `ChunkedTransfer` is active
  - Takes `ISerializationHelper` for JSON serialization

### 3.2 Add API Route Handler Wrapper
- [ ] Create `src/WatsonWebserver.Core/Routing/ApiRouteHandler.cs`
  - Internal class that wraps `Func<ApiRequest, Task<object>>` into `Func<HttpContextBase, Task>`
  - Handles body deserialization (generic `<T>` variant and no-body variant)
  - Constructs `ApiRequest` from `HttpContextBase`
  - Invokes `ApiResponseProcessor` on the return value
  - Handles `WebserverException` and `JsonException` with structured error responses
  - Integrates with `MiddlewarePipeline` if middleware registered

### 3.3 Add API Route Methods to RoutingGroup
- [ ] Add extension methods in `src/WatsonWebserver.Core/Routing/RoutingGroupApiExtensions.cs`
- [ ] Methods on `RoutingGroup`:
  ```
  // No-body routes (GET, DELETE, HEAD, OPTIONS)
  void Get(string path, Func<ApiRequest, Task<object>> handler, ...)
  void Delete(string path, Func<ApiRequest, Task<object>> handler, ...)
  void Head(string path, Func<ApiRequest, Task<object>> handler, ...)
  void Options(string path, Func<ApiRequest, Task<object>> handler, ...)

  // Body routes (POST, PUT, PATCH, DELETE with body)
  void Post<T>(string path, Func<ApiRequest, Task<object>> handler, ...) where T : class
  void Put<T>(string path, Func<ApiRequest, Task<object>> handler, ...) where T : class
  void Patch<T>(string path, Func<ApiRequest, Task<object>> handler, ...) where T : class

  // Non-generic body routes (manual deserialization)
  void Post(string path, Func<ApiRequest, Task<object>> handler, ...)
  void Put(string path, Func<ApiRequest, Task<object>> handler, ...)
  void Patch(string path, Func<ApiRequest, Task<object>> handler, ...)
  ```
- [ ] All methods support optional `Action<OpenApiRouteMetadata> openApi` parameter
- [ ] All methods support optional `Func<HttpContextBase, Exception, Task> exceptionHandler` parameter
- [ ] Routes register as parameter routes under the hood (same as SwiftStack)

### 3.4 Add Convenience Methods to WebserverBase
- [ ] Add shorthand methods directly on `WebserverBase` that default to `PreAuthentication`:
  ```
  server.Get("/path", handler)                -> Routes.PreAuthentication.Get(...)
  server.Post<T>("/path", handler)            -> Routes.PreAuthentication.Post<T>(...)
  server.Get("/path", handler, auth: true)    -> Routes.PostAuthentication.Get(...)
  ```
- [ ] The `auth` parameter (default `false`) switches between Pre/PostAuthentication groups
- [ ] This gives the most concise FastAPI-like experience:
  ```csharp
  server.Get("/users/{id}", async (req) => new { Id = req.Parameters.GetGuid("id") });
  server.Post<User>("/users", async (req) => { var user = req.GetData<User>(); return user; });
  ```

### 3.5 Serializer Configuration on WebserverBase
- [ ] Add `ISerializationHelper Serializer` property to `WebserverBase`
- [ ] Default to `DefaultSerializationHelper` (already exists in Core)
- [ ] User can replace with custom implementation
- [ ] API route handlers use this serializer for body deserialization and response serialization

---

## Phase 4: Timeout Support (WatsonWebserver.Core)

### 4.1 Add Timeout Settings
- [ ] Create `src/WatsonWebserver.Core/TimeoutSettings.cs`
  - Property: `TimeSpan DefaultTimeout` (default: `TimeSpan.Zero` = disabled)
- [ ] Add `TimeoutSettings Timeout` property to `WebserverSettings`

### 4.2 Integrate Timeout into API Route Handlers
- [ ] In `ApiRouteHandler`, create linked `CancellationTokenSource` when timeout > 0
- [ ] Pass timeout token via `ApiRequest.CancellationToken`
- [ ] On `OperationCanceledException` from timeout (not server shutdown): return 408 with `ApiErrorResponse`
- [ ] Dispose timeout CTS in finally block

---

## Phase 5: Health Check Support (WatsonWebserver.Core)

### 5.1 Add Health Check Types
- [ ] Create `src/WatsonWebserver.Core/Health/HealthStatusEnum.cs` -- `Healthy`, `Degraded`, `Unhealthy`
- [ ] Create `src/WatsonWebserver.Core/Health/HealthCheckResult.cs` -- `Status`, `Description`, `Data`
- [ ] Create `src/WatsonWebserver.Core/Health/HealthCheckSettings.cs` -- `Path` (default `/health`), `RequireAuthentication`, `CustomCheck` delegate

### 5.2 Add Health Check Extension
- [ ] Create `src/WatsonWebserver.Core/Health/WebserverHealthExtensions.cs`
- [ ] Extension method on `WebserverBase`: `UseHealthCheck(Action<HealthCheckSettings> configure = null)`
- [ ] Registers a parameter route at the configured path
- [ ] Returns 200 for Healthy/Degraded, 503 for Unhealthy

---

## Phase 6: OpenAPI Integration (WatsonWebserver.Core)

Watson Core already has `OpenApi/` with route metadata, document generation, and Swagger UI. This phase wires it into the API route registration.

### 6.1 Verify Existing OpenAPI Support
- [ ] Audit `src/WatsonWebserver.Core/OpenApi/` for compatibility with the new API route methods
- [ ] Ensure `OpenApiRouteMetadata` fluent builder works with the new registration flow
- [ ] Ensure `OpenApiDocumentGenerator` can consume route info from parameter routes

### 6.2 Add OpenAPI Extension for WebserverBase
- [ ] Create or extend `UseOpenApi(Action<OpenApiSettings> configure)` extension method on `WebserverBase`
- [ ] Automatically registers `/openapi.json` and `/swagger` routes
- [ ] Collects metadata from all registered API routes
- [ ] Fluent API on route registration:
  ```csharp
  server.Get("/users/{id}", handler, api => api
      .WithTag("Users")
      .WithSummary("Get user by ID")
      .WithResponse(200, OpenApiResponseMetadata.Json<User>("Success")));
  ```

---

## Phase 7: Protocol Compatibility (HTTP/1.1, HTTP/2, HTTP/3)

### 7.1 Verify Protocol Transparency
- [ ] All API route features (serialization, middleware, timeout, health) operate on `HttpContextBase` which is protocol-agnostic
- [ ] Confirm `ApiRequest` construction works identically for HTTP/1.1, HTTP/2, and HTTP/3 contexts
- [ ] Confirm `ApiResponseProcessor` works with all response implementations (Http1, Http2, Http3)
- [ ] Test chunked transfer and SSE through API routes on HTTP/2 (which uses its own framing)

### 7.2 Protocol-Specific Considerations
- [ ] HTTP/2 multiplexing: ensure middleware pipeline is per-stream, not per-connection
- [ ] HTTP/3 QUIC: same per-stream guarantee
- [ ] Verify timeout CTS is per-stream for HTTP/2/3
- [ ] Document any behavioral differences in API routes across protocols

---

## Phase 8: Authentication Integration

### 8.1 Structured Authentication
- [ ] Add `Func<HttpContextBase, Task<AuthResult>> AuthenticateApiRequest` property to `WebserverRoutes`
- [ ] When set, the routing pipeline uses structured `AuthResult` evaluation (like SwiftStack):
  - `Success + Permitted` -> continue to post-auth routes
  - Any other combination -> 401 with `ApiErrorResponse`
- [ ] Falls back to existing `AuthenticateRequest` (`Func<HttpContextBase, Task>`) for low-level control
- [ ] `AuthResult.Metadata` is propagated to `ctx.Metadata` and `ApiRequest.Metadata`

---

## Phase 9: Test Projects

### 9.1 Interactive Test Project (`Test.RestApi`)
A hands-on interactive server (similar to `Test.Default`) that stands up a full REST API instance for manual exploration via curl, browser, or Postman.

- [ ] Create `src/Test.RestApi/` project
- [ ] Console app that starts a server on `127.0.0.1:8080`
- [ ] Register routes covering all integration features:
  - `GET /` -- hello world, returns JSON
  - `GET /products` -- returns a list of product objects (auto-serialized)
  - `GET /products/{id}` -- typed parameter extraction (`GetGuid`)
  - `POST /products` -- `Post<CreateProductRequest>` with auto-deserialization
  - `PUT /products/{id}` -- `Put<UpdateProductRequest>` with auto-deserialization
  - `DELETE /products/{id}` -- no-body delete
  - `POST /upload` -- non-generic `Post` (manual body access)
  - `POST /login` -- returns tuple `(object, 201)` for custom status code
  - `GET /slow` -- sleeps 60s, demonstrates timeout (408)
  - `GET /error` -- throws `WebserverException(NotFound)`
  - `GET /admin/stats` -- post-authentication route (`auth: true`)
  - `GET /health` -- health check endpoint
  - `GET /openapi.json` and `/swagger` -- OpenAPI + Swagger UI
- [ ] Register middleware (request logging with timing)
- [ ] Register structured `AuthenticateApiRequest` (e.g. basic auth check)
- [ ] Enable timeout (30s)
- [ ] Enable health check with custom check delegate
- [ ] Enable OpenAPI with tags, summaries, and response metadata
- [ ] Print route table and instructions on startup (like `Test.Default`)
- [ ] Wait for keypress to exit

### 9.2 Automated Unit Tests (`Test.XUnit`)
xUnit project for CI-friendly automated testing of all new types in isolation (no server required).

- [ ] Create `src/Test.XUnit/` project
- [ ] Add `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` packages
- [ ] **RequestParameters tests:**
  - `GetInt` / `GetLong` / `GetDouble` / `GetDecimal` with valid, invalid, missing keys
  - `GetBool` with `true`/`false`/`1`/`0`/`yes`/`no`
  - `GetGuid` / `GetDateTime` / `GetTimeSpan` / `GetEnum<T>`
  - `TryGetValue<T>` success and failure paths
  - `GetArray` with different separators
  - `Contains` / `GetKeys` / indexer
- [ ] **ApiRequest tests:**
  - Construction from mocked `HttpContextBase`
  - `GetData<T>()` returns correct cast
  - `GetData<T>()` throws on type mismatch
  - `Parameters`, `Query`, `Headers` wrappers populated correctly
- [ ] **ApiErrorResponse tests:**
  - `StatusCode` computed from `ApiResultEnum`
  - `Description` auto-populated per enum value
- [ ] **WebserverException tests:**
  - Maps `ApiResultEnum` to correct HTTP status codes
  - Carries message and data
- [ ] **ApiResponseProcessor tests:**
  - `null` -> empty response
  - `string` -> text/plain
  - primitive -> text/plain
  - object -> JSON serialization
  - `(object, int)` tuple -> custom status + JSON
  - SSE/chunked flags -> skip processing
- [ ] **MiddlewarePipeline tests:**
  - No middleware -> terminal handler called directly
  - Single middleware -> wraps terminal handler
  - Multiple middleware -> execute in registration order
  - Short-circuit (don't call `next`) -> terminal not reached
  - Exception propagation through pipeline
- [ ] **AuthResult tests:**
  - Success + Permitted -> passes
  - NotFound + DeniedImplicit -> fails
  - Metadata propagation
- [ ] **TimeoutSettings tests:**
  - Default is `TimeSpan.Zero` (disabled)
  - Timeout CTS cancels after configured duration
- [ ] **HealthCheckResult tests:**
  - Status enum -> HTTP status code mapping (Healthy->200, Unhealthy->503)

### 9.3 Automated Integration Tests (`Test.Automated`)
Integration tests that start a real `Webserver` instance, send HTTP requests, and assert on responses.

- [ ] Create `src/Test.Automated/` project
- [ ] Add `xunit`, `Microsoft.NET.Test.Sdk`, and `System.Net.Http` packages
- [ ] Use `IAsyncLifetime` or test fixture to start/stop server on a random port per test class
- [ ] **API route integration tests:**
  - GET route returns 200 + JSON body
  - POST<T> route deserializes body and returns serialized response
  - POST (non-generic) route receives raw body
  - PUT<T> / PATCH<T> / DELETE routes work correctly
  - Unknown route returns default route response (404)
- [ ] **Serialization tests:**
  - Object return -> `application/json` content type
  - String return -> `text/plain` content type
  - Null return -> 204 no content (or empty 200)
  - Tuple `(body, statusCode)` return -> correct status + body
- [ ] **Parameter extraction tests:**
  - URL parameters (`/items/{id}`) extracted correctly
  - Query parameters (`?page=2&size=10`) extracted correctly
  - Multiple URL parameters in one route
- [ ] **WebserverException tests:**
  - Thrown exception -> structured JSON error response with correct status code
  - `JsonException` from bad body -> 400 with deserialization error
- [ ] **Middleware integration tests:**
  - Middleware modifies request/response (e.g., adds header)
  - Middleware short-circuit returns early response
  - Middleware ordering verified via response headers
- [ ] **Timeout integration tests:**
  - Fast route completes normally
  - Slow route returns 408 when timeout exceeded
  - Server shutdown cancellation distinct from timeout cancellation
- [ ] **Authentication integration tests:**
  - Unauthenticated route accessible without credentials
  - Authenticated route returns 401 without credentials
  - Authenticated route returns 200 with valid credentials
  - `AuthResult.Metadata` available in route handler via `req.Metadata`
- [ ] **Health check integration tests:**
  - Default health check returns 200 + Healthy
  - Custom check returning Unhealthy -> 503
- [ ] **OpenAPI integration tests:**
  - `/openapi.json` returns valid JSON document
  - Document contains registered routes with correct methods/paths
  - Fluent metadata (tags, summaries) appears in document
- [ ] **Protocol tests (conditional):**
  - HTTP/1.1 -- all above tests run over HTTP/1.1
  - HTTP/2 -- repeat key tests if platform supports it (skip otherwise)
  - HTTP/3 -- repeat key tests if QUIC available (skip otherwise)

### 9.4 Update Existing Test Projects
- [ ] Extend `src/Test.OpenApi/` to demonstrate fluent metadata on API routes
- [ ] Extend `src/Test.Authentication/` to demonstrate structured `AuthResult` flow

### 9.5 Add All New Test Projects to Solution
- [ ] Add `Test.RestApi`, `Test.XUnit`, `Test.Automated` to `WatsonWebserver.sln`
- [ ] Verify `dotnet build WatsonWebserver.sln` builds all new projects
- [ ] Verify `dotnet test` discovers and runs `Test.XUnit` and `Test.Automated`

---

## Phase 10: Documentation and Migration

### 10.1 README Update
- [ ] Update root `README.md` with v7.0 feature highlights:
  - FastAPI-like route registration (`server.Get()`, `server.Post<T>()`, etc.)
  - Automatic JSON serialization/deserialization
  - Typed parameter access (`RequestParameters`)
  - Middleware pipeline
  - Structured authentication (`AuthResult`)
  - Request timeouts with cooperative cancellation
  - Built-in health checks
  - OpenAPI / Swagger UI
  - HTTP/1.1, HTTP/2, HTTP/3 support
- [ ] Add "Quick Start" section showing the simplest REST API (5-10 lines)
- [ ] Add "API Routes" section with examples of each verb, body handling, and error handling
- [ ] Add "Middleware" section with registration and short-circuit examples
- [ ] Add "Authentication" section showing `AuthenticateApiRequest` usage
- [ ] Add "OpenAPI" section showing `UseOpenApi` and fluent metadata
- [ ] Update "Version History" / feature comparison table
- [ ] Preserve existing low-level usage documentation (both APIs coexist)

### 10.2 CHANGELOG
- [ ] Create or update `CHANGELOG.md` with a v7.0.0 entry covering:
  - **Added**: FastAPI-like route registration on `WebserverBase`
  - **Added**: `ApiRequest`, `RequestParameters`, `ApiResponseProcessor` for typed handler workflow
  - **Added**: Automatic JSON serialization/deserialization of request/response bodies
  - **Added**: `WebserverException` for structured error responses
  - **Added**: Middleware pipeline (`MiddlewarePipeline`, `MiddlewareDelegate`)
  - **Added**: Structured authentication (`AuthResult`, `AuthenticationResultEnum`, `AuthorizationResultEnum`)
  - **Added**: Request timeout support (`TimeoutSettings`)
  - **Added**: Built-in health check endpoints (`UseHealthCheck`)
  - **Added**: OpenAPI / Swagger UI integration (`UseOpenApi`)
  - **Added**: HTTP/2 support
  - **Added**: HTTP/3 support (QUIC, where platform supports it)
  - **Removed**: `WatsonWebserver.Lite` (consolidated into main package)
  - **Breaking**: List any breaking changes from v6.x (namespace changes, removed types, etc.)
  - Note: Follow [Keep a Changelog](https://keepachangelog.com/) format

### 10.3 Update CLAUDE.md
- [ ] Add API route usage patterns and examples
- [ ] Document middleware pipeline behavior
- [ ] Document serializer configuration
- [ ] Document health check setup
- [ ] Document timeout configuration

### 10.4 Update XML Documentation
- [ ] All new public types and methods have XML docs per coding standards
- [ ] Document exception behavior, nullability, and thread safety

### 10.5 Write Migration Guide
- [ ] Document how SwiftStack users migrate to native Watson 7.0 API routes
- [ ] Mapping table: `SwiftStack.Rest.RestApp.Get()` -> `server.Get()` / `server.Routes.PreAuthentication.Get()`
- [ ] Mapping table: `AppRequest` -> `ApiRequest`
- [ ] Mapping table: `SwiftStackException` -> `WebserverException`
- [ ] Mapping table: `SwiftStackApp` -> no equivalent needed (just use `Webserver` directly)
- [ ] Document what SwiftStack features are NOT migrated (WebSockets, RabbitMQ) and why

---

## Target Developer Experience

After integration, the simplest Watson 7.0 REST API looks like this:

```csharp
using WatsonWebserver;
using WatsonWebserver.Core;

var settings = new WebserverSettings("localhost", 8080, false);
using var server = new Webserver(settings);

// Simple GET
server.Get("/", async (req) => new { Message = "Hello, World!" });

// GET with typed parameters
server.Get("/users/{id}", async (req) =>
{
    Guid id = req.Parameters.GetGuid("id");
    return new { Id = id, Name = "John" };
});

// POST with automatic body deserialization
server.Post<CreateUserRequest>("/users", async (req) =>
{
    CreateUserRequest body = req.GetData<CreateUserRequest>();
    return (new { Id = Guid.NewGuid(), body.Email }, 201);
});

// POST without auto-deserialization (manual handling)
server.Post("/upload", async (req) =>
{
    byte[] data = req.Http.Request.Data;
    return new { Size = data.Length };
});

// Middleware
server.Middleware.Add(async (ctx, next, token) =>
{
    Console.WriteLine($"{ctx.Request.Method} {ctx.Request.Url.RawWithoutQuery}");
    await next();
});

// Health check
server.UseHealthCheck();

// OpenAPI
server.UseOpenApi(api =>
{
    api.Info.Title = "My API";
    api.Info.Version = "1.0.0";
});

// Timeouts
server.Settings.Timeout.DefaultTimeout = TimeSpan.FromSeconds(30);

// Structured authentication
server.Routes.AuthenticateApiRequest = async (ctx) =>
{
    // ... return AuthResult
};

// Protected route
server.Get("/admin/stats", async (req) => new { Users = 42 }, auth: true);

// Start
await server.StartAsync();
```

And the low-level `Func<HttpContextBase, Task>` signature continues to work exactly as before:

```csharp
server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/legacy", async (ctx) =>
{
    ctx.Response.StatusCode = 200;
    await ctx.Response.Send("works");
});
```

---

## Ordering and Dependencies

```
Phase 1 (Core Types)
  |
  v
Phase 2 (Middleware) ----+
  |                      |
  v                      v
Phase 3 (API Routes) <---+
  |
  +---> Phase 4 (Timeout)
  +---> Phase 5 (Health)
  +---> Phase 6 (OpenAPI)
  +---> Phase 8 (Auth)
  |
  v
Phase 7 (Protocol Compat) -- can proceed in parallel with 4-6, 8
  |
  v
Phase 9 (Tests) -- after all feature phases
  |
  v
Phase 10 (Docs) -- after tests pass
```

## Files Created/Modified Summary

**New files in `WatsonWebserver.Core`:**
- `RequestParameters.cs`
- `ApiRequest.cs`
- `ApiResultEnum.cs`
- `ApiErrorResponse.cs`
- `WebserverException.cs`
- `AuthResult.cs`
- `AuthenticationResultEnum.cs`
- `AuthorizationResultEnum.cs`
- `TimeoutSettings.cs`
- `Middleware/MiddlewareDelegate.cs`
- `Middleware/MiddlewarePipeline.cs`
- `Routing/ApiResponseProcessor.cs`
- `Routing/ApiRouteHandler.cs`
- `Routing/RoutingGroupApiExtensions.cs`
- `Health/HealthStatusEnum.cs`
- `Health/HealthCheckResult.cs`
- `Health/HealthCheckSettings.cs`
- `Health/WebserverHealthExtensions.cs`

**Modified files in `WatsonWebserver.Core`:**
- `WebserverBase.cs` -- add `Serializer`, `Middleware`, convenience methods, middleware pipeline integration
- `WebserverSettings.cs` -- add `Timeout` property
- `WebserverRoutes.cs` -- add `AuthenticateApiRequest` property
- `Routing/RoutingGroup.cs` -- (may need changes for extension method accessibility)

**New test projects:**
- `src/Test.RestApi/` -- interactive server for manual exploration (like Test.Default)
- `src/Test.XUnit/` -- unit tests for all new types (no server required)
- `src/Test.Automated/` -- integration tests with real HTTP requests against running server

**Modified test projects:**
- `src/Test.OpenApi/` -- use new API route methods
- `src/Test.Authentication/` -- use structured auth

**Documentation:**
- `README.md` -- updated with v7.0 features, quick start, API route examples
- `CHANGELOG.md` -- v7.0.0 entry with all additions, removals, and breaking changes

---

## Out of Scope

These SwiftStack features are NOT part of this integration:
- **WebSocket support** (`WebsocketsApp`) -- separate concern, can be added later
- **RabbitMQ integration** (`RabbitMqApp`) -- messaging is not a web server concern
- **SyslogLogging dependency** -- Watson uses its own event-based logging
- **SwiftStackApp container** -- unnecessary; `Webserver` is the application entry point
- **Favicon handling** -- trivial for users to add as a static route
