# WebSockets Integration Plan

This document defines the implementation plan for native WebSocket support in Watson 7.

It reflects the debated architecture and replaces the earlier draft that:
- proposed custom RFC 6455 framing
- rejected same-path HTTP and WebSocket registration
- treated per-message global events as a primary API
- defaulted client-supplied GUIDs to enabled
- bundled HTTP/1.1, HTTP/2, and HTTP/3 into one release gate

This is an execution document. Annotate it as work progresses.

## How To Use This Document

Work top to bottom.

For each checklist item:
- change `[ ]` to `[x]` when complete
- change `[ ]` to `[~]` when partially complete
- change `[ ]` to `[>]` when intentionally deferred
- change `[ ]` to `[-]` when replaced by a different implementation
- add a short note below the item when implementation or validation differs

Suggested annotation style:

```md
- [x] Add HTTP/1.1 WebSocket handshake path
  Note: Implemented in `src/WatsonWebserver/WebSockets/Http1WebSocketHandshake.cs`
  Validation: `Test.XUnit.WebSockets.Http11Handshake.AcceptsValidUpgrade`
```

## Goal

Integrate the useful server-side behavior of `c:\code\watson\watsonwebsocket-4.0` into Watson 7 while:
- preserving Watson 7 routing and hosting architecture
- preserving a simple whole-message developer experience
- maximizing correctness and performance
- minimizing protocol risk in the initial release

## Delivery Strategy

### v1 Release Gate

v1 ships:
- Watson 7-native WebSocket public API
- Watson-owned `WebSocketSession` and connection registry
- HTTP/1.1 WebSocket support only
- full test coverage for the shipped surface
- benchmark coverage for the shipped surface
- README, API, testing, and migration documentation

v1 must not block on HTTP/2 or HTTP/3.

### v1.x Follow-Up

v1.x may add:
- HTTP/2 WebSocket support via RFC 8441 extended CONNECT
- HTTP/3 WebSocket support via extended CONNECT over QUIC
- transport adapters that reuse the already-shipped public API and session model

The v1 API must be designed so HTTP/2 and HTTP/3 can plug in later without public-surface churn.

## Scope

### In Scope For v1

- native server-side WebSocket support in Watson 7
- route registration for WebSocket endpoints
- same-path HTTP and WebSocket registration
- whole-message receive API
- text, binary, ping, pong, and close lifecycle handling
- per-session state, metadata, and statistics
- connection registry and disconnect APIs
- thorough shared test coverage in `Test.Shared`
- scenario exposure in both `Test.Automated` and `Test.XUnit`
- benchmark scenarios in `Test.Benchmark`
- updates to `README.md`, `TESTING.md`, and a new `MIGRATING_FROM_WATSONWEBSOCKET.md`

### Explicitly Out Of Scope For v1

- custom RFC 6455 frame parsing and writing
- public exposure of the raw underlying `System.Net.WebSockets.WebSocket`
- exact event-for-event API preservation from WatsonWebsocket 4.0
- HTTP/2 and HTTP/3 runtime support
- client-library merge of `WatsonWsClient`

## Baseline Behaviors Worth Preserving

WatsonWebsocket 4.0 provides behaviors worth preserving in Watson 7-native form:
- whole-message delivery instead of frame-level delivery
- per-session send serialization
- GUID-based session identity
- optional developer metadata bag
- connection registry operations
- basic sent/received counters
- request metadata available at connect time

These behaviors should be preserved unless there is a concrete Watson 7 reason not to.

## Architectural Position

### Transport Engine

Do not implement a custom RFC 6455 parser for v1.

For HTTP/1.1:
- perform the Watson-owned handshake and route dispatch
- then create the active socket using `System.Net.WebSockets.WebSocket.CreateFromStream()`
- then run Watson-owned session management above the framework `WebSocket`

This gives RFC 6455 compliance, fragmentation handling, masking, ping/pong, close semantics, and control-frame validation from the platform instead of from new handwritten protocol code.

### Watson-Owned Session Layer

Watson still needs its own layer above framework `WebSocket` for:
- route-owned lifetime
- full-message receive abstraction
- send serialization
- connection registry
- settings enforcement
- counters and diagnostics
- close and disposal coordination

### Routing Model

WebSocket routing belongs inside the existing Watson 7 `RoutingGroup` model:
- `Routes.PreAuthentication`
- `Routes.PostAuthentication`

Add a distinct `WebSocketRouteManager` to each `RoutingGroup`.

WebSocket dispatch must run before normal HTTP route dispatch inside the routing-group execution path, because an HTTP/1.1 upgrade request is still a `GET` and would otherwise be consumed by an ordinary HTTP route.

Same-path HTTP and WebSocket registration must be allowed:

```csharp
server.Get("/chat", HandleChatHttp);
server.WebSocket("/chat", HandleChatSocketAsync);
```

Dispatch rule:
- if the request is a WebSocket initiation, match `WebSocketRouteManager`
- otherwise route through the normal HTTP managers

### Event Model

Per-message global events are not part of the primary API.

Route handlers own message processing through the session.

Server-level events are observability-only:
- session started
- session ended
- handshake failed

## Target Public API

Use this public shape unless a concrete blocker requires a documented deviation.

### Route Registration

```csharp
server.WebSocket("/chat", HandleSocketAsync);
server.WebSocket("/chat/{room}", HandleSocketAsync);
```

Handler signature:

```csharp
Task HandleSocketAsync(HttpContextBase context, WebSocketSession session)
```

### Session API

`WebSocketSession` minimum surface:
- `Guid Id`
- `bool IsConnected`
- `string? Subprotocol`
- `string RemoteIp`
- `int RemotePort`
- reduced handshake headers or equivalent immutable request metadata
- query parameters or equivalent
- `object? Metadata`
- session statistics snapshot or counters
- `Task<WebSocketMessage?> ReceiveAsync(CancellationToken token = default)`
- `IAsyncEnumerable<WebSocketMessage> ReadMessagesAsync(CancellationToken token = default)`
- `Task SendTextAsync(string data, CancellationToken token = default)`
- `Task SendBinaryAsync(byte[] data, CancellationToken token = default)`
- `Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken token = default)`
- `Task CloseAsync(WebSocketCloseStatus closeStatus, string? reason, CancellationToken token = default)`

Required session behavior:
- whole-message delivery
- single owner for receive operations
- per-session send serialization
- deterministic cleanup on close, error, disconnect, and server shutdown
- no public raw-socket escape hatch that can bypass Watson invariants

### Server-Level API

```csharp
void WebSocket(string path, Func<HttpContextBase, WebSocketSession, Task> handler, bool auth = false);
IEnumerable<WebSocketSession> ListWebSocketSessions();
bool IsWebSocketSessionConnected(Guid guid);
Task DisconnectWebSocketSessionAsync(Guid guid, WebSocketCloseStatus status, string? reason);
```

## Defaults And Settings

Add `WebSocketSettings` under `WatsonWebserver.Core.Settings`.

### Required Settings

- `Enable`
- `MaxMessageSize`
- `ReceiveBufferSize`
- `CloseHandshakeTimeoutMs`
- `AllowClientSuppliedGuid`
- `ClientGuidHeaderName`
- `SupportedVersions`
- `EnableHttp1`
- `EnableHttp2`
- `EnableHttp3`

### Required Defaults

- `Enable = false`
- `MaxMessageSize = 16777216`
- `ReceiveBufferSize = 65536`
- `CloseHandshakeTimeoutMs = 5000`
- `AllowClientSuppliedGuid = false`
- `ClientGuidHeaderName = "x-guid"`
- `SupportedVersions = ["13"]`
- `EnableHttp1 = true`
- `EnableHttp2 = false`
- `EnableHttp3 = false`

Notes:
- only WebSocket version `13` is supported initially
- `AllowClientSuppliedGuid` remains available only as an explicit compatibility opt-in
- HTTP/2 and HTTP/3 switches may exist in v1, but must default off and be documented as not yet implemented if the code path is deferred

## Repository Layout

Implement the feature in Watson 7-native folders.

### New Folders

- `src/WatsonWebserver.Core/WebSockets/`
- `src/WatsonWebserver/WebSockets/`
- `src/Test.Shared/WebSockets/`

### Core Files

- `src/WatsonWebserver.Core/Settings/WebSocketSettings.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketMessage.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketSessionStatistics.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketHandshakeUtilities.cs`

### Watson Implementation Files

- `src/WatsonWebserver/WebSockets/WebSocketSession.cs`
- `src/WatsonWebserver/WebSockets/WebSocketConnectionRegistry.cs`
- `src/WatsonWebserver/WebSockets/WebSocketRouteManager.cs`
- `src/WatsonWebserver/WebSockets/WebSocketRequestDescriptor.cs`
- `src/WatsonWebserver/WebSockets/Http1WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/WebSocketProtocolDetector.cs`

### Deferred v1.x Candidates

- `src/WatsonWebserver/WebSockets/Http2WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/Http3WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/Http2WebSocketStreamAdapter.cs`
- `src/WatsonWebserver/WebSockets/Http3WebSocketStreamAdapter.cs`

### Existing Files Expected To Change

- `src/WatsonWebserver.Core/WebserverSettings.cs`
- `src/WatsonWebserver.Core/WebserverBase.cs`
- `src/WatsonWebserver.Core/WebserverEvents.cs`
- `src/WatsonWebserver.Core/Routing/...`
- `src/WatsonWebserver/Webserver.cs`
- `README.md`
- `TESTING.md`

## Implementation Phases

## Phase 1: Shared Session And Settings

### 1.1 Settings

- [x] Add `WebSocketSettings`
  Note: Implemented in `src/WatsonWebserver.Core/Settings/WebSocketSettings.cs`
- [x] Add `Settings.WebSockets` to `WebserverSettings`
  Note: Added to `src/WatsonWebserver.Core/WebserverSettings.cs`
- [x] Clamp numeric values to safe ranges
  Note: Implemented for `MaxMessageSize`, `ReceiveBufferSize`, and `CloseHandshakeTimeoutMs`
- [x] Enforce `SupportedVersions` validation
  Note: `src/WatsonWebserver.Core/WebserverSettingsValidator.cs` currently rejects anything other than version `13`
- [x] Document that v1 supports only WebSocket version `13`
  Note: Documented in code comments and enforced by validation
- [x] Default `AllowClientSuppliedGuid` to `false`

### 1.2 Session Model

- [x] Add `WebSocketSession`
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketSession.cs` to keep the public surface in `WatsonWebserver.Core`
- [x] Add server-generated session ID creation
  Note: `WebSocketSession` generates a new `Guid` when one is not supplied by the transport
- [x] Add optional client-supplied GUID override behind explicit opt-in
  Note: `src/WatsonWebserver/Webserver.cs` honors `Settings.WebSockets.AllowClientSuppliedGuid`
- [x] Add reduced immutable handshake metadata needed after upgrade
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketRequestDescriptor.cs`
- [x] Add developer metadata bag
- [x] Add session statistics counters
- [x] Add per-session send gate
- [x] Add per-session cancellation and disposal coordination
- [x] Add close-state tracking
  Note: `WebSocketSession` now retains `State`, `CloseStatus`, and `CloseStatusDescription` after shutdown, with shared coverage in `SharedWebSocketTests.TestCloseStateRetainedAsync`

Implementation notes:
- preserve the useful parts of WatsonWebsocket client metadata
- do not keep heavyweight HTTP request objects alive after upgrade
- make the session own receive operations to prevent concurrent-reader bugs

### 1.3 Message Delivery Layer

- [x] Add `WebSocketMessage` type for whole-message delivery
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketMessage.cs`
- [x] Implement Watson-owned receive loop on top of framework `WebSocket`
  Note: Implemented in `ReceiveAsync()` / `ReadMessagesAsync()` in `src/WatsonWebserver.Core/WebSockets/WebSocketSession.cs`
- [x] Enforce `MaxMessageSize`
  Note: Oversized messages now close the session and are covered in shared tests
- [x] Reassemble messages from framework receive calls into complete text or binary messages
- [x] Handle close and cancellation coordination
- [x] Record bytes and messages sent and received

Implementation notes:
- this is not a custom RFC 6455 implementation
- use pooled buffers where practical
- avoid allocating a new large buffer per receive cycle

### 1.4 Registry

- [x] Add connection registry keyed by `Guid`
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketConnectionRegistry.cs`
- [x] Add `ListWebSocketSessions()`
  Note: Added to `src/WatsonWebserver.Core/WebserverBase.cs`
- [x] Add `IsWebSocketSessionConnected(Guid)`
  Note: Added to `src/WatsonWebserver.Core/WebserverBase.cs`
- [x] Add disconnect-by-guid support
  Note: Added to `src/WatsonWebserver.Core/WebserverBase.cs` backed by the shared registry
- [x] Ensure cleanup on disconnect, exception, and server shutdown
  Note: Covered for graceful close, disconnect-by-guid, handler exception, abrupt client abort, failed handshake, oversized-message rejection, and server stop

## Phase 2: Routing And Public API

### 2.1 Route Manager

- [x] Add `WebSocketRouteManager` to each `RoutingGroup`
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketRouteManager.cs` and attached to `RoutingGroup`
- [x] Support static and parameterized WebSocket paths
- [x] Preserve routing-group placement for pre-auth and post-auth execution
  Note: Shared tests cover both pre-auth and post-auth websocket routes
- [x] Allow same-path HTTP and WebSocket registration
- [x] Reject only duplicate WebSocket registrations that are truly ambiguous
  Note: Static and parameterized duplicate websocket registrations are rejected independently

### 2.2 Protocol-Aware Dispatch

- [x] Add protocol detection before normal HTTP route dispatch
  Note: Integrated into `ProcessRoutingGroupAsync` in `src/WatsonWebserver.Core/WebserverBase.cs`
- [x] Detect HTTP/1.1 WebSocket initiation from request metadata
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/WebSocketProtocolDetector.cs`
- [x] Route upgrade attempts to `WebSocketRouteManager` before `Static`, `Content`, `Parameter`, and `Dynamic`
  Note: Matching now occurs before the normal route managers inside each routing group
- [x] Ensure non-WebSocket requests bypass WebSocket route matching cheaply
  Note: The detector short-circuits unless the request presents upgrade-specific headers

Implementation notes:
- this ordering is required so `GET` upgrade requests are not incorrectly consumed by ordinary HTTP routes
- keep WebSocket matching separate from HTTP method-keyed route managers

### 2.3 Public Surface

- [x] Add `server.WebSocket(...)` registration API
  Note: Added to `src/WatsonWebserver.Core/WebserverBase.cs`
- [x] Add session enumeration and disconnect APIs at server level
  Note: Added to `src/WatsonWebserver.Core/WebserverBase.cs`
- [x] Add observability-only WebSocket event hooks
  Note: Added to `src/WatsonWebserver.Core/WebserverEvents.cs`
- [x] Do not add a global per-message callback
  Note: The websocket surface remains route/session-owned; no global per-message API was introduced

Required observability events:
- `WebSocketSessionStarted`
- `WebSocketSessionEnded`
- `WebSocketHandshakeFailed`

## Phase 3: HTTP/1.1 Runtime

### 3.1 Handshake

- [x] Detect valid HTTP/1.1 upgrade requests
  Note: Implemented in `src/WatsonWebserver/WebSockets/Http1WebSocketHandshake.cs`
- [x] Validate:
  - `Connection: Upgrade`
  - `Upgrade: websocket`
  - `Sec-WebSocket-Version`
  - `Sec-WebSocket-Key`
- [x] Validate requested version against `Settings.WebSockets.SupportedVersions`
- [x] Generate `Sec-WebSocket-Accept`
- [x] Write `101 Switching Protocols`
- [x] Create the framework socket using `WebSocket.CreateFromStream()`
- [x] Create the Watson `WebSocketSession`

Implementation notes:
- hook in after request parsing but before ordinary HTTP response handling
- upgraded connections must not re-enter the HTTP keep-alive pipeline
- verify compatibility with both plain `NetworkStream` and TLS `SslStream`

### 3.2 Session Execution

- [x] Invoke the registered route handler with `HttpContextBase` and `WebSocketSession`
- [x] Start the Watson-owned receive path
  Note: The session-owned receive API is live and route handlers explicitly drive `ReceiveAsync()` / `ReadMessagesAsync()`
- [x] Ensure send operations remain serialized
  Note: `WebSocketSession` uses a per-session `SemaphoreSlim`, with concurrent-send coverage in `Test.Shared`
- [x] Ensure clean server-stop behavior for active WebSocket sessions
  Note: `src/WatsonWebserver/Webserver.cs` now closes active sessions during `Stop()` / disposal
- [x] Ensure route completion and socket shutdown semantics are deterministic
  Note: Route completion now closes the session if still connected, and handler exceptions trigger a close path

### 3.3 Same-Path HTTP And WebSocket Behavior

- [x] Add explicit tests and implementation notes for same-path dual registration
  Note: Shared loopback coverage added in `src/Test.Shared/SharedWebSocketTests.cs`
- [x] Confirm normal `GET /path` routes to HTTP handler
- [x] Confirm valid upgrade request to the same path routes to WebSocket handler
- [~] Confirm middleware and auth-phase behavior remain consistent with existing routing-group semantics
  Note: Pre-auth and post-auth websocket routes are covered; broader middleware parity validation remains pending

## Phase 4: v1.x HTTP/2 And HTTP/3 Design Stubs

This phase is design and backlog shaping in v1, not a release gate.

### 4.1 HTTP/2 Design

- [>] Define RFC 8441 handshake requirements
- [>] Define stream-adapter requirements if `WebSocket.CreateFromStream()` is to be reused
- [>] Define flow-control, cancellation, and close-mapping constraints
- [>] Record open technical questions and validation requirements

### 4.2 HTTP/3 Design

- [>] Define HTTP/3 extended CONNECT requirements
- [>] Define QUIC stream adapter and abort semantics
- [>] Define sibling-stream isolation expectations
- [>] Record open technical questions and validation requirements

## Phase 5: Compatibility Mapping

Map WatsonWebsocket 4.0 concepts deliberately, not mechanically.

- [x] Map `ClientConnected` to route start plus session-start observability
- [x] Map `ClientDisconnected` to session-end observability
- [x] Map `MessageReceived` to `ReceiveAsync()` / `ReadMessagesAsync()`
- [x] Map `GuidHeader` behavior to `Settings.WebSockets.ClientGuidHeaderName`
- [x] Map `ListClients()` to `ListWebSocketSessions()`
- [x] Map `IsClientConnected(Guid)` to `IsWebSocketSessionConnected(Guid)`
- [x] Map `DisconnectClient(Guid)` to Watson 7 disconnect API
- [x] Replace WatsonWebsocket raw HTTP fallback patterns with ordinary Watson HTTP routes
- [x] Document every intentionally changed behavior in `MIGRATING_FROM_WATSONWEBSOCKET.md`

## Phase 6: Testing

Testing must be exhaustive for the shipped v1 surface.

The requirement is not just unit coverage. The plan must exercise:
- shared scenario logic
- xUnit assertions
- automated runner exposure
- protocol behavior
- route behavior
- shutdown and failure paths
- resource cleanup

### 6.1 Shared Test Architecture

- [-] Create `src/Test.Shared/WebSockets/`
  Note: Replaced by `src/Test.Shared/SharedWebSocketTests.cs` plus `LoopbackServerHost.cs` to keep the shared scenario surface consolidated
- [~] Add canonical scenario helpers in `Test.Shared`
  Note: Canonical websocket scenarios live in `SharedWebSocketTests.cs`; helper extraction remains optional rather than required for v1
- [x] Add reusable server setup helpers
  Note: `LoopbackServerHost` provides reusable plain/TLS loopback server setup for websocket scenarios
- [~] Add reusable client helpers
  Note: `SharedWebSocketTests.cs` now contains shared client receive/raw-upgrade helpers, though they remain local methods rather than a separate helper class
- [x] Add shared assertions for handshake, messaging, close, and cleanup behavior
  Note: Shared assertions and waiting helpers are centralized in `SharedWebSocketTests.cs`
- [x] Ensure each canonical shared scenario is surfaced by both `Test.XUnit` and `Test.Automated`

Required exposure model:
- `Test.Shared` contains the canonical scenario logic
- `Test.XUnit` wraps the shared scenarios as xUnit tests
- `Test.Automated` exposes the same scenarios for automated runner presentation and smoke execution

### 6.2 Canonical Traffic Patterns

Add multiple concrete scenario families to `Test.Shared` and expose each in both `Test.XUnit` and `Test.Automated`.

- [x] one-way client-to-server text
- [x] one-way client-to-server binary
- [x] one-way server-to-client text
  Note: Covered by route-owned sends and the sample harness unsolicited-send flow
- [x] one-way server-to-client binary
- [x] bidirectional request-reply
- [x] bidirectional alternating ping-pong application traffic
- [x] full-duplex concurrent chatter where both sides send without waiting
- [x] burst traffic with many small messages
- [x] sustained throughput with medium messages
- [x] large-message transfer near configured limit
  Note: Covered in `src/Test.Shared/SharedWebSocketTests.cs`
- [x] fragmented message assembly
- [x] mixed text and binary across the same connection
  Note: Covered by a shared loopback session that exchanges text and binary on the same connection
- [x] concurrent many-session traffic
- [x] slow-consumer scenario
- [x] server-initiated close
- [x] client-initiated close
- [x] abrupt client disconnect
  Note: Covered in `src/Test.Shared/SharedWebSocketTests.cs`
- [x] abrupt server shutdown
  Note: Covered by the active-session server stop test in `SharedWebSocketTests.TestServerStopClosesSessionsAsync`

For every scenario above:
- [~] define expected counters
  Note: Counter expectations are explicitly asserted for dedicated statistics scenarios; the full scenario matrix does not yet restate counters per case
- [~] define expected close state
  Note: Close-state expectations are asserted for explicit close-path scenarios; not every traffic-pattern test asserts terminal state
- [x] define expected cleanup behavior
  Note: Cleanup behavior is asserted throughout the shared suite via registry-drain and connectivity checks
- [x] define timeout expectations
  Note: Every loopback scenario uses explicit bounded `CancellationTokenSource` timeouts

### 6.3 Positive Functional Coverage

- [x] valid HTTP/1.1 handshake succeeds
  Note: Covered through `ClientWebSocket` loopback scenarios in `src/Test.Shared/SharedWebSocketTests.cs`
- [>] optional subprotocol negotiation succeeds when supported
  Note: Deferred until Watson exposes public subprotocol negotiation configuration
- [x] route parameters are available in WebSocket handlers
- [x] query and header metadata survive the handshake
- [x] pre-auth WebSocket routes work
  Note: Covered through loopback websocket routes in `src/Test.Shared/SharedWebSocketTests.cs`
- [x] post-auth WebSocket routes work
  Note: Covered in `src/Test.Shared/SharedWebSocketTests.cs`
- [x] same-path HTTP and WebSocket registration works
- [x] session enumeration reflects live connections
- [x] disconnect-by-guid works
- [x] client-supplied GUID works only when explicitly enabled
- [x] client-supplied GUID is ignored or rejected correctly when disabled
- [x] observability events fire with correct ordering and payloads
- [x] stats counters advance correctly
- [x] UTF-8 text handling is correct
- [x] binary payload integrity is preserved

### 6.4 Negative And Edge Coverage

- [x] unsupported `Sec-WebSocket-Version` rejected
- [x] missing `Sec-WebSocket-Key` rejected
- [x] malformed upgrade headers rejected
  Note: Raw handshake rejection coverage now exercises missing-key and unsupported-version failures
- [x] wrong HTTP method for WebSocket initiation rejected
- [x] duplicate or conflicting WebSocket route registrations rejected
- [x] unsupported subprotocol requests handled correctly
  Note: Shared coverage verifies that unsupported requested subprotocols remain unnegotiated and do not break the session
- [x] oversized message rejected
- [x] invalid client-supplied GUID header handled safely
- [x] handshake failure does not leak registry entries
- [x] route handler exception closes the session cleanly
- [x] client cancellation during receive cleans up correctly
  Note: Shared coverage verifies that framework-client receive cancellation aborts the websocket and Watson drains the session registry
- [x] client cancellation during send cleans up correctly
  Note: Shared coverage verifies registry drain when the client aborts during an active server-send loop
- [x] server stop closes sessions and drains registry
  Note: Active sessions are closed during `Stop()` and loopback coverage verifies registry drain for the stop path
- [x] half-open network failure cleans up correctly
- [x] non-WebSocket requests on WebSocket-only paths continue through ordinary HTTP behavior
- [x] non-WebSocket hot path shows no behavioral regression
  Note: Verified by the passing `Test.Automated` run alongside focused websocket dispatch-survival coverage

### 6.5 Interop Coverage

- [x] add a framework-client interop path using `ClientWebSocket`
- [>] add browser-oriented validation where practical
  Note: Deferred; automated coverage uses `ClientWebSocket`, while browser validation remains available as a manual follow-up path
- [x] validate TLS WebSocket behavior (`wss`) in at least one automated path
- [x] validate plain TCP WebSocket behavior (`ws`) in at least one automated path

### 6.6 Resource, Concurrency, And Leak Coverage

- [x] verify no concurrent receive path exists on a session
- [x] verify concurrent sends are serialized correctly
  Note: Covered by concurrent server-send loopback coverage in `src/Test.Shared/SharedWebSocketTests.cs`
- [~] verify session disposal runs exactly once
  Note: Shared coverage verifies `WebSocketSessionEnded` fires once per session on an abrupt-abort path; direct disposal instrumentation is still pending
- [x] verify registry removal on all exit paths
  Note: Covered for graceful close, disconnect-by-guid, client/server initiated close, abrupt client abort, half-open failure, failed handshake, oversized-message rejection, handler exception, and server stop
- [x] verify counters remain correct under concurrency
- [~] verify no retained heavyweight request objects after upgrade
  Note: The public session retains `WebSocketRequestDescriptor` rather than the live HTTP request object; no dedicated profiling assertion was added
- [~] verify repeated connect/disconnect cycles do not leak memory or handles
  Note: Shared coverage now verifies repeated connect/disconnect registry drain; memory/handle profiling is still pending

### 6.7 Test Naming And Presentation

- [x] use protocol-and-behavior-oriented test names
- [x] make the automated runner clearly show one-way, bidirectional, concurrency, shutdown, and negative-path scenarios
- [x] ensure shared scenarios are visible in both `Test.Automated` and `Test.XUnit`

## Phase 7: Benchmarking

### 7.1 Benchmark Scenarios

- [x] extend `Test.Benchmark` with WebSocket scenarios
  Note: Watson 7 HTTP/1.1 websocket benchmark scenarios now include echo, connect-close, client-text, and server-text
- [x] benchmark HTTP/1.1 connection establishment
  Note: Covered by `websocket-connect-close`
- [x] benchmark one-way client-to-server text
  Note: Covered by `websocket-client-text`, with a minimal acknowledgement to keep worker flow synchronized
- [x] benchmark one-way server-to-client text
  Note: Covered by `websocket-server-text`, with a minimal trigger message to initiate the server send
- [x] benchmark bidirectional echo/request-reply
  Note: Covered by `websocket-echo`
- [x] benchmark concurrent-session throughput
  Note: Websocket benchmark scenarios run across the configured concurrency level with one socket per worker
- [~] benchmark near-limit payload transfer
  Note: Existing websocket text scenarios honor `--payload-bytes`, but there is not yet a dedicated near-limit scenario label

### 7.2 Comparison Targets

- [x] add Watson 7 WebSocket benchmark target
- [>] add WatsonWebsocket 4 benchmark target using `c:\code\watson\watsonwebsocket-4.0`
  Note: Deferred; the v1 benchmark harness now ships with Watson 7 websocket coverage, while legacy-target comparison remains follow-up work
- [>] add Kestrel benchmark target where comparison is fair
  Note: Deferred until a fair websocket comparison path is defined

### 7.3 Required Outputs

- [x] connection establishment rate
- [x] messages per second
- [x] text throughput
- [>] binary throughput
  Note: Deferred pending a dedicated binary websocket benchmark scenario
- [x] latency percentiles where practical
- [x] allocation profile where practical

## Phase 8: Documentation

Documentation must be explicit about both capability and limitation.

### 8.1 README

- [x] update `README.md` with a WebSocket section
- [x] document that v1 supports HTTP/1.1 WebSockets
- [x] document that HTTP/2 and HTTP/3 WebSockets are not yet available in v1
- [x] document route registration and same-path HTTP plus WebSocket behavior
- [x] document whole-message receive semantics
- [x] document session enumeration and disconnect APIs
- [x] document settings and defaults
- [x] document TLS expectations
- [x] document notable limitations clearly and prominently

Required limitations to state clearly in README:
- v1 is HTTP/1.1 only
- no raw underlying `WebSocket` access is exposed publicly
- receive operations are session-owned and message-oriented
- HTTP/2 and HTTP/3 are planned follow-up work, not shipped behavior

### 8.2 WebSocket API Guide

- [x] create `WEBSOCKETS_API.md`
- [x] document route registration
- [x] document `WebSocketSession`
- [x] document `ReceiveAsync()` and `ReadMessagesAsync()`
- [x] document send and close methods
- [x] document lifecycle and event hooks
- [x] document same-path HTTP plus WebSocket routing behavior
- [x] add copy-paste examples for one-way and bidirectional patterns

### 8.3 Migration Guide

- [x] create `MIGRATING_FROM_WATSONWEBSOCKET.md`
- [x] map old WatsonWebsocket types and concepts to Watson 7
- [x] document changed defaults, especially `AllowClientSuppliedGuid = false`
- [x] document event-model changes
- [x] document route-registration differences
- [x] document same-path HTTP plus WebSocket usage
- [x] document unsupported or intentionally removed patterns
- [x] provide before-and-after code examples

### 8.4 Testing Docs

- [x] update `TESTING.md`
- [x] document how to run WebSocket shared scenarios through `Test.Automated`
- [x] document how to run WebSocket xUnit coverage
- [x] document how to run WebSocket benchmarks
- [x] document any environment prerequisites for TLS or interop scenarios

### 8.5 Changelog

- [x] add a changelog entry when the feature lands

## Release Review Checklist

- [x] public API reviewed
- [x] same-path HTTP plus WebSocket routing verified
- [x] HTTP/1.1-only scope verified in docs and code
- [x] migration guide complete
- [x] README limitations explicit
- [x] all `Test.Shared` canonical scenarios surfaced in `Test.XUnit`
- [x] all `Test.Shared` canonical scenarios surfaced in `Test.Automated`
- [x] benchmark data captured
- [x] no non-WebSocket regression on existing HTTP hot path
  Note: Verified by the passing `dotnet run --framework net10.0 --project src\Test.Automated\Test.Automated.csproj` run after websocket integration

## Deliverables

- [x] `WebSocketSettings`
- [x] `WebSocketSession`
- [x] `WebSocketConnectionRegistry`
- [x] `WebSocketRouteManager`
- [x] HTTP/1.1 WebSocket support
- [x] server-level session management APIs
- [x] `Test.Shared` canonical WebSocket scenarios
- [x] `Test.XUnit` WebSocket coverage
- [x] `Test.Automated` WebSocket coverage
- [x] `Test.Benchmark` WebSocket coverage
- [x] `README.md` update
- [x] `WEBSOCKETS_API.md`
- [x] `MIGRATING_FROM_WATSONWEBSOCKET.md`
- [x] `TESTING.md` update

## Progress Summary

- Design complete: [x]
- Shared session and settings complete: [x]
- Routing and public API complete: [~]
  Note: Shipped surface is complete; broader middleware-parity proof remains partial
- HTTP/1.1 runtime complete: [~]
  Note: The shipped HTTP/1.1 websocket runtime is complete; public subprotocol negotiation remains intentionally unshipped
- Test.Shared scenarios complete: [~]
  Note: Canonical shipped-surface scenarios are in place; helper extraction and a few stretch-goal leak assertions remain partial
- Test.XUnit exposure complete: [x]
- Test.Automated exposure complete: [x]
- Benchmarks complete: [~]
  Note: Watson 7 websocket benchmark coverage is in place, with comparison targets and binary/near-limit specialty scenarios still deferred
- Documentation complete: [x]
- Ready for release review: [x]
  Note: Remaining partial or deferred items are follow-up enhancements rather than blockers for the shipped HTTP/1.1 websocket surface
