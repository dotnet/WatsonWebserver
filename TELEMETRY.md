# Watson Telemetry

Watson measures itself and hands the numbers to whatever you already use to watch your systems. It
emits metrics and traces through the .NET base class library — a `Meter` and an `ActivitySource`, both
named `Watson` — and stops there. It never opens a connection to a backend, never picks a vendor, and
never asks you to configure an exporter. Your collector subscribes to those two names and takes it from
there. If nothing is listening, the whole thing costs a few nanoseconds per request and allocates
nothing.

That split is the thing to hold onto: **Watson emits, your host collects.** Everything below is about
the two halves of that sentence — what Watson puts on the wire, and how to pick it up.

## Getting the data out in two lines

Point any OpenTelemetry-aware collector at the two source names and you have metrics and traces. With
the OpenTelemetry SDK:

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

Sdk.CreateMeterProviderBuilder()
    .AddMeter("Watson")
    .AddOtlpExporter()      // or Prometheus, console, whatever you run
    .Build();

Sdk.CreateTracerProviderBuilder()
    .AddSource("Watson")
    .AddOtlpExporter()
    .Build();
```

With [Radiant](https://github.com/jchristn/Radiant), the same idea, one settings object:

```csharp
RadiantSettings settings = new RadiantSettings("my-service");
settings.Sources.AddMeter("Watson");
settings.Sources.AddActivitySource("Watson");

using (RadiantHost host = RadiantHost.Start(settings))
{
    // start Watson, run your app
}
```

Watson takes no dependency on either one. The strings `"Watson"` are the entire contract — you can run
Prometheus, Tempo, Jaeger, Azure Monitor, or a two-line unit test against the same instrumentation, and
Watson neither knows nor cares which.

Both names default to `Watson` and can be changed with `Settings.Telemetry.MeterName` and
`ActivitySourceName` if you run several servers in one process and want to tell them apart. Treat them
like a public API once you've deployed a dashboard against them — they're a promise to your collector.

## What Watson measures

The four request metrics use the OpenTelemetry HTTP server semantic-convention names, so they light up
a stock Grafana panel with no relabeling. Everything Watson knows that those conventions don't cover
lives under `watson.*`. Instrument names are dotted; a Prometheus exporter rewrites them to the snake
case in the last column (adding `_total`, `_seconds`, `_bytes`, and histogram suffixes).

| Instrument | Kind | Unit | Key labels | Prometheus name |
|---|---|---|---|---|
| `http.server.request.duration` | Histogram | s | method, status, route | `http_server_request_duration_seconds` |
| `http.server.active_requests` | UpDownCounter | {request} | method, scheme | `http_server_active_requests` |
| `http.server.request.body.size` | Histogram | By | method | `http_server_request_body_size_bytes` |
| `http.server.response.body.size` | Histogram | By | method, status | `http_server_response_body_size_bytes` |
| `watson.server.connections.active` | Gauge | {connection} | — | `watson_server_connections_active` |
| `watson.server.connections.total` | Counter | {connection} | protocol version | `watson_server_connections_total` |
| `watson.server.connections.denied` | Counter | {connection} | protocol version | `watson_server_connections_denied_total` |
| `watson.server.requests.aborted` | Counter | {request} | — | `watson_server_requests_aborted_total` |
| `watson.server.requests.disconnected` | Counter | {request} | — | `watson_server_requests_disconnected_total` |
| `watson.server.exceptions` | Counter | {exception} | error type, protocol version | `watson_server_exceptions_total` |
| `watson.server.received.bytes` | Counter | By | — | `watson_server_received_bytes_total` |
| `watson.server.sent.bytes` | Counter | By | — | `watson_server_sent_bytes_total` |
| `watson.server.uptime` | Gauge | s | — | `watson_server_uptime_seconds` |
| `watson.server.up` | Gauge | 1 | — | `watson_server_up` |
| `watson.http2.streams.active` | Gauge | {stream} | — | `watson_http2_streams_active` |
| `watson.http3.streams.active` | Gauge | {stream} | — | `watson_http3_streams_active` |
| `watson.route.matches` | Counter | {match} | route type, route | `watson_route_matches_total` |
| `watson.route.unmatched` | Counter | {request} | method | `watson_route_unmatched_total` |
| `watson.auth.requests` | Counter | {request} | mode, authn, authz | `watson_auth_requests_total` |
| `watson.websocket.sessions.active` | UpDownCounter | {session} | — | `watson_websocket_sessions_active` |
| `watson.websocket.sessions.total` | Counter | {session} | — | `watson_websocket_sessions_total` |
| `watson.websocket.handshake.failures` | Counter | {handshake} | — | `watson_websocket_handshake_failures_total` |

Histogram bucket boundaries are a collector concern, not Watson's — pick them with an OpenTelemetry view
or a Radiant `LatencyBuckets` preset. Watson emits the raw measurements and lets the aggregation happen
downstream.

### The label keys, exactly

These are the strings you'll filter and group by. They follow OpenTelemetry naming:

| Key | Example values |
|---|---|
| `http.request.method` | `GET`, `POST`, `PUT`, `DELETE` |
| `http.response.status_code` | `200`, `404`, `500` |
| `http.route` | the route **template**, e.g. `/users/{id}` |
| `url.scheme` | `http`, `https` |
| `network.protocol.version` | `1.1`, `2`, `3` |
| `error.type` | the exception type name |
| `watson.route.type` | `Static`, `Content`, `Parameter`, `Dynamic` |
| `watson.auth.mode` | `api`, `legacy` |
| `watson.auth.authn` | `Success`, `NotFound`, `Expired`, … |
| `watson.auth.authz` | `Permitted`, `DeniedImplicit`, `DeniedExplicit` |

## Traces

Every request produces one span, kind `Server`, named `{method} {route}` — for example `GET /users/{id}`.
Its duration is the real end-to-end time, and its status turns to `Error` on a 5xx or an unhandled
exception, with the exception type, message, and stack attached as a span event. The span carries the
low-cardinality dimensions you'd expect (method, route, status, scheme, protocol version) plus the
high-cardinality detail that has no business on a metric: the raw path, the client address, the user
agent, and — in both directions — body size and content type.

If an inbound request already carries a W3C `traceparent` header, Watson adopts it as the span's parent,
so a request that arrives from an upstream service joins the same trace instead of starting a new one.
Nothing to configure; it happens whenever the header is present and a sampler is listening.

```
GET /orders/{id}                 (upstream service span)
  └─ GET /orders/{id}            (Watson server span, same trace)
       └─ your handler's spans   (whatever you start inside the handler)
```

Because Watson sets `Activity.Current` for the life of the handler, any `ILogger` you use inside a route
is automatically stamped with the trace and span id by the OpenTelemetry logging pipeline. You get
log-to-trace correlation without Watson owning your logging.

## The one rule that keeps this cheap

`http.route` is always the route **template**, never the concrete path. A metric labeled `/users/{id}`
is one time series no matter how many users you have. If it were labeled with the real path, you'd get a
new series per user, forever, until your scrape endpoint fell over trying to serialize them. Watson
enforces this for you: parameter routes report `/users/{id}`, dynamic routes report their pattern, and
requests that match nothing carry no route label at all (they land in `watson.route.unmatched` instead).

The corollary: the identifiers you actually want to search by — the specific user id, the request GUID,
the caller's IP — live on **spans and logs**, where one row per request is the whole point. Let a trace
carry the id from span to log. Keep the metrics to things you could list on a whiteboard.

## Getting the real client IP behind a proxy

Watson terminates the socket, so out of the box `client.address` on the span is whoever connected —
which behind a load balancer or reverse proxy is the proxy, not the person. The raw peer is always
recorded separately as `network.peer.address`. To make `client.address` the real client, turn on
forwarded-header resolution and tell Watson which proxies it may believe:

```csharp
server.Settings.Telemetry.TrustForwardedHeaders = true;
server.Settings.Telemetry.TrustedProxies.Add("10.0.0.0", "255.0.0.0");
server.Settings.Telemetry.ForwardLimit = 1;   // hops to walk back through
```

Watson then walks `X-Forwarded-For` from the nearest hop, stepping over addresses in your trusted list
up to `ForwardLimit`, and takes the first address it doesn't recognize as the client. `X-Forwarded-Proto`
does the same for `url.scheme`. This is off by default on purpose: trusting a client-supplied header on
an internet-facing listener would let any caller forge its own source IP. It only ever affects span
attributes — never a routing or access-control decision — so the worst case of a misconfiguration is a
wrong label, not a security hole.

## Serving `/metrics` yourself, without a second port

If you don't run a collector, Watson can expose a Prometheus scrape endpoint in-process. It's served on
the **same listener your app already uses**, at a path you choose — so it opens no new port and can't
collide with anything. It's disabled by default:

```csharp
server.Settings.Telemetry.Prometheus.Enable = true;    // GET /metrics on your existing port
server.Settings.Telemetry.Prometheus.Path = "/metrics"; // change it if it clashes with a route
```

A scraper then reads `http://<your-host>:<your-port>/metrics` and gets the snake-case series from the
table above — `http_server_request_duration_seconds` histograms, `watson_server_up`, and the rest. This
is meant as a zero-infrastructure convenience for a single process; once you have more than one, point an
OpenTelemetry Collector at the OTLP export instead and leave this off.

## Settings, at a glance

Everything lives under `Settings.Telemetry`. The defaults are chosen so a fresh server is fully
instrumented and effectively free until a collector attaches.

| Setting | Default | What it does |
|---|---|---|
| `Enable` | `true` | Master switch; `false` skips all emission. |
| `MeterName` / `ActivitySourceName` | `Watson` | The names your collector subscribes to. |
| `EnableMetrics` / `EnableTraces` | `true` | Turn metrics or traces off independently. |
| `PropagateContext` | `true` | Adopt an inbound `traceparent` as the span's parent. |
| `CaptureRequestBodySize` / `CaptureResponseBodySize` | `true` | Record body-size metric and span attribute. |
| `CaptureContentType` | `true` | Stamp request/response media type on the span. |
| `CaptureWebSocketMetrics` | `true` | Record WebSocket session and handshake metrics. |
| `RecordExceptionEvents` | `true` | Attach an exception event to the span on failure. |
| `TrustForwardedHeaders` | `false` | Resolve `client.address` from a forwarded header. |
| `ForwardedForHeader` / `ForwardedProtoHeader` | `X-Forwarded-For` / `X-Forwarded-Proto` | Which headers to read. |
| `TrustedProxies` | empty | Proxies allowed to set forwarded headers. |
| `ForwardLimit` | `1` | Trusted proxy hops to walk. |
| `Prometheus.Enable` / `Prometheus.Path` | `false` / `/metrics` | In-process scrape endpoint on the main listener. |

The full property reference with validation rules is in [WEBSERVER_SETTINGS.md](WEBSERVER_SETTINGS.md).

## Verifying it yourself

You don't need a running collector to assert on Watson's output — the same BCL primitives the collectors
use will do. A `MeterListener` reads metrics and an `ActivityListener` reads spans, both in-box, no
OpenTelemetry package required:

```csharp
using System.Diagnostics.Metrics;

List<string> seen = new List<string>();
using (MeterListener listener = new MeterListener())
{
    listener.InstrumentPublished = (instrument, l) =>
    {
        if (instrument.Meter.Name == "Watson") l.EnableMeasurementEvents(instrument);
    };
    listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
    {
        if (instrument.Name == "http.server.request.duration") seen.Add("recorded");
    });
    listener.Start();

    // drive a request against your server, then assert `seen` is non-empty
}
```

Watson's own test suite (`SharedTelemetryTests`) does exactly this across HTTP/1.1 and HTTP/2 — asserting
the route template on the duration metric, the error-status span and exception counter on a 500, inbound
trace-context adoption, forwarded-header resolution, and the Prometheus scrape shape.

## What it costs

An `Add` or `Record` with nobody listening is an enabled-check and an early return — low single-digit
nanoseconds, zero allocation. A `StartActivity` on an unsampled source returns null in about the same
time. That's the property that makes it safe to leave on by default: until you attach a collector, the
instrumentation is close to invisible. Once a collector is subscribed, counters and histograms stay
cheap (tens of nanoseconds), while spans allocate only when they're actually sampled, which is why the
sampling ratio lives on your collector, not on Watson. And the cost that actually matters — memory —
scales with distinct label combinations, not with request count. Keep the labels bounded (the route
template rule above) and ten thousand requests a second is as cheap as ten.

If you want silence, set `Settings.Telemetry.Enable = false` and Watson short-circuits before it touches
the BCL at all.
