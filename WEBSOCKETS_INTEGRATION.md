# WebSockets Integration Plan

This document defines how WatsonWebsocket functionality will be merged natively into Watson 7.

This is an implementation plan, not just a feature wishlist. It is written so a developer can:
- execute the work in order
- annotate progress directly in this file
- record deviations, regressions, and follow-up work in place

## How To Use This Document

Work top to bottom.

For each checklist item:
- change `[ ]` to `[x]` when complete
- change `[ ]` to `[~]` when partially complete
- change `[ ]` to `[>]` when intentionally deferred
- change `[ ]` to `[-]` when replaced by a different implementation
- add a note immediately below the item when the implementation differs from the plan or when validation matters

Suggested annotation style:

```md
- [x] Add HTTP/1.1 WebSocket upgrade parser
  Note: Implemented in `src/WatsonWebserver.Core/WebSockets/Http1WebSocketHandshake.cs`
  Validation: `Test.XUnit.WebSocketHandshakeTests.Http11UpgradeSucceeds`
```

## Goal

Merge the server-side functionality of WatsonWebsocket 4.0 into Watson 7 so that Watson 7 natively supports WebSockets over:
- HTTP/1.1
- HTTP/2
- HTTP/3

without:
- `HttpListener`
- `http.sys`
- a sidecar server package
- a second routing stack

## Scope

### In Scope

- native server-side WebSocket support in Watson 7
- route registration for WebSocket endpoints
- request and handshake metadata access
- per-session state and metadata
- send, receive, and close lifecycle
- parity-style automated tests in:
  - `Test.Automated`
  - `Test.XUnit`
  - shared infrastructure in `Test.Shared`
- benchmarking in `Test.Benchmark`
- README and API documentation

### Out Of Scope For Initial Delivery

- full client-library merge of `WatsonWsClient`
- preserving every WatsonWebsocket 4.0 public type and event shape exactly
- compatibility shims for every historical WatsonWebsocket quirk

Client work can be added later, but server integration should not wait on it.

## Source Baseline

WatsonWebsocket 4.0 currently provides:
- `WatsonWsServer`
- `WatsonWsClient`
- `ClientConnected`
- `ClientDisconnected`
- `MessageReceived`
- `ServerStopped`
- GUID-based client identity
- optional client-supplied GUID via `x-guid`
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
- raw HTTP fallback for non-WebSocket requests
- basic send and receive statistics

Important existing implementation behavior in WatsonWebsocket:
- client messages are assembled until end-of-message before raising `MessageReceived`
- send operations are serialized per client using `SemaphoreSlim`
- client GUID assignment defaults to a new `Guid`, but can be overridden using the `x-guid` header
- `ClientConnected` gets handshake HTTP request metadata

The Watson 7 merge should preserve these useful behaviors unless there is a strong Watson 7-native reason not to.

## Watson 7 Integration Strategy

This work should be done by extending Watson 7's existing protocol ownership, not by embedding the old WatsonWebsocket server.

### Core Principle

Each protocol path should establish a WebSocket session using the protocol-appropriate handshake mechanism, then hand ownership of the stream or request stream into a shared WebSocket session implementation.

That means:
- HTTP/1.1 uses RFC 6455 upgrade
- HTTP/2 uses RFC 8441 extended CONNECT
- HTTP/3 uses WebSockets over HTTP/3 extended CONNECT semantics

The actual frame and message processing, session tracking, send serialization, stats, and public API should be shared across protocols as much as possible.

## Target Public API

Use the following public shape unless a concrete implementation blocker requires a documented change.

### Route Registration

Add WebSocket route registration directly to Watson 7.

Route-registration shape:

```csharp
server.WebSocket("/chat", HandleSocketAsync);
server.WebSocket("/chat/{room}", HandleSocketAsync);
```

Handler signature:

```csharp
Task HandleSocketAsync(HttpContextBase context, WebSocketSession session)
```

Why:
- the initial `HttpContextBase` gives access to request metadata from the handshake
- `WebSocketSession` becomes the long-lived object after protocol handoff

### Route Conflict Rule

Keep route conflict behavior simple:
- if a route is registered for normal HTTP handling
- and the same route is registered for WebSocket handling
- throw immediately during route registration

Do not:
- merge them
- create precedence rules
- attempt "HTTP if normal, WebSocket if upgrade" on the same route registration

The route model should stay explicit.

### Session Type

Add a Watson 7-native session type:
- `WebSocketSession`

Minimum surface:
- `Guid Id`
- `bool IsConnected`
- `string Subprotocol`
- `string RemoteIp`
- `int RemotePort`
- `NameValueCollection Headers` or equivalent reduced request metadata
- `RequestParameters Query` or equivalent
- `object Metadata`
- `Task SendTextAsync(string data, CancellationToken token = default)`
- `Task SendBinaryAsync(byte[] data, CancellationToken token = default)`
- `Task SendBinaryAsync(ArraySegment<byte> data, CancellationToken token = default)`
- `Task CloseAsync(WebSocketCloseStatus closeStatus, string reason, CancellationToken token = default)`

Required behavior:
- preserve GUID identity model from WatsonWebsocket
- preserve optional developer metadata bag
- preserve per-session send serialization

### Events

Events should be integrated into Watson 7 rather than exposed only through a separate websocket server class.

Add these callbacks to `WebserverEvents`:
- `HandleWebSocketConnecting`
- `HandleWebSocketConnected`
- `HandleWebSocketDisconnected`
- `HandleWebSocketMessageReceived`

These should complement route handlers, not replace them.

## Repository Layout

Implement the feature in Watson 7-native folders.

Add these folders:

- `src/WatsonWebserver.Core/WebSockets/`
- `src/WatsonWebserver/WebSockets/`
- `src/Test.Shared/WebSockets/`

Add these core files:

- `src/WatsonWebserver.Core/Settings/WebSocketSettings.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketConstants.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketOpcode.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketFrameHeader.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketFrameReader.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketFrameWriter.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketMessageAssembler.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketHandshakeUtilities.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketVersionPolicy.cs`
- `src/WatsonWebserver.Core/WebSockets/WebSocketSessionStatistics.cs`

Add these Watson implementation files:

- `src/WatsonWebserver/WebSockets/WebSocketSession.cs`
- `src/WatsonWebserver/WebSockets/WebSocketConnectionRegistry.cs`
- `src/WatsonWebserver/WebSockets/Http1WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/Http2WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/Http3WebSocketHandshake.cs`
- `src/WatsonWebserver/WebSockets/WebSocketRouteManager.cs`
- `src/WatsonWebserver/WebSockets/WebSocketRequestDescriptor.cs`
- `src/WatsonWebserver/WebSockets/Http2WebSocketStreamAdapter.cs`
- `src/WatsonWebserver/WebSockets/Http3WebSocketStreamAdapter.cs`

Modify these existing files:

- `src/WatsonWebserver.Core/WebserverSettings.cs`
- `src/WatsonWebserver.Core/WebserverBase.cs`
- `src/WatsonWebserver.Core/WebserverEvents.cs`
- `src/WatsonWebserver.Core/Routing/...`
- `src/WatsonWebserver/Webserver.cs`
- `src/WatsonWebserver/Http2/Http2ConnectionSession.cs`
- `src/WatsonWebserver/Http3/Http3ConnectionSession.cs`

## Implementation Phases

## Phase 1: Shared WebSocket Core

Build the protocol-independent pieces first.

### 1.1 Settings

- [ ] Add `WebSocketSettings` in `WatsonWebserver.Core.Settings`
- [ ] Add `Settings.WebSockets` to `WebserverSettings`
- [ ] Include these properties:
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
- [ ] Clamp values to reasonable ranges
- [ ] Default `SupportedVersions` to:
  - `13`
- [ ] Enforce version validation against the configured supported version list
- [ ] Document explicitly that only `13` is supported initially

Use these defaults:
- `Enable = false`
- `MaxMessageSize = 16777216`
- `ReceiveBufferSize = 65536`
- `CloseHandshakeTimeoutMs = 5000`
- `AllowClientSuppliedGuid = true`
- `ClientGuidHeaderName = "x-guid"`
- `SupportedVersions = ["13"]`
- `EnableHttp1 = true`
- `EnableHttp2 = true`
- `EnableHttp3 = true`

Notes:

### 1.2 Shared Session Model

- [ ] Add `WebSocketSession`
- [ ] Add session ID generation and optional client-supplied GUID override
- [ ] Add remote endpoint metadata
- [ ] Add developer metadata bag
- [ ] Add per-session send lock
- [ ] Add per-session cancellation token source
- [ ] Add bytes and messages sent and received counters
- [ ] Add deterministic disposal and close behavior

Implementation notes:
- model session ownership after WatsonWebsocket `ClientMetadata`, but do not carry `HttpListenerContext`
- store only Watson 7-native request metadata needed after handshake
- do not keep heavyweight request objects alive longer than needed

Notes:

### 1.3 Shared Framing

- [ ] Add frame header parsing
- [ ] Add frame writer
- [ ] Add masking and unmasking
- [ ] Add fragmentation handling
- [ ] Add message assembly to complete text and binary messages before callback
- [ ] Add close, ping, and pong support
- [ ] Enforce opcode and RSV validation
- [ ] Enforce message-size limits

Implementation notes:
- use pooled buffers
- keep receive buffers reusable
- return full messages to user code like WatsonWebsocket did
- separate low-level frames from public message delivery

Notes:

### 1.4 Shared Registry

- [ ] Add a connection and session registry keyed by `Guid`
- [ ] Add `ListSessions()` support
- [ ] Add `IsConnected(Guid)` support
- [ ] Add `Disconnect(Guid)` support
- [ ] Add safe cleanup on disconnect, exception, and shutdown

Notes:

## Phase 2: HTTP/1.1 Integration

This is the closest semantic match to the old WatsonWebsocket server.

### 2.1 Handshake Path

- [ ] Detect WebSocket upgrade requests in the HTTP/1.1 request path
- [ ] Validate:
  - `Connection: Upgrade`
  - `Upgrade: websocket`
  - `Sec-WebSocket-Version`
  - `Sec-WebSocket-Key`
- [ ] Validate that the version is in `Settings.WebSockets.SupportedVersions`
- [ ] Generate `Sec-WebSocket-Accept`
- [ ] Return `101 Switching Protocols`
- [ ] Hand the live connection stream to `WebSocketSession`

Implementation notes:
- hook into the HTTP/1.1 path after request parsing but before normal response handling
- once upgraded, the request must not flow through the ordinary HTTP route response pipeline
- the connection must no longer participate in HTTP/1.1 keep-alive request reuse

Notes:

### 2.2 Route Binding

- [ ] Add WebSocket route registration for HTTP/1.1 paths
- [ ] Reuse Watson 7 route matching rules for static and parameterized paths
- [ ] Throw if the same route is registered for both HTTP and WebSocket handling
- [ ] Make route registration fail early and clearly

Notes:

### 2.3 Session Run Loop

- [ ] Start a receive loop once the upgrade succeeds
- [ ] Raise connect, message, and disconnect notifications
- [ ] Call the registered route handler with `HttpContextBase` plus `WebSocketSession`
- [ ] Ensure server stop closes active HTTP/1.1 WebSocket sessions cleanly

Notes:

## Phase 3: HTTP/2 Integration

HTTP/2 must not reuse HTTP/1.1 upgrade logic. It should be implemented through the HTTP/2 session layer.

### 3.1 Extended CONNECT Handshake

- [ ] Detect RFC 8441 WebSocket establishment requests in `Http2ConnectionSession`
- [ ] Validate:
  - `:method = CONNECT`
  - `:protocol = websocket`
  - valid authority, path, and scheme rules
  - any required headers and subprotocol headers
- [ ] Reject classic `Upgrade: websocket` assumptions on HTTP/2
- [ ] Create a `WebSocketSession` bound to the HTTP/2 stream

Implementation notes:
- the stream remains multiplexed inside the HTTP/2 connection
- WebSocket session ownership is stream-based, not socket-based
- send and receive need to sit on top of HTTP/2 DATA frames rather than a raw TCP stream

Notes:

### 3.2 HTTP/2 Frame Bridging

- [ ] Add a stream adapter that lets `WebSocketSession` read and write payload bytes through HTTP/2 DATA frames
- [ ] Respect HTTP/2 flow control
- [ ] Ensure close and error handling map correctly to stream termination
- [ ] Ensure sibling streams continue functioning when a WebSocket stream closes or errors

Implementation notes:
- this adapter is the key abstraction for protocol sharing
- do not fork a second framing stack if the WebSocket core can operate against a byte-oriented duplex abstraction

Notes:

## Phase 4: HTTP/3 Integration

HTTP/3 should follow the same shape as HTTP/2 conceptually, but on top of QUIC stream and session semantics.

### 4.1 Extended CONNECT Handshake

- [ ] Detect HTTP/3 WebSocket establishment requests in `Http3ConnectionSession`
- [ ] Validate the HTTP/3-specific extended CONNECT pseudo-header set
- [ ] Reject classic upgrade assumptions on HTTP/3
- [ ] Create a `WebSocketSession` bound to the HTTP/3 request stream

Notes:

### 4.2 HTTP/3 Stream Bridging

- [ ] Add a stream adapter that lets `WebSocketSession` read and write payload bytes over HTTP/3 stream data
- [ ] Ensure clean interaction with QUIC stream abort and graceful close semantics
- [ ] Ensure sibling streams remain healthy
- [ ] Ensure cleanup runs on abrupt peer termination

Notes:

## Phase 5: Public API Wiring

### 5.1 `WebserverBase`

- [ ] Add public route registration methods for WebSockets
- [ ] Add session enumeration and lookup methods at server level
- [ ] Add disconnect helpers at server level

Suggested API:

```csharp
void WebSocket(string path, Func<HttpContextBase, WebSocketSession, Task> handler, bool auth = false);
IEnumerable<WebSocketSession> ListWebSocketSessions();
bool IsWebSocketSessionConnected(Guid guid);
Task DisconnectWebSocketSessionAsync(Guid guid, WebSocketCloseStatus status, string reason);
```

Notes:

### 5.2 Events

- [ ] Extend `WebserverEvents`
- [ ] Add WebSocket-specific lifecycle callbacks
- [ ] Ensure logger integration produces useful diagnostics for:
  - handshake success and failure
  - message receive and send failures
  - disconnect cleanup

Notes:

### 5.3 Statistics

- [ ] Add WebSocket stats to `WebserverStatistics` or add a WebSocket-specific stats block accessible from the server
- [ ] Include:
  - open sessions
  - total accepted sessions
  - total rejected handshakes
  - sent messages
  - received messages
  - sent bytes
  - received bytes

Notes:

## Phase 6: Compatibility Mapping

Map WatsonWebsocket 4.0 concepts onto Watson 7 deliberately.

- [ ] Map `ClientConnected` to the new connected event and handler lifecycle
- [ ] Map `ClientDisconnected` to the new disconnected event and handler lifecycle
- [ ] Map `MessageReceived` to Watson 7 message delivery
- [ ] Map `GuidHeader` behavior to `Settings.WebSockets.ClientGuidHeaderName`
- [ ] Map `ListClients()` to `ListWebSocketSessions()`
- [ ] Map `IsClientConnected(Guid)` to `IsWebSocketSessionConnected(Guid)`
- [ ] Map `DisconnectClient(Guid)` to Watson 7 disconnect API
- [ ] Replace `HttpHandler` with ordinary Watson HTTP routing rather than carrying forward a special raw HTTP fallback hook

Compatibility expectation:
- preserve behavior where it is useful
- do not preserve `HttpListenerRequest`-specific types
- convert usage guidance to Watson 7-native `HttpContextBase` and `WebSocketSession`

Notes:

## Phase 7: Testing

Everything meaningful should exist in both:
- `Test.Automated`
- `Test.XUnit`

Shared logic should live in:
- `Test.Shared`

### 7.1 Shared Infrastructure

- [ ] Add `Test.Shared/WebSockets/...`
- [ ] Add protocol-specific handshake helpers for HTTP/1.1, HTTP/2, and HTTP/3
- [ ] Add shared message helpers for text, binary, fragmentation, ping, pong, and close
- [ ] Add shared loopback server setup for WebSocket endpoints

Notes:

### 7.2 Positive Coverage

- [ ] HTTP/1.1 connection succeeds
- [ ] HTTP/2 connection succeeds
- [ ] HTTP/3 connection succeeds
- [ ] text send and receive on HTTP/1.1
- [ ] text send and receive on HTTP/2
- [ ] text send and receive on HTTP/3
- [ ] binary send and receive on HTTP/1.1
- [ ] binary send and receive on HTTP/2
- [ ] binary send and receive on HTTP/3
- [ ] fragmented message assembly on HTTP/1.1
- [ ] fragmented message assembly on HTTP/2
- [ ] fragmented message assembly on HTTP/3
- [ ] ping and pong on HTTP/1.1
- [ ] ping and pong on HTTP/2
- [ ] ping and pong on HTTP/3
- [ ] multiple concurrent sessions on HTTP/1.1
- [ ] multiple concurrent sessions on HTTP/2
- [ ] multiple concurrent sessions on HTTP/3
- [ ] session enumeration and disconnect APIs
- [ ] parameterized route handling on all three protocols
- [ ] auth-protected WebSocket routes on all three protocols

Notes:

### 7.3 Negative Coverage

- [ ] unsupported `Sec-WebSocket-Version` rejected
- [ ] missing `Sec-WebSocket-Key` rejected
- [ ] invalid HTTP/1.1 upgrade headers rejected
- [ ] invalid HTTP/2 extended CONNECT rejected
- [ ] invalid HTTP/3 extended CONNECT rejected
- [ ] invalid opcode rejected
- [ ] masking violation rejected
- [ ] oversized message rejected
- [ ] bad continuation sequence rejected
- [ ] abrupt disconnect cleanup verified
- [ ] route conflict registration throws

Notes:

### 7.4 Runner Presentation

- [ ] Ensure test names are protocol- and behavior-oriented
- [ ] Ensure no stale implementation-history labels are used

Notes:

## Phase 8: Benchmarking And Resource Review

### 8.1 Benchmark Implementation

- [ ] Extend `Test.Benchmark` with WebSocket scenarios
- [ ] Add Watson 7 WebSocket benchmark target
- [ ] Add WatsonWebsocket 4 benchmark target using `c:\code\watson\watsonwebsocket-4.0`
- [ ] Add Kestrel benchmark target
- [ ] Benchmark separately for HTTP/1.1, HTTP/2, and HTTP/3 where supported

Required output:
- connection establishment rate
- messages per second
- text throughput
- binary throughput
- latency where practical

Notes:

### 8.2 Resource And Leak Review

- [ ] Review session cleanup on success, failure, and exception paths
- [ ] Review send locks and cancellation handling
- [ ] Review large-message buffering and pooling
- [ ] Review sibling-stream behavior on HTTP/2 and HTTP/3
- [ ] Review non-WebSocket hot-path impact on HTTP/1.1

Notes:

## Phase 9: Documentation

### 9.1 README

- [ ] Add a WebSocket section to `README.md`
- [ ] Document protocol support across HTTP/1.1, HTTP/2, and HTTP/3
- [ ] Document route registration
- [ ] Document TLS behavior
- [ ] Add migration notes from WatsonWebsocket 4.0

Notes:

### 9.2 WebSocket API Guide

- [ ] Create `WEBSOCKETS_API.md`
- [ ] Document settings
- [ ] Document route registration
- [ ] Document `WebSocketSession`
- [ ] Document connection and message lifecycle
- [ ] Document send, receive, and close usage
- [ ] Document protocol differences
- [ ] Add copy/paste examples

Notes:

### 9.3 Testing Docs

- [ ] Update `TESTING.md`
- [ ] Document how to run WebSocket smoke tests
- [ ] Document how to run WebSocket xUnit tests
- [ ] Document how to run WebSocket benchmarks

Notes:

### 9.4 Changelog

- [ ] Add a changelog entry when the feature lands

Notes:

## Deliverables

- [ ] `WebSocketSettings`
- [ ] `WebSocketSession`
- [ ] HTTP/1.1 WebSocket support
- [ ] HTTP/2 WebSocket support
- [ ] HTTP/3 WebSocket support
- [ ] Watson 7 route registration for WebSockets
- [ ] server-level session management APIs
- [ ] shared tests in `Test.Automated` and `Test.XUnit`
- [ ] `README.md` updates
- [ ] `WEBSOCKETS_API.md`
- [ ] `TESTING.md` updates
- [ ] `Test.Benchmark` WebSocket coverage

## Progress Summary

- Design complete: [ ]
- Shared core complete: [ ]
- HTTP/1.1 complete: [ ]
- HTTP/2 complete: [ ]
- HTTP/3 complete: [ ]
- Public API complete: [ ]
- Tests complete: [ ]
- Benchmarks complete: [ ]
- Documentation complete: [ ]
- Ready for release review: [ ]
