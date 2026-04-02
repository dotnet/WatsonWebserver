# Change Log

## Current Version

`v7.0.4`

## v7.0.4

- Merged `Watson.Core` into the `Watson` package as a single assembly
- The `WatsonWebserver.Core` namespace is preserved for backward compatibility
- Core source files now live under `src/WatsonWebserver/Core/`
- Consumers install only the `Watson` NuGet package; there is no longer a separate `Watson.Core` package
- Existing `using WatsonWebserver.Core;` statements require no changes

## v7.0.0

Watson 7.0 is a major release that expands the server from an HTTP/1.1-focused model into a unified multi-protocol platform with shared consumer semantics across HTTP/1.1, HTTP/2, and HTTP/3.

### WebSockets

- Added native Watson 7 HTTP/1.1 WebSocket support with Watson-owned handshake handling and `WebSocketSession`
- Added websocket route registration through `server.WebSocket(...)`, including same-path HTTP and websocket dispatch
- Added websocket session enumeration, connectivity checks, and disconnect-by-guid APIs
- Added websocket observability events for session start, session end, and handshake failure
- Added `Test.WebsocketServer` and `Test.WebsocketClient` interactive sample applications
- Added shared websocket coverage in `Test.Shared`, surfaced through both `Test.XUnit` and `Test.Automated`
- Added initial websocket benchmark coverage in `Test.Benchmark`
- Added websocket-specific documentation in `README.md`, `WEBSOCKETS_API.md`, `MIGRATING_FROM_WATSONWEBSOCKET.md`, and `TESTING.md`

### Protocols And Transport

- Added protocol-level configuration through `WebserverSettings.Protocols`
- Added support in `Watson` for:
  - HTTP/1.1
  - HTTP/2 over TLS
  - HTTP/2 cleartext prior knowledge when explicitly enabled
  - HTTP/3 over TLS/QUIC
- Added runtime HTTP/3 availability detection and startup normalization
- Added Alt-Svc emission support through `WebserverSettings.AltSvc`
- Added HTTP/2 and HTTP/3 connection, request, response, and protocol-processing paths

### Consumer Configuration Surface

- Added `ProtocolSettings`
  - `EnableHttp1`
  - `EnableHttp2`
  - `EnableHttp3`
  - `EnableHttp2Cleartext`
  - `IdleTimeoutMs`
  - `MaxConcurrentStreams`
  - `Http2`
  - `Http3`
- Added `AltSvcSettings`
  - `Enabled`
  - `Authority`
  - `Port`
  - `Http3Alpn`
  - `MaxAgeSeconds`
- Added HTTP/3 tuning through `Http3Settings`
  - `MaxFieldSectionSize`
  - `QpackMaxTableCapacity`
  - `QpackBlockedStreams`
  - `EnableDatagram`
- Retained and extended `WebserverSettings.IO`, `Ssl`, `Headers`, `AccessControl`, `Debug`, and `UseMachineHostname`

### Validation And Runtime Safety

- Added `WebserverSettingsValidator` protocol validation
- Startup now fails fast when:
  - all protocols are disabled
  - HTTP/2 is enabled on a transport that does not support it
  - HTTP/3 is enabled on a transport that does not support it
  - HTTP/2 is enabled without TLS and without explicit cleartext prior-knowledge mode
  - HTTP/3 is enabled without TLS
  - Alt-Svc is enabled while HTTP/3 is disabled
- When HTTP/3 is configured but QUIC is unavailable at runtime, Watson now disables HTTP/3 and Alt-Svc for that process start rather than advertise unsupported behavior

### Request And Response Semantics

- Unified request and response consumption model across protocols through `HttpContextBase`, `HttpRequestBase`, and `HttpResponseBase`
- Added protocol metadata to request and context objects
  - negotiated protocol
  - connection metadata
  - stream metadata
- Clarified chunked-transfer semantics:
  - HTTP/1.1 request `ReadChunk()` remains available for actual chunked transfer-encoding
  - HTTP/2 and HTTP/3 requests now explicitly reject `ReadChunk()` because those protocols do not expose HTTP/1.1 chunk semantics
  - `SendChunk()` and `SendEvent()` remain available across protocols, with HTTP/2 and HTTP/3 using transport-native framing semantics
- Added request trailer support when the protocol permits it
- Added response trailer support when the protocol permits it
- Added `ReadBodyAsync()` to make full-body reads explicit and cache-aware

### FastAPI-Like API Route Integration (SwiftStack)

Watson 7.0 integrates the SwiftStack REST experience directly into the webserver, providing a FastAPI-like developer experience without requiring a separate library.

- Added `server.Get()`, `server.Post<T>()`, `server.Put<T>()`, `server.Patch<T>()`, `server.Delete()`, `server.Head()`, `server.Options()` convenience methods on `WebserverBase`
- Added `ApiRequest` wrapper providing typed access to URL parameters, query parameters, headers, and deserialized request body
- Added `RequestParameters` class with typed accessors (`GetInt`, `GetGuid`, `GetBool`, `GetEnum<T>`, `TryGetValue<T>`, etc.)
- Added automatic JSON serialization of handler return values via `ApiResponseProcessor`
  - `null` returns empty response
  - `string` returns `text/plain`
  - Objects return `application/json`
  - `(object, int)` tuples set custom HTTP status codes
- Added `WebserverException` for structured error responses mapping to HTTP status codes
- Added `ApiErrorResponse` with `ApiResultEnum` for consistent error payloads
- Added middleware pipeline (`MiddlewarePipeline`, `MiddlewareDelegate`) with per-request execution and short-circuit support
- Added structured authentication via `AuthenticateApiRequest` returning `AuthResult` with automatic 401 responses
- Added request timeout support via `WebserverSettings.Timeout` with cooperative cancellation and 408 responses
- Added built-in health check endpoints via `UseHealthCheck()` with custom check delegates
- Added `RoutingGroupApiExtensions` for API route registration on `RoutingGroup` instances
- API routes support all existing Watson features: OpenAPI metadata, exception handlers, pre/post-authentication groups

### Routing, Lifecycle, And Extensibility

- Preserved the existing Watson routing pipeline while aligning it with the 7.0 shared protocol architecture
- Continued support for:
  - preflight routes
  - pre-routing hooks
  - pre-authentication routes
  - authentication hooks
  - post-authentication routes
  - default routes
  - post-routing hooks
- Retained `HostBuilder` fluent configuration support
- Added or expanded lifecycle metadata and cancellation handling through `HttpContextBase`
- Retained `Metadata` on `HttpContextBase` for application-defined request state

### OpenAPI / Swagger

- Added built-in OpenAPI 3.0 document generation
- Added built-in Swagger UI hosting
- Added `UseOpenApi()` extension methods on `WebserverBase`
- Added route-level OpenAPI metadata support for documented endpoints
- Added `Test.OpenApi` coverage and examples

### Performance

The 7.0 optimization program benchmarked each candidate independently and retained only the items that improved throughput without introducing correctness regressions.

Kept optimizations in the 7.0 line:

- Item 3: static-route reads now use frozen snapshots and the normalized path is reused
- Item 4: cached `JsonSerializerOptions` in `DefaultSerializationHelper`
- Item 5: cached serialized header prefixes on the simple-response path
- Item 7: pooled HTTP/1.1 request/response/context objects for keep-alive reuse
- Item 12: lazy header materialization for HTTP/2 and HTTP/3 requests
- Item 17: internal `ConfigureAwait(false)` consistency fix on the response path

Optimization candidates that regressed performance or behavior were benchmarked, documented, and reverted before release.

### Testing And Tooling

- Added `Test.RestApi` interactive server demonstrating all API route integration features
- Replaced `Test.All` with `Test.Automated`
- Added `Test.XUnit` with unit tests for core types (`RequestParameters`, `MiddlewarePipeline`, `AuthResult`, `WebserverException`, `ApiErrorResponse`, `TimeoutSettings`)
- Refactored automated tests to emit:
  - per-test pass/fail result lines
  - per-test runtime
  - final aggregate pass/fail summary
  - final failed-test enumeration
- Expanded automated coverage for the higher-risk 7.0 areas, including:
  - route snapshot coherency
  - serializer behavior
  - cached response header paths
  - pooled HTTP/1.1 object reset behavior
  - HTTP/2 and HTTP/3 lazy header materialization behavior
- Added stable execution scripts and testing documentation for repository contributors

### Breaking Changes And Behavioral Changes

- Watson 7.0 should be treated as a major release for consumers
- Protocol enablement is now explicit and validated
- HTTP/2 cleartext use requires explicit prior-knowledge opt-in
- HTTP/3 requires TLS and depends on runtime QUIC availability
- `ReadChunk()` is now explicitly an HTTP/1.1-only API surface
- Multi-protocol deployments should prefer protocol-agnostic body reads through `ReadBodyAsync()`, `Data`, `DataAsBytes`, or `DataAsString`
- `UseMachineHostname` behavior remains part of the host-handling model and is forced when wildcard hostnames such as `*` or `+` are used

## Previous Versions

### v6.6.0

- Fixed post-authentication content routes calling the pre-authentication handler
- Fixed `PostRouting` not being awaited
- Fixed static `CancellationTokenSource` sharing across contexts
- Added directory traversal protection in content-route file serving
- Added `MaxRequestBodySize`
- Added `MaxHeaderCount`
- Added `IDisposable` support to context, request, and response base classes
- Fixed `MemoryStream` leaks on response paths
- Improved route-manager and body-read performance
- Replaced route-manager `lock` usage with `ReaderWriterLockSlim`
- Added preparatory HTTP/1.1 annotations ahead of protocol work

### v6.5.x

- Added OpenAPI and Swagger support

### v6.4.x

- Minor breaking changes to server-sent events

### v6.3.x

- Changed `SendChunk` to accept `isFinal`
- Added server-sent events
- Minor internal refactor

### v6.2.x

- Added exception-handler support for static, content, parameter, and dynamic routes

### v6.1.x

- Moved `ContentRouteHandler` into `ContentRouteManager`

### v6.0.x

- Major refactor consolidating WatsonWebserver and HttpServerLite concepts
- Consolidated shared types into `WatsonWebserver.Core`
- Removed attribute routes
- Unified constructors
- Simplified SSL configuration around `X509Certificate2`
- Introduced current routing architecture and host-builder changes

### v5.1.x

- Added `HostBuilder`

### v5.0.x

- Moved to `NameValueCollection`
- Reintroduced header and query helper methods on `HttpRequest`

### v4.x

- Added parameter routes
- Added response/request data helper changes
- Added route details within `HttpContext`
- Added improved constructor and hostname behavior
- Added support for unknown HTTP methods

### v3.x

- Added and expanded chunked-transfer support
- Moved route callbacks to async `Task` signatures
- Added stream and callback improvements
- Added statistics and multi-listener support

### v2.x

- Added pre-routing callback
- Added automatic decoding of chunked inbound requests
- Added request and response stream support
- Simplified constructors and response model

### v1.x

- Added static routes, dynamic routes, content routes, whitelist/blacklist behavior, multiple-host support, and early framework portability work
