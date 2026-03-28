# HTTP/1.1, HTTP/2, and HTTP/3 Unified Native Implementation Plan for WatsonWebserver 7.x

This document replaces earlier drafts. It reflects the revised direction established after codebase review and technical debate.

The target is no longer an additive 6.x sidecar rollout. The target is WatsonWebserver 7.x as a unified multi-protocol server with one public server instance and one logical endpoint that serves HTTP/1.1, HTTP/2, and HTTP/3.

## Objectives

1. Deliver native support for HTTP/1.1, HTTP/2, and HTTP/3 in WatsonWebserver 7.x.
2. Present one public server API and one logical endpoint to users.
3. Share routing and request/response semantics where that is honest.
4. Keep protocol framing, transport, and flow-control mechanics protocol-specific.
5. Preserve Watson's low-level control philosophy for HTTP/1.1.
6. Expand the existing console-app testing style into exhaustive protocol coverage.

## Status Legend

- [ ] Not started
- [~] In progress
- [x] Complete
- [!] Needs decision

## Repository Annotation (2026-03-27)

This file is now primarily historical. The repository already contains a large portion of the implementation described here.

Completed or clearly present in the repository:

- [x] Watson 7 targets `net8.0` and `net10.0`
- [x] Watson 7 uses direct transport ownership rather than `HttpListener` / `http.sys`
- [x] A unified server surface exists with protocol-specific internals
- [x] HTTP/1.1 implementation exists on raw TCP/TLS paths
- [x] HTTP/2 implementation exists, including connection preface, settings negotiation, frame parsing/writing, stream state management, GOAWAY handling, and a serialized connection writer
- [x] HTTP/3 implementation exists, including QUIC accept loop, control stream handling, request/response mapping, GOAWAY handling, runtime availability detection, and graceful runtime normalization when QUIC is unavailable
- [x] `WebserverSettings.Protocols`, `AltSvc`, and startup validation exist
- [x] Alt-Svc is explicit configuration and is not emitted by default
- [x] Shared semantics live in `WatsonWebserver.Core`
- [x] `Test.Automated`, `Test.XUnit`, and `Test.Benchmark` exist
- [x] Cross-target benchmarking against Watson 6, WatsonLite6, Watson 7, and Kestrel exists in `Test.Benchmark`

Still best read as open, incomplete, or only partially evidenced by the repository:

- [~] Formal closeout of every acceptance criterion inside this document
- [~] Explicit proof in-repo that every benchmark and protocol test was run on both Windows and Linux
- [~] Exhaustive standards-compliance signoff for every HPACK/QPACK adversarial case beyond the implemented automated coverage

## Target Framework Strategy

WatsonWebserver 7.x targets `net8.0` and `net10.0` only. Support for .NET Framework (`net462`, `net48`), `netstandard2.0`, and `netstandard2.1` is dropped. Legacy runtime users remain on Watson 6.x.

All protocol capabilities are available on all supported TFMs:

| Capability | net8.0 | net10.0 |
|---|---|---|
| HTTP/1.1 | Yes | Yes |
| HTTP/2 | Yes | Yes |
| HTTP/3 (QUIC) | Yes* | Yes* |

\* HTTP/3 availability depends on `System.Net.Quic` and platform support (`msquic` on Windows, `libmsquic` on Linux). The server must detect QUIC availability at startup and degrade gracefully if unavailable, logging a clear warning.

This eliminates conditional compilation for protocol features, TFM-based capability matrices, and the "single interface, different capabilities" ambiguity. Every supported target gets the full protocol stack. The only runtime check is QUIC/platform availability for HTTP/3.

## Final Product Position

### 1. One public server, one logical endpoint

WatsonWebserver 7.x should expose one primary server type and one configuration model.

Users should configure one endpoint and one routing/auth/session/event pipeline.

Internally, "one endpoint" means:

- TCP listener on the configured port for HTTP/1.1 and HTTP/2
- UDP/QUIC listener on the same port number for HTTP/3

This is one product surface, not three separate products.

Any dedicated cleartext `h2c` listener should be treated as optional development or interoperability support, not as part of the primary "one logical endpoint" product model.

WatsonWebserver 7.x should also be one primary library/runtime implementation, not a continued split between `WatsonWebserver` and `WatsonWebserver.Lite` as peer server products.

If package continuity matters, Lite can survive only as a thin compatibility facade during migration. It should not remain a separate transport architecture in 7.x.

### 2. Do not build on `HttpListener`

`HttpListener` is not the foundation for native HTTP/2 and HTTP/3.

WatsonWebserver 7.x should own transport directly:

- HTTP/1.1 and HTTP/2: `TcpListener` plus optional `SslStream`
- HTTP/3: `System.Net.Quic`

The current `HttpListener`-based server remains part of older lines, but it is not the architectural base for 7.x.

### 3. Do not treat Lite or CavemanTcp as the foundation

Lite is useful for understanding direct transport ownership and existing behavior, but its current one-request-per-client lifecycle is not a valid base for multiplexed protocols.

CavemanTcp should not define the architecture for 7.x HTTP/2 or HTTP/3.

Direct use of `System.Net.*` is the correct transport posture.

### 4. Performance position: validate, do not assume

Moving off `HttpListener` and `http.sys` is an intentional control and portability trade, not a claim that user-space transport automatically wins on every workload.

WatsonWebserver 7.x should justify direct transport ownership by being:

- cross-platform
- protocol-complete for HTTP/1.1, HTTP/2, and HTTP/3 where supported
- low-level and predictable
- fast enough under realistic Watson workloads, proven by benchmarks

Performance work should be explicit:

- use pooled buffers and minimize avoidable allocations
- keep parsing incremental and span-based where practical
- avoid unnecessary body buffering and response-copying
- preserve concurrency for HTTP/2 and HTTP/3 where the protocol allows it
- benchmark against current Watson, current Lite, and a simple Kestrel baseline
- measure throughput, latency, allocations, handshake cost, streaming behavior, and concurrent-stream behavior

### 5. Share only the semantic layer

The shared layer belongs in `WatsonWebserver.Core` and should cover:

- routing and route matching
- auth and session hooks
- request/response/context shape
- protocol/version metadata
- stream/connection metadata
- trailers where valid
- lifecycle signals such as response started, response completed, and request/stream aborted

Do not create shared abstractions for:

- frame encoding or decoding
- connection writers
- transport flow-control windows
- protocol-specific transport state

Those differ too much between HTTP/1.1, HTTP/2, and HTTP/3.

### 6. Preserve explicit HTTP/1.1 wire control

Watson has historically exposed low-level HTTP/1.1 behavior intentionally. That should remain true in 7.x.

Do not deprecate or remove HTTP/1.1-specific controls simply because they do not map to HTTP/2 or HTTP/3.

Specifically:

- preserve `ChunkedTransfer`
- preserve `SendChunk()`
- preserve explicit control over `Content-Length`, keepalive, and chunked behavior where those semantics are valid

On HTTP/2 and HTTP/3, HTTP/1.1-only settings should be rejected or deterministically ignored with clear documentation.

### 7. Unified public surface, protocol-specific internals

The user-facing API should remain unified as much as practical.

Below that line, each protocol keeps its own implementation strategy:

- HTTP/1.1: parser, keepalive handling, chunked transfer, low-level wire control
- HTTP/2: serialized connection writer, stream state machine, HPACK, explicit flow control
- HTTP/3: per-request `QuicStream` writers, control stream, QPACK streams, transport-driven backpressure

No shared abstraction should force HTTP/2's connection-serialization rules onto HTTP/3.

### 8. Structurally async and bounded internal architecture

Every outbound body path must be async, cancellation-aware, and bounded by per-connection or per-stream buffering rules from day one. This is a structural invariant, not a deferred optimization.

Requirements:

- `ValueTask`-based writes on all protocol paths
- Explicit flush points — no implicit unbounded buffering
- No sync-over-async fallback anywhere in the write pipeline
- No unbounded intermediate queues in the shared layer

This structural design gives each protocol room to express backpressure through its native mechanism — TCP send buffer saturation for HTTP/1.1, WINDOW_UPDATE for HTTP/2, QUIC stream credit for HTTP/3 — without requiring a premature shared backpressure abstraction.

The public API remains simple: writes are awaitable, cancellable, and fail deterministically on abort. Richer flow-control exposure is deferred until proven necessary.

### 9. Connection lifecycle management

HTTP/2 multiplexes streams over one TCP connection. HTTP/3 multiplexes streams over one QUIC connection. HTTP/1.1 has keepalive with optional pipelining. Connection lifecycle management must be explicitly designed, not emergent.

Requirements:

- Idle connection timeout with configurable duration
- Maximum concurrent streams per connection (HTTP/2 SETTINGS, HTTP/3 transport parameter)
- Graceful connection drain: stop accepting new streams, allow in-flight streams to complete, then close
- Connection-level error recovery: distinguish stream errors (reset one stream) from connection errors (tear down the connection)
- Connection metadata tracking for the explicit metrics surface (`ActiveConnectionCount`, etc.)

This is where most HTTP/2 server bugs live. Each protocol handles connection lifecycle differently, so this is protocol-specific implementation, not shared abstraction.

### 10. Fail-fast configuration validation

The server must validate the full protocol/TLS/platform/runtime matrix during construction or `Start()` and throw actionable errors for incoherent configurations. Configuration problems must never surface as runtime failures on first request.

Examples of required validation:

- "HTTP/2 is enabled without TLS. Cleartext HTTP/2 (h2c) via prior knowledge is not supported in this configuration. Either enable TLS or disable HTTP/2."
- "HTTP/3 is enabled but System.Net.Quic is not available on this platform. Disable HTTP/3 or ensure the QUIC runtime is available."
- "HTTP/2 is enabled but TLS certificate configuration is missing. HTTP/2 requires TLS with ALPN negotiation."
- "Alt-Svc emission is enabled but HTTP/3 is disabled. This will advertise a protocol the server cannot serve."

### 11. Alt-Svc as explicit configuration

Clients discover HTTP/3 availability via the `Alt-Svc` HTTP header sent over HTTP/1.1 or HTTP/2 responses. However, automatic Alt-Svc emission is operationally dangerous — reverse proxies, NAT, split TCP/UDP firewall rules, and container port mapping can all cause the advertised QUIC endpoint to be unreachable.

Alt-Svc is a configuration surface with safe-off defaults:

- Never auto-emit Alt-Svc by default
- Provide explicit configuration: `server.Settings.AltSvc.Enabled = true` with configurable authority and port
- Optionally: startup self-check that attempts a QUIC listener bind and warns if it fails, but still does not auto-advertise

## Delivery Strategy

Implementation happens incrementally, but the product architecture is unified from the start.

### Phase 0: Semantic specification and shared core extraction

Phase 0 produces two outputs: a normative semantic specification and the extracted shared Core layer.

Repository note: the normative semantic contract is now captured in [PHASE0_SEMANTIC_SPEC.md](PHASE0_SEMANTIC_SPEC.md).

The semantic specification resolves observable behavioral divergences between Watson (`http.sys`) and Watson.Lite (CavemanTcp) before any transport work begins. The current products disagree in ways that affect user code:

- `http.sys` silently de-chunks and presents a unified body; Lite surfaces `ChunkedTransfer == true` with `ContentLength == 0`
- `http.sys` rejects certain malformed headers at the kernel level before Watson ever sees them; Lite passes them through
- Abort/disconnect timing differs because `http.sys` owns the connection and signals differently than a raw TCP read failure
- Counter and metric semantics (`RequestCount`, etc.) are inconsistent across the existing codebase

"Behavior parity" cannot mean "preserve both." The specification must make normative choices.

#### Semantic specification deliverables

- [x] Request body exposure contract: how chunked bodies are presented, what `ContentLength` means when chunked encoding is in play, whether the server de-chunks or exposes raw transfer encoding
- [x] Malformed header policy: which invalid headers are rejected vs. passed through, and at what layer
- [x] Disconnect and abort timing contract: how and when the application is notified of client disconnect
- [x] Trailer semantics: when trailers are surfaced, on which protocols, and what the application can rely on
- [x] Metrics contract: what `RequestCount`, `ActiveConnectionCount`, and stream-level metrics mean precisely
- [x] HTTP/1.1-only behavior on HTTP/2 and HTTP/3: explicit rejection vs. silent ignore policy for each feature

#### Core extraction deliverables

- [x] Extract the route execution pipeline into Core
- [x] Extract shared auth/session/event sequencing into Core
- [x] Normalize request/response/context lifecycle behavior per the semantic spec
- [x] Add protocol/version metadata
- [x] Add connection/stream metadata
- [x] Add explicit lifecycle signals for response start, response completion, and aborts
- [~] Define the internal writer contract: `ValueTask`-based, cancellation-aware, bounded buffering, no sync-over-async
- [x] Define the configuration validation contract for fail-fast startup checks

### Phase 1: HTTP/1.1 on raw TCP (the hardest phase)

This is the highest-risk phase. Moving from `HttpListener` (which handles parsing, keepalive, chunked decoding, and connection management) to raw `TcpListener` means owning every byte. Watson.Lite's CavemanTcp experience is informative but its architecture is not carried forward.

Phase 1 is broken into explicit sub-phases with distinct acceptance criteria.

#### Phase 1a: HTTP/1.1 request parser

- [x] Incremental, span-based HTTP/1.1 request line and header parser
- [x] Chunked transfer-encoding decoder
- [x] Content-Length body reader
- [x] Malformed input handling per the Phase 0 semantic spec
- [x] Parser fuzzing with malformed, truncated, and adversarial inputs (obs-fold headers, chunk extensions, request smuggling vectors)

#### Phase 1b: Connection manager

- [x] Persistent connection (keepalive) handling with configurable idle timeout
- [x] Connection tracking for metrics (`ActiveHttp1ConnectionCount`)
- [x] Graceful connection drain on server shutdown
- [x] Disconnect detection and abort signaling per the Phase 0 semantic spec

#### Phase 1c: Response writer

- [x] Response writer with explicit Content-Length, chunked, and connection-close semantics
- [x] `SendChunk()` support preserving existing Watson behavior
- [~] `ValueTask`-based async writes with cancellation support
- [~] Bounded output buffering -- no unbounded intermediate queues

#### Phase 1d: TLS integration

- [x] TLS via `SslStream` with configurable certificate
- [x] ALPN negotiation support (preparing for HTTP/2 in Phase 2)
- [x] TLS configuration validation at startup

#### Phase 1e: Parity validation

- [x] Behavior parity tests against the Phase 0 semantic spec (not against either existing product's quirks)
- [x] Existing console-app test suite passes against the new foundation
- [~] Ship as alpha for early feedback before proceeding to Phase 2

### Phase 2: HTTP/2 implementation

Repository note: the implemented HTTP/2 milestone and acceptance coverage are summarized in [PHASE2_HTTP2_CLOSEOUT.md](PHASE2_HTTP2_CLOSEOUT.md).

- [x] Implement HTTP/2 connection preface and settings negotiation
- [x] Implement frame parsing and frame writing
- [x] Implement serialized connection writer
- [x] Implement stream state management
- [x] Implement HPACK using existing battle-tested code (e.g., port from Kestrel's `System.Net.Http.HPack` or a standalone library -- do not write from scratch)
- [x] Implement per-stream and connection-level flow control
- [x] Implement connection lifecycle: idle timeout, max concurrent streams negotiation, graceful drain
- [x] Implement GOAWAY behavior
- [x] Implement Alt-Svc header emission when HTTP/3 is also enabled (off by default, explicit configuration required)
- [x] Validate HTTP/2 route handling against the Phase 0 semantic spec
- [x] Verify HTTP/2 code paths build and pass on both `net8.0` and `net10.0`

### Phase 3: HTTP/3 implementation

- [x] Runtime capability detection for `System.Net.Quic` availability at startup
- [x] Graceful degradation: server starts without HTTP/3 if QUIC is unavailable, logs a clear warning
- [x] Implement QUIC accept loop and stream handling
- [x] Implement HTTP/3 control stream handling
- [x] Implement QPACK static-table-plus-literals mode using existing battle-tested code where available (do not write from scratch)
- [x] Implement request/response mapping to QUIC streams
- [x] Implement connection lifecycle: idle timeout, max concurrent streams, graceful drain
- [x] Implement abort, cancellation, and backpressure behavior
- [x] Implement graceful shutdown behavior
- [x] Alt-Svc coordination with HTTP/2 responses (respecting explicit configuration)
- [x] Validate HTTP/3 route handling against the Phase 0 semantic spec
- [x] Verify HTTP/3 code paths build and pass on both `net8.0` and `net10.0`
- [x] Document platform requirements: Windows (msquic ships with recent versions), Linux (requires `libmsquic` package)

### Phase 4: Unified validation

- [~] All acceptance criteria (below) pass
- [~] Full cross-protocol behavior parity validated against the Phase 0 semantic spec
- [x] Configuration validation covers all protocol/TLS/platform combinations
- [x] Alt-Svc-based HTTP/3 discovery works end-to-end with curl and a browser (when explicitly configured)

## Testing Strategy

The existing console-app harness remains valuable, but it is not sufficient by itself.

The current baseline is 103 `ExecuteTest(...)` call sites, not 158.

### 1. Keep and expand the console-app harness

- [x] Preserve the existing console test applications
- [x] Add unified-server coverage for HTTP/1.1, HTTP/2, and HTTP/3
- [x] Add side-by-side behavior parity tests across protocols where semantics should match
- [x] Add explicit tests for HTTP/1.1-only features and expected rejection/ignore behavior on HTTP/2 and HTTP/3

### 2. Add protocol-level test harnesses

HTTP/1.1 tests:

- [x] keepalive behavior
- [x] chunked transfer behavior
- [x] large body streaming
- [x] malformed header and parser edge cases
- [x] request smuggling vector resistance
- [x] obs-fold header handling
- [x] chunk extension handling

HTTP/2 tests:

- [x] frame parser coverage
- [x] SETTINGS negotiation
- [x] HEADERS and CONTINUATION handling
- [x] DATA flow-control behavior
- [x] WINDOW_UPDATE behavior
- [x] GOAWAY behavior
- [x] RST_STREAM behavior
- [x] deterministic multiplexing on one connection
- [x] writer serialization correctness
- [x] sibling stream survival during cancellation or reset
- [x] connection lifecycle: idle timeout, max concurrent streams, graceful drain
- [x] HPACK encoder/decoder correctness including edge cases and size limits (HPACK bomb resistance)

HTTP/3 tests:

- [x] QUIC stream concurrency
- [x] control stream handling
- [x] QPACK static table plus literals
- [x] cancellation and abort behavior
- [x] transport backpressure behavior
- [x] sibling stream survival
- [x] connection lifecycle: idle timeout, max concurrent streams, graceful drain
- [x] QUIC unavailability graceful degradation

Cross-protocol behavior tests:

- [x] route matching parity
- [x] auth/session/event parity
- [x] trailer behavior where valid
- [x] long-lived SSE and delayed streaming behavior
- [x] large uploads and downloads
- [x] connection shutdown semantics

### 3. Negative and interoperability testing

- [x] malformed requests
- [x] invalid frame sequences
- [x] invalid HPACK/QPACK inputs
- [x] protocol downgrade and negotiation failures
- [x] mixed-version client interoperability checks
- [x] configuration validation: verify all fail-fast checks produce correct errors

### 4. Benchmark and workload validation

- [x] benchmark HTTP/1.1, HTTP/2, and HTTP/3 throughput
- [x] benchmark p50/p95/p99 latency under realistic concurrency
- [x] track allocations per request/stream
- [x] measure TLS and QUIC handshake cost
- [x] measure long-lived streaming and SSE behavior
- [x] compare against current Watson, current Lite, and a simple Kestrel baseline
- [~] run benchmark coverage on both Windows and Linux

## Compatibility and Breaking Changes

This is a 7.x effort, so some migration is acceptable. The goal is still to minimize unnecessary breaks.

Breaking changes that are justified:

- moving the primary 7.x server architecture off `HttpListener`
- retiring Lite as a separate peer server architecture in favor of one unified 7.x implementation
- replacing ambiguous runtime semantics with explicit metrics and metadata
- dropping .NET Framework (`net462`, `net48`), `netstandard2.0`, and `netstandard2.1` target support — Watson 7.x requires `net8.0`+
- resolving behavioral divergences between Watson and Lite per the Phase 0 semantic spec (some users of either product will see behavior changes)

Behavior that should be preserved where possible:

- existing routing model
- existing auth/session/event model
- existing low-level HTTP/1.1 controls
- existing console-app testing style

Where semantics differ by protocol, document them explicitly instead of hiding them behind misleading abstractions.

## Non-Goals for Initial Delivery

- HTTP/2 server push
- HTTP/3 server push
- Upgrade-based `h2c`
- Mixed cleartext HTTP/1.1 plus `h2c` sniffing on one port
- Early QPACK dynamic-table implementation
- Fake shared transport abstractions that blur real protocol differences
- Assuming performance leadership without benchmark evidence
- WebSocket-over-HTTP/2 (RFC 8441)
- CONNECT method and extended CONNECT support
- Public backpressure abstraction (internal flow-control is structurally required; public API exposure is deferred until proven necessary)
- Automatic Alt-Svc emission (supported as explicit configuration only)

## Recommended Acceptance Criteria

The 7.x unified server is ready only when all of the following are true:

1. One public server instance can be configured for one logical endpoint.
2. HTTP/1.1 and HTTP/2 operate on the TCP/TLS side of that endpoint.
3. HTTP/3 operates on QUIC using the same port number.
4. The routing/auth/session/event pipeline is shared successfully across protocols.
5. HTTP/1.1-specific wire controls remain available and correct.
6. HTTP/2 uses a correct serialized connection writer and passes multiplexing and flow-control tests.
7. HTTP/3 uses per-stream body writes without artificial connection-level serialization and passes transport/backpressure tests.
8. Existing console-style tests are preserved and materially expanded.
9. Protocol-level tests are exhaustive enough to catch frame, stream, cancellation, and shutdown errors.
10. Benchmark and workload data show the unified 7.x server is operationally credible on Windows and Linux.
11. Configuration validation catches all incoherent protocol/TLS/platform combinations at startup with actionable error messages.
12. Alt-Svc-based HTTP/3 discovery works end-to-end with curl and a browser when explicitly configured.
13. HPACK and QPACK implementations pass specification compliance tests including adversarial inputs.
14. Server starts cleanly on platforms without QUIC support when HTTP/3 is not configured, and degrades gracefully with a warning when HTTP/3 is configured but QUIC is unavailable.
15. Behavior across all protocols conforms to the Phase 0 semantic specification — no undocumented divergences.

## Summary

WatsonWebserver 7.x should be designed as one unified multi-protocol server, not as a collection of loosely related protocol packages.

It should also be one unified server product, not a continued Watson-versus-Lite split.

The correct shared boundary is the application semantic layer.
The correct non-shared boundary is transport, framing, and protocol control.

HTTP/1.1 remains first-class and keeps its explicit low-level controls.
HTTP/2 and HTTP/3 are implemented natively with protocol-specific machinery beneath one public server surface.

The foundation for all transport work is the Phase 0 semantic specification, which resolves existing behavioral divergences and establishes normative contracts before any parser or protocol code is written.
