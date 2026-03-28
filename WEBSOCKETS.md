# WebSockets Plan

This document defines the plan to merge WatsonWebsocket functionality into Watson 7 natively, without relying on `HttpListener` or `http.sys`.

The plan is intentionally actionable:
- each major work item is broken into concrete tasks
- each task can be marked complete in place
- developers can annotate findings, decisions, regressions, and follow-up work directly in this file

## How To Use This Document

Follow the phases in order.

For each checklist item:
- change `[ ]` to `[x]` when complete
- change `[ ]` to `[~]` when partially complete
- add a short note under the item when implementation details, deviations, or follow-up work matter
- if a task is intentionally deferred, mark it `[>]` and explain why
- if a task is rejected or replaced, mark it `[-]` and explain the replacement

Suggested annotation format:

```md
- [x] Add HTTP/1.1 WebSocket upgrade parser
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/...`
  Validation: `SharedWebSocketSmokeTests.Http11UpgradeSucceeds`
```

## Objectives

The merged Watson 7 WebSocket implementation should:
- preserve the useful WatsonWebsocket server capabilities developers rely on today
- run on Watson 7 native transports rather than `HttpListener`
- integrate naturally with Watson 7 routing, settings, events, logging, middleware, and tests
- support clean HTTP/1.1 WebSocket upgrade first
- explicitly define what is and is not supported for HTTP/2 and HTTP/3
- avoid creating a parallel server stack inside the repository

## WatsonWebsocket 4.0 Today

The current WatsonWebsocket server is built around:
- `HttpListener`
- `AcceptWebSocketAsync`
- `HttpListenerContext` / `HttpListenerRequest`
- a standalone `WatsonWsServer` API surface

Key server behaviors and features currently exposed:
- synchronous and asynchronous server start/stop
- event-driven connection lifecycle:
  - `ClientConnected`
  - `ClientDisconnected`
  - `MessageReceived`
  - `ServerStopped`
- `Guid`-based client identity
- optional caller-supplied GUID via `x-guid` header
- per-client metadata:
  - `Guid`
  - `Ip`
  - `Port`
  - `Name`
  - `Metadata`
- `ListClients()`
- `IsClientConnected(Guid)`
- `DisconnectClient(Guid)`
- `SendAsync(Guid, string|byte[]|ArraySegment<byte>, WebSocketMessageType, CancellationToken)`
- optional raw HTTP fallback handler for non-WebSocket requests
- simple permitted-IP filter
- basic statistics

The current WatsonWebsocket client is built around:
- `ClientWebSocket`
- `WatsonWsClient`
- connection lifecycle events
- send/receive helpers
- cookie injection
- caller header injection
- optional client GUID advertisement

## Watson 7 Today

Watson 7 already owns:
- HTTP/1.1 transport
- HTTP/2 transport
- HTTP/3 transport
- TLS
- routing
- request/response abstractions
- logging/events
- testing infrastructure:
  - `Test.Automated`
  - `Test.XUnit`
  - `Test.Shared`
  - `Test.Benchmark`

Watson 7 does not currently provide:
- native WebSocket server upgrade handling
- native WebSocket frame processing
- an integrated WebSocket endpoint registration model
- server-side WebSocket client/session tracking

## Architectural Decision Summary

The plan should proceed with these assumptions unless explicitly changed during implementation:

1. HTTP/1.1 WebSocket support is the first required deliverable.
2. HTTP/2 WebSocket support via RFC 8441 is not a phase-1 deliverable.
3. HTTP/3 WebSocket support is not a phase-1 deliverable.
4. Watson 7 should reject unsupported WebSocket attempts on HTTP/2 and HTTP/3 explicitly and predictably.
5. Server-side WebSocket support belongs in Watson 7 itself, not in a separate `HttpListener`-backed companion package.
6. The old WatsonWebsocket API should inform the new API, but not dictate an exact one-to-one surface if a Watson 7-native model is better.
7. A compatibility/shim layer may be added later if needed, but the native Watson 7 API is the primary design target.

## Target End State

At the end of this initiative, Watson 7 should have:
- native HTTP/1.1 WebSocket upgrade support
- a Watson 7-native server-side WebSocket API
- route-level upgrade registration
- access to the initial HTTP request metadata during upgrade decisions
- connection/session tracking and send/disconnect helpers
- message receive callbacks or handlers
- clear close/error semantics
- native automated coverage in both:
  - `Test.Automated`
  - `Test.XUnit`
- clear documentation in the README and testing docs

Optional follow-on work:
- a Watson 7-compatible replacement for `WatsonWsClient`
- RFC 8441 support for HTTP/2 WebSockets
- an eventual HTTP/3 WebSocket story if the team wants it

## Proposed API Direction

The exact naming can change, but the shape should look like Watson 7 rather than a separate server product.

Recommended direction:
- add a WebSocket feature area under Watson 7 rather than a separate server class
- expose WebSocket route registration on `WebserverBase` or `WebserverRoutes`
- keep `HttpContextBase` for the handshake phase, then hand off to a WebSocket session/context object

Possible route registration patterns:

```csharp
server.WebSocket("/chat", HandleSocketAsync);
server.WebSocket("/ws/{room}", HandleSocketAsync);
server.WebSocket("/ws", async (HttpContextBase ctx, WebSocketSession session) => { ... });
```

or:

```csharp
server.WebSockets.Map("/chat", HandleSocketAsync);
```

Recommended session surface:
- session ID or client `Guid`
- remote endpoint metadata
- request metadata from the opening handshake
- negotiated subprotocol
- send text
- send binary
- close
- per-session metadata bag
- connection state

Recommended event surface:
- `WebSocketConnecting`
- `WebSocketConnected`
- `WebSocketDisconnected`
- `WebSocketMessageReceived`

The implementation may choose either:
- route-handler-first API
- event-first API

Preferred direction:
- route-handler-first for Watson 7 native usage
- optional events for observability and compatibility scenarios

## Important Product Decisions

These decisions should be made early and documented as they are resolved.

### 1. Protocol Scope

- [ ] Confirm phase-1 scope is HTTP/1.1 only
- [ ] Confirm HTTP/2 WebSocket attempts will return a documented rejection path
- [ ] Confirm HTTP/3 WebSocket attempts will return a documented rejection path
- [ ] Decide whether to return `400`, `404`, `426`, or another documented response for unsupported upgrade attempts on non-HTTP/1.1 protocols

### 2. API Style

- [ ] Decide whether the primary public model is route-based, event-based, or both
- [ ] Decide whether to preserve `Guid` as the primary session identity
- [ ] Decide whether to expose a `ClientMetadata`-like type, a `WebSocketSession` type, or both
- [ ] Decide whether user code receives the opening `HttpContextBase`, a reduced request descriptor, or both

### 3. Packaging

- [ ] Decide whether server-side WebSocket support ships only in `Watson`
- [ ] Decide whether any common WebSocket abstractions belong in `Watson.Core`
- [ ] Decide whether a Watson 7-native client should be added in this repo now, later, or never

## Phase 1: Design And Inventory

### 1.1 Capture WatsonWebsocket Surface Area

- [ ] Inventory the full public server API from `WatsonWsServer`
- [ ] Inventory the full public client API from `WatsonWsClient`
- [ ] Inventory all public event args and metadata types
- [ ] Inventory settings and defaults from `WebsocketSettings`
- [ ] Inventory statistics fields and semantics
- [ ] Inventory behavioral expectations covered by WatsonWebsocket automated tests
- [ ] Capture which WatsonWebsocket behaviors are truly required for Watson 7 phase 1
- [ ] Mark which WatsonWebsocket behaviors are compatibility nice-to-haves rather than core requirements

Notes:

### 1.2 Define Watson 7 Native Scope

- [ ] Write down the minimal phase-1 feature set for HTTP/1.1 server-side WebSockets
- [ ] Write down what is explicitly out of scope for phase 1
- [ ] Write down what phase 2 may include
- [ ] Resolve whether the non-WebSocket HTTP fallback handler concept should survive, and if so, how it maps onto Watson 7 routing

Notes:

## Phase 2: Core Transport And Protocol Work

### 2.1 HTTP/1.1 Upgrade Detection

- [ ] Add a native HTTP/1.1 upgrade detection path for WebSocket requests
- [ ] Validate required request elements:
  - `Connection: Upgrade`
  - `Upgrade: websocket`
  - `Sec-WebSocket-Version: 13`
  - `Sec-WebSocket-Key`
- [ ] Reject malformed or incomplete WebSocket upgrade requests predictably
- [ ] Generate `Sec-WebSocket-Accept` correctly
- [ ] Send `101 Switching Protocols` correctly
- [ ] Ensure the connection is removed cleanly from normal HTTP request-response flow after successful upgrade

Validation targets:
- successful upgrade
- missing key
- bad version
- missing/incorrect upgrade headers
- case-insensitive header handling

Notes:

### 2.2 Native WebSocket Framing

- [ ] Add a native WebSocket frame parser for server-side connections
- [ ] Add a native WebSocket frame writer for server-side connections
- [ ] Support masked client frames
- [ ] Support fragmented messages
- [ ] Support text messages
- [ ] Support binary messages
- [ ] Support close frames
- [ ] Support ping frames
- [ ] Support pong frames
- [ ] Enforce payload length rules
- [ ] Enforce reserved-bit and opcode validation rules
- [ ] Decide message-size limits and implement them

Implementation guidance:
- use pooled buffers where appropriate
- avoid unbounded per-message allocations
- keep send serialization per connection
- separate low-level frame parsing from public session abstractions

Notes:

### 2.3 Connection Lifecycle And Session Ownership

- [ ] Add a WebSocket session type owned by Watson 7
- [ ] Ensure session disposal closes network resources deterministically
- [ ] Ensure half-closed and error states are handled cleanly
- [ ] Ensure disconnects remove sessions from tracking reliably
- [ ] Ensure close handshakes are attempted where valid
- [ ] Ensure abrupt disconnects still trigger cleanup and user notifications

Notes:

## Phase 3: Watson 7 Public API

### 3.1 Settings

- [ ] Add `WebSocketSettings` under `WatsonWebserver.Core.Settings`
- [ ] Add a `Settings.WebSockets` property to `WebserverSettings`
- [ ] Include defaults and value clamping for:
  - enable/disable
  - max message size
  - receive buffer size
  - send timeout
  - close timeout
  - ping/pong or heartbeat options if supported
  - max concurrent sessions if desired
- [ ] Decide whether permitted IP filtering belongs in `AccessControl` rather than WebSocket-specific settings
- [ ] Decide whether GUID-header configuration is kept, renamed, or removed

Recommended phase-1 settings to consider:
- `Enable`
- `MaxMessageSize`
- `ReceiveBufferSize`
- `CloseHandshakeTimeoutMs`
- `AllowClientSuppliedGuid`
- `ClientGuidHeaderName`

Notes:

### 3.2 Route Registration

- [ ] Add a public route registration surface for WebSocket endpoints
- [ ] Ensure route matching works with static and parameterized paths
- [ ] Ensure upgrade endpoints participate cleanly in existing routing
- [ ] Decide whether auth routing applies before upgrade
- [ ] Decide whether middleware runs before upgrade, after upgrade, or both
- [ ] Document route matching precedence for WebSocket endpoints versus normal HTTP routes

Notes:

### 3.3 Session API

- [ ] Add a public `WebSocketSession`-style type
- [ ] Expose server-generated session identity
- [ ] Expose remote endpoint
- [ ] Expose request metadata from the opening handshake
- [ ] Expose optional developer metadata bag
- [ ] Add `SendTextAsync`
- [ ] Add `SendBinaryAsync`
- [ ] Add `CloseAsync`
- [ ] Add connected/closed state visibility
- [ ] Add subprotocol visibility if negotiated

Notes:

### 3.4 Events And Observability

- [ ] Decide which connection/message lifecycle events should exist on `WebserverEvents`
- [ ] Add events for connect, disconnect, and message receive if that model is retained
- [ ] Ensure logging callbacks receive meaningful upgrade/session diagnostics
- [ ] Decide whether statistics should be integrated into `WebserverStatistics`, WebSocket-specific stats, or both

Recommended phase-1 metrics:
- current open WebSocket sessions
- total accepted WebSocket upgrades
- total rejected WebSocket upgrade attempts
- messages received
- messages sent
- bytes received
- bytes sent

Notes:

## Phase 4: Compatibility Mapping

This phase exists so that WatsonWebsocket users can understand how to migrate.

### 4.1 Server Compatibility Mapping

- [ ] Document the WatsonWebsocket server features and their Watson 7 equivalents
- [ ] Map `ClientConnected` to the new Watson 7 lifecycle hook
- [ ] Map `ClientDisconnected` to the new Watson 7 lifecycle hook
- [ ] Map `MessageReceived` to the new Watson 7 lifecycle hook
- [ ] Map `ListClients()` to the new Watson 7 session enumeration API if provided
- [ ] Map `IsClientConnected(Guid)` to the new Watson 7 session lookup API if provided
- [ ] Map `DisconnectClient(Guid)` to the new Watson 7 close/disconnect API
- [ ] Map GUID-header negotiation behavior if retained
- [ ] Decide whether `HttpHandler` is replaced by ordinary HTTP routes

Notes:

### 4.2 Client Compatibility Decision

- [ ] Decide whether `WatsonWsClient` should remain separate from Watson 7 scope
- [ ] If yes, document that server-side merge is complete but client-side migration remains separate
- [ ] If no, create a follow-on phase for a Watson 7-native client or compatibility wrapper

Recommendation:
- do not block server-side WebSocket merge on client-library migration

Notes:

## Phase 5: Protocol Boundaries

### 5.1 HTTP/1.1

- [ ] Support RFC 6455 over HTTP/1.1
- [ ] Ensure keep-alive/request accounting transitions correctly from HTTP to WebSocket session ownership
- [ ] Ensure upgraded connections no longer participate in normal HTTP request reuse logic

Notes:

### 5.2 HTTP/2

- [ ] Explicitly reject classic `Upgrade: websocket` usage on HTTP/2
- [ ] Decide whether RFC 8441 is a later phase
- [ ] Document the rejection behavior clearly in README and tests

Recommendation:
- phase 1 should reject and document
- RFC 8441 should be a separate, deliberate follow-on project

Notes:

### 5.3 HTTP/3

- [ ] Explicitly reject classic WebSocket assumptions on HTTP/3 in phase 1
- [ ] Decide whether HTTP/3 WebSocket support is a future initiative
- [ ] Document the rejection behavior clearly

Notes:

## Phase 6: Testing

All meaningful WebSocket server tests should live in both:
- `Test.Automated`
- `Test.XUnit`

Reusable infrastructure should live in:
- `Test.Shared`

### 6.1 Shared Test Infrastructure

- [ ] Add shared WebSocket test helpers in `Test.Shared`
- [ ] Add a loopback WebSocket client helper for raw upgrade and frame tests
- [ ] Add helper coverage for TLS `wss` where appropriate
- [ ] Add helper coverage for malformed upgrade requests
- [ ] Add helper coverage for fragmented frames

Notes:

### 6.2 Positive Server Coverage

- [ ] Basic HTTP/1.1 upgrade succeeds
- [ ] Basic text message receive
- [ ] Basic binary message receive
- [ ] Server sends text message
- [ ] Server sends binary message
- [ ] Multiple clients connect and receive isolated messages
- [ ] Broadcast behavior if implemented
- [ ] Large message receive
- [ ] Fragmented message receive
- [ ] Ping/pong handling
- [ ] Graceful close initiated by client
- [ ] Graceful close initiated by server
- [ ] Session enumeration
- [ ] Session disconnect by ID
- [ ] Request metadata available during connection
- [ ] Parameterized route upgrade
- [ ] Auth-protected route upgrade behavior

Notes:

### 6.3 Negative Server Coverage

- [ ] Missing `Sec-WebSocket-Key` rejected
- [ ] Wrong `Sec-WebSocket-Version` rejected
- [ ] Missing `Upgrade` rejected
- [ ] Missing `Connection: Upgrade` rejected
- [ ] Bad handshake method/path rejected
- [ ] Masking violations rejected
- [ ] Invalid opcode rejected
- [ ] Oversized message rejected
- [ ] Bad continuation sequence rejected
- [ ] Message on closed socket handled safely
- [ ] Disconnect cleanup runs on abrupt remote close

Notes:

### 6.4 Protocol Coverage

- [ ] HTTP/1.1 websocket route accepts upgrade
- [ ] HTTP/2 websocket attempt is rejected as documented
- [ ] HTTP/3 websocket attempt is rejected as documented
- [ ] TLS `wss` handshake works

Notes:

### 6.5 Runner Presentation

- [ ] Ensure `Test.Automated` names are clear and protocol-oriented
- [ ] Ensure `Test.XUnit` names are equally clear
- [ ] Ensure no test names use stale implementation-history labels

Notes:

## Phase 7: Documentation

### 7.1 README

- [ ] Add a WebSocket section to the README
- [ ] Document supported phase-1 protocols
- [ ] Document unsupported WebSocket scenarios explicitly
- [ ] Add usage examples for server-side WebSocket routes
- [ ] Add migration notes for WatsonWebsocket users
- [ ] Document TLS behavior for `wss`

Notes:

### 7.2 Testing Docs

- [ ] Add WebSocket test commands to `TESTING.md`
- [ ] Document how to run WebSocket smoke tests in `Test.Automated`
- [ ] Document how to run WebSocket CI tests in `Test.XUnit`

Notes:

### 7.3 Changelog

- [ ] Add a changelog entry when WebSocket support lands
- [ ] Call out protocol scope and known limitations

Notes:

## Phase 8: Performance And Resource Review

WebSocket support should not regress the normal HTTP server hot path materially.

- [ ] Ensure the HTTP/1.1 non-WebSocket path stays fast when WebSockets are enabled
- [ ] Benchmark upgrade throughput if practical
- [ ] Benchmark steady-state WebSocket send/receive throughput if practical
- [ ] Review connection/session lifetime cleanup for leaks
- [ ] Review send-path serialization and lock contention
- [ ] Review large-message buffering behavior

Notes:

## Phase 9: Release And Migration

- [ ] Decide whether the feature lands behind a disabled-by-default setting first
- [ ] Decide whether to release as preview/experimental first
- [ ] Add migration notes for WatsonWebsocket users
- [ ] Add explicit note that Watson 7 WebSockets no longer depend on `http.sys`
- [ ] Decide whether WatsonWebsocket should be archived, maintained, or documented as legacy

Notes:

## Deliverables Checklist

These are the minimum deliverables for a complete phase-1 merge.

- [ ] Native HTTP/1.1 WebSocket upgrade support in Watson 7
- [ ] Native server-side WebSocket session API
- [ ] Route registration for WebSocket endpoints
- [ ] Basic connect/send/receive/close lifecycle
- [ ] Shared automated coverage in `Test.Automated` and `Test.XUnit`
- [ ] README and testing documentation
- [ ] Changelog entry
- [ ] Explicit documentation for unsupported HTTP/2 and HTTP/3 WebSocket scenarios

## Explicit Non-Goals For Phase 1

These should remain out of scope unless deliberately pulled in.

- [ ] RFC 8441 WebSocket over HTTP/2
- [ ] WebSocket over HTTP/3
- [ ] Full compatibility shim for every old WatsonWebsocket API quirk
- [ ] A client-library rewrite unless separately approved

## Implementation Notes

- Use Watson 7 transport ownership directly; do not reintroduce `HttpListener`.
- Keep the WebSocket server work under the Watson 7 architecture rather than building a sidecar server inside the repo.
- Prefer behavior-oriented public naming:
  - `WebSocketSettings`
  - `WebSocketSession`
  - `WebSocketEvents`
  - `MapWebSocket(...)`
- Keep protocol restrictions explicit:
  - HTTP/1.1: supported
  - HTTP/2: rejected for phase 1
  - HTTP/3: rejected for phase 1

## Progress Summary

Use this section as the quick status dashboard during implementation.

- Design complete: [ ]
- HTTP/1.1 upgrade complete: [ ]
- Native framing complete: [ ]
- Public API complete: [ ]
- Shared test coverage complete: [ ]
- Documentation complete: [ ]
- Ready for release review: [ ]
