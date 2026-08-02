# Telemetry and Instrumentation Plan

Watson 7 ships a rich event and statistics surface today — `WebserverEvents`, `WebserverStatistics`,
`WebSocketSessionStatistics` — but nothing a standards-based collector can read. A host that wants
request rates, latency percentiles, or a distributed trace has to hand-roll adapters against C#
`event` handlers. This plan closes that gap by emitting first-class OpenTelemetry-shaped metrics and
traces straight from the pipeline, so [Radiant](https://github.com/jchristn/Radiant), a raw
OpenTelemetry SDK, Prometheus, or any other listener can subscribe by name and get dashboard-ready
data with no glue code.

The work is scoped as a **minor release: `7.0.15` → `7.1.0`**. It is additive. No existing event,
statistic, or public signature changes; the new surface sits alongside what is already there.

---

## Implementation status (shipped in 7.1.0)

Most of this plan is implemented and merged. The parts that touch deep protocol framing are
deliberately deferred and called out so nothing reads as done that isn't.

**Shipped.** The `Watson` meter and activity source; the four OpenTelemetry HTTP metrics; the
`watson.*` metrics for connections, route match/unmatched (by type and template), auth outcomes,
aborts, disconnects, server exceptions, received/sent bytes, uptime/up, and active HTTP/2 and HTTP/3
streams; WebSocket session and handshake metrics; the per-request `Server` span with W3C context
propagation, forwarded-header client-address resolution, and bidirectional body-size/content-type
attributes; `Settings.Telemetry` (`TelemetrySettings`) and `WebserverBase.Telemetry`; the optional
in-process Prometheus endpoint served on the main listener; `MeterListener`/`ActivityListener` tests
wired into both runners; and every version artifact (csproj, CHANGELOG, README, WEBSERVER_SETTINGS,
CLAUDE.md).

The connection, stream, byte, and WebSocket metrics are wired to the existing `WebserverEvents` and
`WebserverStatistics` surface from inside `WebserverTelemetry` — event subscriptions and observable
gauges over the live statistics — so the transport hot paths were not modified.

**Deferred (still `- [ ]` below).** Per-frame HTTP/2 counters (frames by type, GOAWAY, RST_STREAM,
flow-control wait) and HTTP/3 GOAWAY/unavailable counters; the middleware and serialization duration
histograms; the dedicated `watson.api.*` error/timeout counters (API-route errors still surface via
the duration histogram's status code); and the opt-in detailed and connection spans. Active HTTP/2 and
HTTP/3 stream counts *did* ship, sampled as gauges over the existing statistics counters.

---

## Guiding principle: your server emits, the host collects

The design follows the same split Radiant's `INTEGRATION.md` insists on. **Watson emits; it never
collects.** Emitting a measurement is a base-class-library operation — you create a `Meter` and an
`ActivitySource` and record into them. That costs nothing and throws nothing when no one is listening.
The host is the listener; it subscribes to Watson's meter and activity-source *by name* and owns the
export pipeline, the sampling ratio, and the Prometheus endpoint. Watson takes **no dependency on
Radiant or on the OpenTelemetry SDK** — only on `System.Diagnostics.DiagnosticSource`, which is in-box
for `net8.0`/`net10.0` and a single small package for `netstandard2.1`.

Three consequences fall out of that stance, and they shape every decision below:

- **Vendor neutrality is free.** Because the emit path is pure BCL, the same instrumentation feeds
  Radiant, the OpenTelemetry Collector, Azure Monitor, Prometheus, or a unit test's `MeterListener`.
  Watson does not pick a winner.
- **Unobserved cost is near zero.** An `Add`/`Record` with no subscriber is an `Enabled` check and an
  early return — low single-digit nanoseconds, no heap allocation when tags ride a `TagList` struct. A
  `StartActivity` on an unsampled source returns `null` in a few nanoseconds. Watson can leave
  instrumentation on by default without taxing users who never turn a collector on.
- **The names are a public contract.** The meter name, the activity-source name, every instrument
  name, and every attribute key are strings a consumer hard-codes. They are treated like public API:
  stable across patch and minor releases, changed only with a major bump and a changelog note.

---

## The naming contract

Everything a telemetry consumer needs is a string. The two that matter most are the source names —
these are what a host passes to `AddMeter` / `AddActivitySource`:

| Contract | Value | Configurable via |
|---|---|---|
| Meter name | `Watson` | `Settings.Telemetry.MeterName` |
| Meter version | assembly informational version (e.g. `7.1.0`) | derived |
| ActivitySource name | `Watson` | `Settings.Telemetry.ActivitySourceName` |

`Watson` matches the NuGet package id and parallels the repo's naming (`Watson` is the server,
`Watson.Clients` the client package), so the source name a host subscribes to is the name it already
installed. Two things follow from choosing a bare word over a dotted namespace. It surfaces as
`otel_scope_name="Watson"` on every series the OpenTelemetry Prometheus exporter emits, so document
that as the scope filter value. And a wildcard subscription of the form `AddMeter("Watson.*")` will
**not** match a bare `Watson` — hosts subscribe with the exact string `"Watson"`. Both names are
overridable for the rare consumer who runs several servers in one process and wants to disambiguate,
but the default is what documentation and dashboards assume.

### Metric instrument names

The four core HTTP-server metrics use the **OpenTelemetry HTTP semantic-convention names verbatim**,
so they land on a stock Grafana panel and match Radiant's `SemConv.Http.*` conventions exactly.
Everything Watson measures that the HTTP semconv does not cover — connections, routing decisions,
auth outcomes, HTTP/2 frames, HTTP/3 lifecycle, WebSockets, internal backpressure — lives under a
`watson.*` namespace so it is unmistakably ours and never collides with a well-known name.

### Attribute (label) keys

| Key | Values (examples) | Used on |
|---|---|---|
| `http.request.method` | `GET`, `POST`, `PUT`, `DELETE` | HTTP metrics + server span |
| `http.response.status_code` | `200`, `404`, `500` (int) | HTTP metrics + server span |
| `http.route` | route **template**, e.g. `/users/{id}` | HTTP metrics + server span |
| `url.scheme` | `http`, `https` | active-requests + server span |
| `network.protocol.version` | `1.1`, `2`, `3` | `watson.*` instruments + server span |
| `network.protocol.name` | `http` | server span |
| `error.type` | exception type name or status class | opt-in on request duration; server span |
| `watson.route.type` | `Static`, `Content`, `Parameter`, `Dynamic` | routing instruments |
| `watson.direction` | `inbound`, `outbound`, `sent`, `received` | frames, bytes, messages |
| `watson.auth.mode` | `api`, `legacy` | auth instrument |
| `watson.auth.authn` | `Success`, `NotFound`, `Failure` (from `AuthenticationResultEnum`) | auth instrument |
| `watson.auth.authz` | `Permitted`, `DeniedImplicit`, `DeniedExplicit` (from `AuthorizationResultEnum`) | auth instrument |
| `watson.result` | `ApiResultEnum` name for API errors | API exception instrument |
| `http2.frame.type` | `Data`, `Headers`, `Settings`, `Ping`, `WindowUpdate`, … | HTTP/2 frame counter |
| `http2.error.code` | HTTP/2 error code name | GOAWAY / RST_STREAM counters |
| `watson.reason` | short cause string | denied / rejected / degraded counters |

### The one rule that keeps this affordable

`http.route` is the **route template, never the raw request path.** `/users/{id}` is one time series;
`/users/8a3f…`, `/users/9c21…`, `/users/…` is one series per user until the scrape endpoint falls
over. This is the single discipline that separates a healthy metrics pipeline from an outage, and it
is enforced here by construction:

- **Static** routes report their registered path.
- **Parameter** routes report the template with `{name}` placeholders intact.
- **Content** routes report the registered base path, not the resolved file.
- **Dynamic** routes report the registered regex pattern string (a bounded set the developer wrote),
  not the URL that matched it.
- **Unmatched** requests carry no `http.route` label at all and increment a dedicated
  `watson.route.unmatched` counter instead.

High-cardinality identifiers — the actual path, the query string, the client IP, the request GUID —
belong on **spans** and in **logs**, where one row per request is the point. A trace carries the id
from span to log; a metric never should. `HttpContextBase` already exposes exactly what each layer
needs: `ctx.Route` and `ctx.RouteType` for the bounded metric label, `ctx.Guid` and the raw request
data for the unbounded span attributes.

---

## Metric catalog

Kinds map directly to `System.Diagnostics.Metrics` instrument types. The Prometheus column shows the
name a scrape sees **after** the OpenTelemetry Prometheus exporter applies its `_total`/unit suffix
rules — Watson keeps the clean dotted name; the exporter derives the rest. Histogram buckets are a
**consumer concern**: Watson does not set bucket boundaries (the netstandard2.1 target predates the
`InstrumentAdvice` API anyway), so a host picks them with an OpenTelemetry view or Radiant's
`LatencyBuckets.Default` / `.Fast` presets.

### Core HTTP server metrics (OpenTelemetry semantic conventions)

These four match Radiant's `SemConv.Http.*` conventions **name-for-name and label-for-label**, so a
host can register the Radiant catalog in Strict mode and every measurement is accepted. That
compatibility is why the extra dimensions Watson knows (protocol version, scheme on duration) are
deliberately *not* stamped here — they would be rejected by a strict catalog. They live on the
`watson.*` instruments and the span instead.

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `http.server.request.duration` | Histogram | `s` | `http.request.method`, `http.response.status_code`, `http.route` | `http_server_request_duration_seconds` | `WebserverBase.ProcessHttpContextAsync` finally, from `ctx.Timestamp.TotalMs` (`WebserverBase.cs:881`) |
| `http.server.active_requests` | UpDownCounter | `{request}` | `http.request.method`, `url.scheme` | `http_server_active_requests` | +1 at request start (`WebserverBase.cs:682`), −1 in finally (`:908`) |
| `http.server.request.body.size` | Histogram | `By` | `http.request.method` | `http_server_request_body_size_bytes` | from `ctx.Request.ContentLength` / `IncrementReceivedPayloadBytes` (`:684`) |
| `http.server.response.body.size` | Histogram | `By` | `http.request.method`, `http.response.status_code` | `http_server_response_body_size_bytes` | from `IncrementSentPayloadBytes` (`:895`) |

### Connection and server lifecycle (`watson.server.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.server.connections.active` | UpDownCounter | `{connection}` | `network.protocol.version` | `watson_server_connections_active` | `IncrementActiveConnectionCount` / `Decrement` — TCP `Webserver.cs:769`, QUIC `:619`, teardown `:892` |
| `watson.server.connections.total` | Counter | `{connection}` | `network.protocol.version` | `watson_server_connections_total` | connection accepted (`Webserver.cs:769` / `:619`) |
| `watson.server.connections.denied` | Counter | `{connection}` | `network.protocol.version`, `watson.reason` | `watson_server_connections_denied_total` | access-control / `Events.ConnectionDenied` |
| `watson.server.connections.rejected` | Counter | `{connection}` | `watson.reason` | `watson_server_connections_rejected_total` | slot/backpressure refusal (`TryAcquireRequestSlotAsync`, `Webserver.cs:749`) |
| `watson.server.requests.inflight` | UpDownCounter | `{request}` | — | `watson_server_requests_inflight` | `_RequestCount` inc/dec (`Webserver.cs:806` / `:873`) |
| `watson.server.requests.aborted` | Counter | `{request}` | `watson.reason` (`aborted` / `disconnected`) | `watson_server_requests_aborted_total` | `MarkRequestTerminated` (`WebserverBase.cs:1177`) → `RequestAborted` / `RequestorDisconnected` |
| `watson.server.exceptions` | Counter | `{exception}` | `error.type`, `network.protocol.version` | `watson_server_exceptions_total` | every `Events.HandleExceptionEncountered` (`WebserverBase.cs:830`/`:848`, `Webserver.cs:494`/`:632`) |
| `watson.server.uptime` | ObservableGauge | `s` | — | `watson_server_uptime_seconds` | sampled from `Statistics.StartTime` |
| `watson.server.up` | ObservableGauge | `1` | — | `watson_server_up` | `1` between `ServerStarted` and `ServerStopped` |

### Routing, middleware, authentication (`watson.route.*`, `watson.middleware.*`, `watson.auth.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.route.matches` | Counter | `{match}` | `watson.route.type`, `http.route` | `watson_route_matches_total` | each match in `ProcessRoutingGroupAsync` (`WebserverBase.cs:965`/`:999`/`:1032`/`:1068`) |
| `watson.route.unmatched` | Counter | `{request}` | `http.request.method` | `watson_route_unmatched_total` | 404 no-match (`WebserverBase.cs:813`) |
| `watson.route.match.duration` | Histogram | `s` | `watson.route.type` | `watson_route_match_duration_seconds` | routing-group span *(detailed mode)* |
| `watson.middleware.duration` | Histogram | `s` | — | `watson_middleware_duration_seconds` | `MiddlewarePipeline.Execute` (`MiddlewarePipeline.cs:77`) |
| `watson.auth.requests` | Counter | `{request}` | `watson.auth.mode`, `watson.auth.authn`, `watson.auth.authz` | `watson_auth_requests_total` | `AuthenticateApiRequest` (`WebserverBase.cs:747`), `AuthenticateRequest` (`:775`) |

### API-route errors and timeouts (`watson.api.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.api.exceptions` | Counter | `{exception}` | `error.type`, `http.response.status_code`, `watson.result` | `watson_api_exceptions_total` | `ApiRouteHandler` catch arms (`ApiRouteHandler.cs:58`/`:62`/`:66`/`:70`) |
| `watson.api.timeouts` | Counter | `{request}` | `http.route` | `watson_api_timeouts_total` | 408 path (`ApiRouteHandler.cs:58` → `SendTimeoutResponseAsync:163`) |
| `watson.api.serialization.duration` | Histogram | `s` | `watson.direction` | `watson_api_serialization_duration_seconds` | serialize (`ApiResponseProcessor.cs:53`), deserialize (`ApiRouteHandler.cs:118`) *(detailed mode)* |

### HTTP/2 (`watson.http2.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.http2.streams.active` | UpDownCounter | `{stream}` | — | `watson_http2_streams_active` | `GetOrCreateRemoteStream` (`Http2ConnectionSession.cs:770`) / `RemoveStreamState` (`:947`) |
| `watson.http2.streams.total` | Counter | `{stream}` | — | `watson_http2_streams_total` | stream opened (`:770`) |
| `watson.http2.streams.refused` | Counter | `{stream}` | — | `watson_http2_streams_refused_total` | `MaxConcurrentStreams` refusal → RST_STREAM (`:382`) |
| `watson.http2.frames` | Counter | `{frame}` | `http2.frame.type`, `watson.direction` | `watson_http2_frames_total` | inbound `HandleFrameAsync` (`:185`), outbound `Http2ConnectionWriter` |
| `watson.http2.goaway` | Counter | `{frame}` | `watson.direction`, `http2.error.code` | `watson_http2_goaway_total` | send `SendGoAwayAsync` (`:811`), receive (`:207`) |
| `watson.http2.rst_stream` | Counter | `{frame}` | `watson.direction`, `http2.error.code` | `watson_http2_rst_stream_total` | send `SendRstStreamAsync` (`:794`), receive `HandleRstStreamFrame` (`:755`) |
| `watson.http2.flow_control.wait.duration` | Histogram | `s` | — | `watson_http2_flow_control_wait_duration_seconds` | send-window stall in `ReserveSendWindowAsync` (`:822`) *(opt-in)* |

### HTTP/3 / QUIC (`watson.http3.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.http3.streams.active` | UpDownCounter | `{stream}` | — | `watson_http3_streams_active` | `HandleBidirectionalStreamAsync` (`Http3ConnectionSession.cs:270`) / teardown (`:327`) |
| `watson.http3.streams.total` | Counter | `{stream}` | — | `watson_http3_streams_total` | request stream opened (`:270`) |
| `watson.http3.goaway` | Counter | `{frame}` | `watson.direction` | `watson_http3_goaway_total` | `SendGoAwayAsync` (`:754`) |
| `watson.http3.unavailable` | Counter | `{event}` | `watson.reason` | `watson_http3_unavailable_total` | runtime normalization disable (`WebserverSettingsValidator.cs:33`), emitted once at startup |

### WebSockets (`watson.websocket.*`)

| Instrument | Kind | Unit | Labels | Prometheus name | Emission site |
|---|---|---|---|---|---|
| `watson.websocket.sessions.active` | UpDownCounter | `{session}` | — | `watson_websocket_sessions_active` | `WebSocketSessionStarted` / `Ended` (`WebserverEvents.cs:88`/`:93`) |
| `watson.websocket.sessions.total` | Counter | `{session}` | — | `watson_websocket_sessions_total` | session started (`:88`) |
| `watson.websocket.messages` | Counter | `{message}` | `watson.direction` | `watson_websocket_messages_total` | `WebSocketSessionStatistics.IncrementReceived` / `IncrementSent` |
| `watson.websocket.bytes` | Counter | `By` | `watson.direction` | `watson_websocket_bytes_total` | same as above |
| `watson.websocket.handshake.failures` | Counter | `{handshake}` | `watson.reason` | `watson_websocket_handshake_failures_total` | `WebSocketHandshakeFailed` (`WebserverEvents.cs:98`) |

That is roughly forty instruments spanning every layer the request touches. The flow-control-wait
histogram and the serialization/route-match duration histograms are gated off by default because they
fire on genuinely hot paths; the rest are cheap enough to leave on.

---

## Traces

One span per request is the backbone. It is created at the top of
`WebserverBase.ProcessHttpContextAsync` (`WebserverBase.cs:661`) and disposed in that method's
`finally` (`:851`), so its duration is the true end-to-end request time and its status reflects the
final outcome — including the exception and disconnect paths the `finally` already handles.

**Root span.** Name `{http.request.method} {http.route}` (low-cardinality, per OTel span-naming
guidance — the template, never the raw path), kind `Server`. Attributes:

| Attribute | Source | Cardinality |
|---|---|---|
| `http.request.method` | `ctx.Request.Method` | bounded |
| `http.route` | `ctx.Route` template | bounded |
| `http.response.status_code` | `ctx.Response.StatusCode` | bounded |
| `url.scheme` | `Ssl.Enable`, or `X-Forwarded-Proto` when trusted | bounded |
| `url.path` | `ctx.Request.Url` path | **unbounded — span only** |
| `url.query` | request query | **unbounded — span only, redaction hook** |
| `network.protocol.version` | negotiated protocol | bounded |
| `network.protocol.name` | `http` | bounded |
| `client.address` / `client.port` | **resolved** client (forwarded header when trusted, else socket peer — see below) | **unbounded — span only** |
| `network.peer.address` / `network.peer.port` | raw socket peer from `ctx.Request.Source` (the proxy, behind a load balancer) | **unbounded — span only** |
| `server.address` / `server.port` | listener / `ctx.Request.Destination` | bounded |
| `user_agent.original` | `ctx.Request.Useragent` | **unbounded — span only** |
| `http.request.body.size` | `ctx.Request.ContentLength` / bytes read | bounded |
| `http.response.body.size` | bytes sent | bounded |
| `http.request.header.content-type` | request `Content-Type` | bounded (media type) |
| `http.response.header.content-type` | response `Content-Type` | bounded (media type) |
| `error.type` | exception type on failure | bounded |

Body size and content type are captured **bidirectionally** — request and response both carry a size
and a media type. The sizes on the span are the same values that feed the
`http.server.request.body.size` / `http.server.response.body.size` histograms, so a trace and the
metric agree. Content type is stamped as the two `http.request.header.content-type` /
`http.response.header.content-type` attributes (the OpenTelemetry header-capture convention),
normalized to the bare media type — `application/json`, not `application/json; charset=utf-8` — so it
stays low-cardinality if a consumer ever promotes it to a metric label. Capture is gated by
`Settings.Telemetry.CaptureContentType` (default on).

On the failure paths (`WebserverBase.cs:817-850`) the span status is set to `Error` with the exception
type, and an `exception` span event is recorded carrying `exception.type` / `exception.message` /
`exception.stacktrace` — the same event shape Radiant's `RadiantSpan.RecordException` emits, so it
renders identically in Tempo.

**Detailed child spans** (`Settings.Telemetry.DetailedSpans`, off by default). When a developer wants
to see where the time went *inside* a request, enabling this wraps the pipeline stages in child spans:
`authenticate` (`:747`/`:775`), `middleware` (`MiddlewarePipeline.Execute:77`), `route.match`
(`ProcessRoutingGroupAsync:932`), `handler`, and `serialize` (`ApiResponseProcessor.cs:19`). Left off,
those same boundaries are recorded as cheap span *events* on the root span instead of as allocating
child activities.

**Connection and per-stream spans** (`Settings.Telemetry.ConnectionSpans`, off by default). Optional
long-lived spans at `HandleClientConnectionAsync` (`Webserver.cs:737`), `HandleQuicConnectionAsync`
(`:608`), and per-stream in `Http2ConnectionSession.ProcessPendingRequestAsync` (`:393`) /
`Http3ConnectionSession.HandleBidirectionalStreamAsync` (`:270`).

### Client address and forwarded headers

Watson terminates the socket, so `ctx.Request.Source` is whatever connected — and behind a reverse
proxy, an ingress, or a cloud load balancer, that is the proxy, not the person. Recording the proxy as
`client.address` is worse than useless: every request looks like it came from one IP. Watson does not
parse `X-Forwarded-For` anywhere today, so this has to be built as part of the trace layer.

The model keeps the two facts separate, the way OpenTelemetry's HTTP conventions intend:
`network.peer.address` is always the real socket peer and is never spoofable; `client.address` is the
*resolved* client and is only as trustworthy as the hop that set the header. Resolution is **off by
default** because trusting a client-supplied header on an internet-facing listener lets any caller
forge its own source IP. A deployment behind a known proxy turns it on and declares who it trusts:

| Property | Type | Default | Meaning |
|---|---|---|---|
| `TrustForwardedHeaders` | `bool` | `false` | Resolve `client.address` from a forwarded header instead of the socket peer. |
| `ForwardedForHeader` | `string` | `X-Forwarded-For` | Header carrying the client-chain IP list. |
| `ForwardedProtoHeader` | `string` | `X-Forwarded-Proto` | Header carrying the client-visible scheme; overrides `url.scheme` when trusted. |
| `TrustedProxies` | `IpMatcher` | empty | CIDR/IP allow-list of proxies permitted to set the header. Reuses the `IpMatcher` dependency Watson already ships for access control. |
| `ForwardLimit` | `int` | `1` | Maximum proxy hops to walk from the right; caps how far back the chain is trusted. |

Resolution walks `X-Forwarded-For` from the **rightmost** entry (the nearest, most-trustworthy proxy)
leftward, stepping over addresses that match `TrustedProxies` up to `ForwardLimit` hops, and takes the
first untrusted address as `client.address`. With an empty `TrustedProxies` and
`TrustForwardedHeaders = true`, Watson trusts the immediate peer only and takes the last hop — the safe
default that still works for a single known LB. This mirrors how ASP.NET Core's forwarded-headers
middleware behaves, and it is deliberately conservative: a misconfiguration produces the proxy IP, not
a forgeable one.

None of this touches metrics. Client IP is unbounded and never becomes a metric label; it lives on the
span (and, if a host logs it, in logs). The forwarded value flows only into the span's
`client.address` / `url.scheme`.

### Context propagation

To stitch Watson into a distributed trace, the server must adopt an incoming trace context as the
parent of its root span. When `Settings.Telemetry.PropagateContext` is on (the default) and the
activity source has listeners, Watson reads the standard W3C headers off the inbound request before
starting the span:

| Header | Purpose |
|---|---|
| `traceparent` | W3C trace id + parent span id + flags |
| `tracestate` | vendor trace state |
| `baggage` | W3C baggage propagation |

Parsing uses `ActivityContext.TryParse` (BCL, no dependency). With no inbound `traceparent`, Watson
starts a fresh root — the normal edge-of-system case.

---

## Logs and correlation

Watson keeps its existing `WebserverEvents.Logger` (`Action<string>`) exactly as it is; this plan does
**not** add a hard dependency on `Microsoft.Extensions.Logging`. Correlation comes for free from the
trace layer: because the request span sets `Activity.Current` for the duration of the handler, any
`ILogger` the application uses inside a route handler is automatically stamped with `trace_id` and
`span_id` by the host's OpenTelemetry logging pipeline. The log-to-trace link in Grafana works without
Watson owning a logging abstraction.

One optional convenience is worth a task: expose the current `Activity.TraceId` on `HttpContextBase`
(a thin read-only pass-through to `Activity.Current`) so application code and the existing
`Events.Logger` string sink can include the trace id in their own messages.

---

## Consumer wiring

The whole integration on the consumer side is a settings object and two source names. Exact strings:

**Radiant.** Subscribe by name, and optionally register the HTTP catalog so undeclared labels are
caught:

```csharp
RadiantSettings settings = new RadiantSettings("my-service");
settings.Sources.AddMeter("Watson");
settings.Sources.AddActivitySource("Watson");

// Optional: enforce the HTTP metric catalog. Watson's four core instruments match these
// conventions name-for-name and label-for-label, so Strict mode accepts every measurement.
settings.Metrics.DefineAll(
    SemConv.Http.ServerRequestDuration,
    SemConv.Http.ServerActiveRequests,
    SemConv.Http.ServerRequestBodySize,
    SemConv.Http.ServerResponseBodySize);

settings.Prometheus.Enable = true;   // in-process /metrics on :9464
using (RadiantHost host = RadiantHost.Start(settings)) { /* run Watson */ }
```

**Raw OpenTelemetry SDK.**

```csharp
Sdk.CreateMeterProviderBuilder().AddMeter("Watson")./* exporter */.Build();
Sdk.CreateTracerProviderBuilder().AddSource("Watson")./* exporter */.Build();
```

**Prometheus.** Point a scrape at whatever `/metrics` endpoint the host exposes (Radiant's in-process
one, or the Collector's). The scraped series names are the Prometheus column in every table above —
`http_server_request_duration_seconds` and friends, plus the `watson_*` family.

---

## Settings surface

A new `TelemetrySettings` (one file, `Core/Settings/TelemetrySettings.cs`) exposed as
`WebserverSettings.Telemetry`, sitting beside `Debug`, `Timeout`, and `Protocols`. Flat properties,
no nested settings objects, so it stays one entity per file:

| Property | Type | Default | Meaning |
|---|---|---|---|
| `Enable` | `bool` | `true` | Master switch. When false, every record call is skipped before any tag work. |
| `MeterName` | `string` | `Watson` | Meter contract name; null/empty rejected on set. |
| `ActivitySourceName` | `string` | `Watson` | ActivitySource contract name; null/empty rejected on set. |
| `EnableMetrics` | `bool` | `true` | Enables the metric instruments. |
| `EnableTraces` | `bool` | `true` | Enables the root request span. |
| `PropagateContext` | `bool` | `true` | Adopt inbound `traceparent`/`tracestate`. |
| `CaptureRequestBodySize` | `bool` | `true` | Record `http.server.request.body.size` (metric + span). |
| `CaptureResponseBodySize` | `bool` | `true` | Record `http.server.response.body.size` (metric + span). |
| `CaptureContentType` | `bool` | `true` | Stamp request/response `content-type` (normalized media type) on the span. |
| `CaptureWebSocketMetrics` | `bool` | `true` | Record the `watson.websocket.*` family. |
| `RecordExceptionEvents` | `bool` | `true` | Attach `exception` events to the server span. |
| `TrustForwardedHeaders` | `bool` | `false` | Resolve `client.address` from a forwarded header (see *Client address and forwarded headers*). |
| `ForwardedForHeader` | `string` | `X-Forwarded-For` | Header carrying the client-chain IP list. |
| `ForwardedProtoHeader` | `string` | `X-Forwarded-Proto` | Header carrying the client-visible scheme. |
| `TrustedProxies` | `IpMatcher.Matcher` | empty | CIDR/IP allow-list of proxies permitted to set forwarded headers. |
| `ForwardLimit` | `int` | `1` | Maximum trusted proxy hops to walk (clamped ≥ 0). |
| `Prometheus.Enable` | `bool` | `false` | Serve the in-process Prometheus endpoint on the main listener (no extra port). |
| `Prometheus.Path` | `string` | `/metrics` | Path for the scrape endpoint; forced to begin with `/`. |

The `DetailedSpans`, `ConnectionSpans`, and HTTP/2 frame/flow-control toggles from the original draft
are deferred along with the instruments they gate (see *Implementation status*), and are not present
on the shipped `TelemetrySettings`.

Defaults keep every user whole: on a server with no collector attached, the instruments and source
have no listeners, so the cost is the documented near-zero. A user who wants absolute silence sets
`Enable = false` and Watson short-circuits before touching the BCL at all. Forwarded-header resolution
stays off until a deployment behind a known proxy opts in and declares its `TrustedProxies`, so an
internet-facing listener never trusts a client-supplied source IP by accident.

---

## Implementation plan

Phased so a developer can land it incrementally and check off progress. Each phase builds and passes
tests before the next. Nothing here removes or renames existing API.

### Phase 0 — Project and contract scaffolding

- [x] Add `System.Diagnostics.DiagnosticSource` `PackageReference` to `WatsonWebserver.csproj`, scoped
      to `netstandard2.1` (net8/net10 have it in-box). Version `8.0.1` to match Radiant's SemConv.
- [x] Create `Core/Telemetry/WatsonTelemetryNames.cs` — a `static class` of `public const string`
      instrument names and attribute keys (the full contract from this document). One entity, one file.
- [x] Create `Core/Settings/TelemetrySettings.cs` (plus `TelemetryPrometheusSettings.cs`) with guard
      clauses on the string setters and XML docs stating defaults.
- [x] Add `WebserverSettings.Telemetry` property (backing field, null-check on set) beside `Debug`.

### Phase 1 — Telemetry object and core HTTP metrics

- [x] Create `Core/Telemetry/WebserverTelemetry.cs` — an `IDisposable` instance type that owns the
      `Meter` and `ActivitySource` (named from settings), creates the instrument handles, and exposes
      the record/span methods. Full dispose pattern; also owns the Prometheus collector.
- [x] Expose `WebserverBase.Telemetry` (read-only), constructed from `Settings.Telemetry`, mirroring
      how `Statistics` and `Events` are owned. Disposed via `DisposeTelemetry()` in `Webserver.Dispose`.
- [x] Instrument the request hot path in `ProcessHttpContextAsync`: `active_requests` +1 at start,
      duration + body-size + `active_requests` −1 in the `finally`, using `ctx.Timestamp.TotalMs`,
      `ctx.Response.StatusCode`, `ctx.Request.Method`, and the resolved `ctx.Route` template, with
      `TagList` on the hot path.

### Phase 2 — Request span and context propagation

- [x] Start the root `Server` span, guarded by `EnableTraces` and `HasListeners()`; set the attribute
      set; dispose in the `finally` with the correct status (Error + `exception` event on the catch
      paths).
- [x] Implement W3C context extraction (`traceparent`/`tracestate`) via `ActivityContext.TryParse`
      when `PropagateContext` is on.
- [x] Implement client-address resolution (`ForwardedHeaderResolver`): always set
      `network.peer.address`/`port`; when `TrustForwardedHeaders` is on, resolve `client.address` by
      walking `ForwardedForHeader` past `TrustedProxies` up to `ForwardLimit`, and override `url.scheme`
      from `ForwardedProtoHeader`. Reuses `IpMatcher`. Spoofing covered by test (untrusted → header ignored).
- [x] Stamp bidirectional body size and normalized `content-type` on the span, gated by
      `CaptureRequestBodySize` / `CaptureResponseBodySize` / `CaptureContentType`.
- [ ] Add `HttpContextBase.TraceId` read-only pass-through to `Activity.Current` for log correlation. *(deferred)*

### Phase 3 — Connection, routing, auth, exceptions

- [x] Bridge connections to `watson.server.connections.*` and byte totals via event subscriptions and
      observable gauges over `WebserverStatistics` inside `WebserverTelemetry` (no transport edits).
- [x] Emit `watson.route.matches` at each match arm and `watson.route.unmatched` at the 404 site.
      `watson.middleware.duration` is deferred.
- [x] Emit `watson.auth.requests` at both auth call sites, tagged from `AuthResult`'s
      `AuthenticationResultEnum` / `AuthorizationResultEnum`.
- [x] Emit `watson.server.exceptions` (from `ExceptionEncountered`) and
      `watson.server.requests.aborted` / `.disconnected` (from the abort/disconnect events).
- [ ] Emit `watson.api.exceptions` / `watson.api.timeouts` in the four `ApiRouteHandler` catch arms. *(deferred — API errors surface via the duration histogram's status code)*

### Phase 4 — Protocol-specific and WebSocket metrics

- [ ] HTTP/2 per-frame counters (`frames` by type, `goaway`, `rst_stream`, `streams.refused`,
      flow-control-wait). *(deferred — needs `Http2ConnectionSession`/`Http2ConnectionWriter` edits; active stream count shipped as a gauge)*
- [ ] HTTP/3 `goaway` and `watson.http3.unavailable` counters. *(deferred — active stream count shipped as a gauge)*
- [x] WebSockets: `sessions.active` / `sessions.total` / `handshake.failures` via the WebSocket events.
      Per-message `messages` / `bytes` counters are deferred.
- [x] Observable gauges: `watson.server.uptime`, `watson.server.up`, active connections, active
      HTTP/2 and HTTP/3 streams, received/sent bytes.

### Phase 5 — Tests, docs, release

- [x] Tests (`SharedTelemetryTests`, wired into both runners).
- [x] Documentation and versioning artifacts (release checklist below).

---

## Testing plan

Assertions ride the same BCL the emit path does — no collector, no OpenTelemetry SDK test dependency.
A `MeterListener` verifies metrics and an `ActivityListener` verifies spans, both in-box.

- [x] Add a shared telemetry suite (`SharedTelemetryTests`, wired into both `Test.Automated` and
      `Test.XUnit`) that:
  - attaches a `MeterListener` to `Watson`, drives a request, and asserts the duration instrument
    carries the `http.route` **template** (`/users/{id}`), plus the active-request and route-match
    counters.
  - attaches an `ActivityListener` and asserts a `Server` span with the route template and `Ok` status.
  - sends `X-Forwarded-For` from a trusted host and asserts `client.address` is the forwarded value
    while `network.peer.address` stays the socket peer; repeats untrusted and asserts the header is
    ignored (no spoofing).
  - scrapes the in-process Prometheus endpoint and asserts the derived series names appear.
  - asserts `Enable = false` produces zero instrument callbacks and zero spans.
  - asserts the error path: a 500 sets the span to `Error` status and increments
    `watson.server.exceptions` with the `error.type`.
  - feeds an inbound `traceparent` and asserts the span adopts the trace id and parent span id.
  - asserts request `content-type` and body size land on the span, and the auth-decision counter fires.
  - drives an HTTP/2 request and asserts the span carries `network.protocol.version = 2`.
  - asserts the Prometheus scrape renders a valid histogram (`_bucket` / `le="+Inf"` / `_sum` / `_count`).

  Each test uses a unique meter/activity-source name so parallel test classes cannot contaminate it,
  and the suite is green on both `net8.0` and `net10.0`.
- [x] The full existing suite (186 shared core cases) continues to pass — the telemetry layer is additive.
- [ ] Extend `Test.RestApi` or a sample to show the Radiant wiring end-to-end against a live
      `/metrics` scrape. *(deferred)*

---

## Versioning and release artifacts (7.1.0)

The bump is `7.0.15` → `7.1.0`. Every version-referencing artifact in the repository:

- [x] `src/WatsonWebserver/WatsonWebserver.csproj` — `<Version>7.1.0</Version>`, new
      `<PackageReleaseNotes>`, and the telemetry/observability/prometheus `<PackageTags>`.
- [x] `src/Watson.Clients/Watson.Clients.csproj` — left at `7.0.14`; it gains no telemetry, so the
      version gap is intentional.
- [x] `CHANGELOG.md` — added a `v7.1.0` entry and updated the **Current Version** marker; the existing
      `Watson.Clients` items remain under **Unreleased**.
- [x] `README.md` — added the **Observability and Telemetry** section and a feature-list entry.
- [x] `WEBSERVER_SETTINGS.md` — documented `WebserverSettings.Telemetry` and every `TelemetrySettings`
      property with its default.
- [x] `CLAUDE.md` — added a **Telemetry** subsection.
- [x] Confirmed no other file hard-codes the package version.

Watson has no `DOCKERHUB_README.md` today, so none is in scope for this change.

---

## Compliance with `c:\code\agents\requirements`

Every new file follows the repository's mandatory code style: `namespace` first with `using`
statements inside it (Microsoft/system usings alphabetized first), XML docs on all public members,
`_PascalCase` private fields, no `var`, no tuples, `using (...)` statements over declarations, one
entity per file, guard clauses with specific exception types, configurable values as properties backed
by sensible defaults (clamped where a range applies), the full `IDisposable` pattern on
`WebserverTelemetry`, and no `Console.WriteLine` in library code. `TelemetrySettings` string setters
reject null/empty; `WebserverBase.Telemetry` is disposed in teardown. The emit path allocates nothing
on the hot line by passing tags through `TagList`, consistent with the cost model this instrumentation
is built to honor.

---

## Decisions a reviewer should weigh in on

A few choices are defensible either way, and it is cheaper to settle them before code than after:

- **Master default.** `Enable = true` gives every user standardized telemetry out of the box at
  near-zero unobserved cost. The conservative alternative — default off, opt in — trades that for a
  guarantee of *identical* runtime behavior to 7.0.x. The plan assumes on-by-default; flip it if the
  project prefers strict opt-in.
- **`error.type` on request duration.** Off by default to keep Strict-catalog compatibility with
  Radiant's three-label `SemConv.Http.ServerRequestDuration`. Turning it on adds a bounded but real
  dimension and diverges from that catalog. Left as a future toggle.
- **Detailed and connection spans.** Off by default because they allocate on live paths. The root span
  alone answers most questions; the rest is opt-in depth.
