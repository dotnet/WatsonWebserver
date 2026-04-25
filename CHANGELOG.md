# Change Log

## Current Version

`v7.0.14`

## v7.0.14

- Fixed HTTP/1.1 disconnect detection so transport aborts on response writes mark the active request aborted, cancel the per-request token, raise `RequestAborted`, and raise the existing `RequestorDisconnected` event
- Implemented the previously-declared `RequestorDisconnected` event plumbing so disconnect observability now works end-to-end instead of remaining dead API surface
- Corrected response observability so failed HTTP/1.1 sends no longer emit a false `ResponseSent` signal, and chunked/SSE finalization no longer marks failed final writes as successful
- Prevented disconnect-triggered response send failures from falling through to the default "route did not send a response" HTTP 500 path
- Added deterministic shared-core coverage for disconnect transport-failure classification, request termination state, and failed final chunk/SSE sends
- Added end-to-end shared smoke coverage for successful chunked/SSE observability and for a client disconnect during a large HTTP/1.1 response, wired into both `Test.XUnit` and `Test.Automated`

## v7.0.13

- Fixed issue `#192`, a shutdown race where disposing the server during active connection teardown could trigger an unobserved task `NullReferenceException` while releasing `_RequestSemaphore`
- Captured the request semaphore per connection so shutdown can safely dispose and null the shared field without breaking in-flight `finally` blocks
- Added regression coverage for disposing the server while idle TCP connections are still unwinding

## v7.0.12

- Added OpenAPI schema composition support: `OpenApiSchemaMetadata.OneOf`, `OpenApiSchemaMetadata.Discriminator`, `OpenApiSchemaMetadata.CreateOneOf(...)`, and `OpenApiSchemaMetadata.WithDiscriminator(...)`
- Added `OpenApiDiscriminatorMetadata` modeling `propertyName` and an optional `mapping`
- Added `OpenApiSettings.Schemas` for registering reusable component schemas emitted under `components.schemas`
- Updated `OpenApiDocumentGenerator` to emit `oneOf`, `discriminator`, and `components.schemas`; the `$ref` short-circuit in `BuildSchema` is preserved for OpenAPI 3.0 compatibility
- Added shared `Test.Shared.SharedOpenApiCompositionTests` covering metadata helpers, component schema registration, `oneOf` with `$ref` branches, discriminator with and without mapping, scalar field regression, and `$ref` short-circuit; wired into `Test.Automated` and `Test.XUnit`

## v7.0.11

- Suppressed CA1416 platform compatibility warnings on QUIC call sites that are already guarded by runtime detection
- Fixed test ordering so the HTTP observability test no longer runs in the middle of the WebSocket test group

## v7.0.10

- Added `netstandard2.1` target for broader runtime compatibility (e.g. Unity, Xamarin, older .NET Core 3.x hosts)
- HTTP/3 and QUIC features require .NET 8 or later; on `netstandard2.1` they are gracefully unavailable
- Added conditional polyfills for `Stream.ReadExactlyAsync`, `SHA1.HashData`, `FrozenDictionary`, `Task.WaitAsync`, `IThreadPoolWorkItem`, and `SslProtocols.Tls13` on older target frameworks
- Added `System.Text.Json` and `Microsoft.CSharp` package references for the `netstandard2.1` target
- Added cross-protocol route method parity test suite covering GET, POST, PUT, DELETE, PATCH, HEAD, and OPTIONS across HTTP/1.1, HTTP/2, and HTTP/3 for static routes, parameter routes, dynamic (regex) routes, content routes, and API routes
- Added `netstandard2.1` compatibility unit tests validating runtime detection, protocol normalization, SHA1 handshake fallback, static route manager dictionary fallback, and server construction

## v7.0.9

- Fixed request timing so `HttpContextBase.Timestamp` starts at request entry instead of first lazy access
- Switched request/response elapsed timing to monotonic stopwatch-based measurement to avoid wall-clock skew spikes
- Added regression coverage for context timing and retained coverage for pooled HTTP/1.1 request-state reset

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
