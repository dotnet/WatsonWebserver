# Testing HTTP/2 and HTTP/3 with WatsonWebserver

This guide shows how to test WatsonWebserver's HTTP/2 and HTTP/3 support from various terminals and tools.

> **Note:** This document started as a forward-looking plan, but the repository now contains real HTTP/1.1, HTTP/2, and HTTP/3 test harnesses. Some sections still describe broader validation goals that remain in progress.
>
> The normative shared behavior contract is documented in [PHASE0_SEMANTIC_SPEC.md](PHASE0_SEMANTIC_SPEC.md).
>
> The HTTP/2 implementation milestone and acceptance coverage are summarized in [PHASE2_HTTP2_CLOSEOUT.md](PHASE2_HTTP2_CLOSEOUT.md).

---

## Prerequisites

### TLS Certificates

HTTP/2 over TLS (the standard mode) and HTTP/3 (always TLS 1.3) require a certificate.

**Development certificate (.NET):**
```bash
dotnet dev-certs https --trust
```

**Self-signed certificate (OpenSSL):**
```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes -subj "/CN=localhost"
openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -password pass:password
```

### Tool Versions

| Tool | HTTP/2 Support | HTTP/3 Support |
|------|---------------|---------------|
| curl (Windows built-in) | 7.47+ (Windows 10 1803+) | Varies — check `curl -V` for `HTTP3` |
| curl (Linux/macOS) | 7.47+ | 7.88+ with nghttp3/quiche backend |
| PowerShell | 7.0+ (`-HttpVersion 2.0`) | 7.4+ (`-HttpVersion 3.0`) |
| .NET HttpClient | .NET Core 3.0+ | .NET 6.0+ |
| nghttp | All versions | N/A |
| h2load | All versions | N/A |
| quiche-client | N/A | All versions |

**Check your curl version and protocol support:**
```bash
curl -V
```
Look for `HTTP2` and/or `HTTP3` in the Features line.

If your Windows `curl.exe` does not report `HTTP2` and/or `HTTP3`, install a curl build that includes those features before attempting the HTTP/2 or HTTP/3 sections below.

Recommended Windows download:
`https://curl.se/windows/`

---

## Testing HTTP/1.1 (current, works today)

These commands work against any WatsonWebserver instance (Watson, Lite, Http2, or Http3).

Assumes server running on `http://127.0.0.1:8080`.

### Windows Command Prompt

```cmd
REM Basic GET
curl http://127.0.0.1:8080/hello

REM Check status code
curl -o NUL -s -w "%%{http_code}" http://127.0.0.1:8080/hello

REM Show response headers
curl -D - -o NUL -s http://127.0.0.1:8080/hello

REM POST with body
curl -X POST -d "hello world" http://127.0.0.1:8080/test/echo

REM HEAD request
curl -I http://127.0.0.1:8080/hello

REM Verbose (shows request and response headers)
curl -v http://127.0.0.1:8080/hello
```

### PowerShell

```powershell
# Basic GET
Invoke-WebRequest -Uri http://127.0.0.1:8080/hello

# Just the body
(Invoke-WebRequest -Uri http://127.0.0.1:8080/hello).Content

# Status code
(Invoke-WebRequest -Uri http://127.0.0.1:8080/hello).StatusCode

# Response headers
(Invoke-WebRequest -Uri http://127.0.0.1:8080/hello).Headers

# POST with body
Invoke-WebRequest -Uri http://127.0.0.1:8080/test/echo -Method POST -Body "hello world"

# Using HttpClient for more control
$client = [System.Net.Http.HttpClient]::new()
$response = $client.GetAsync("http://127.0.0.1:8080/hello").Result
$response.StatusCode
$response.Content.ReadAsStringAsync().Result
```

### Bash

```bash
# Basic GET
curl http://127.0.0.1:8080/hello

# Check status code
curl -o /dev/null -s -w '%{http_code}' http://127.0.0.1:8080/hello

# Show headers and body
curl -D - http://127.0.0.1:8080/hello

# POST with body
curl -X POST -d "hello world" http://127.0.0.1:8080/test/echo

# Save headers to file, body to stdout
curl -D /tmp/headers.txt http://127.0.0.1:8080/hello

# Verify specific header
curl -s -D - -o /dev/null http://127.0.0.1:8080/hello | grep "Content-Type"
```

---

## Testing HTTP/2 (current, works today)

Assumes server running on `https://127.0.0.1:8443` with TLS.

Windows note: these commands require a curl build with `HTTP2` support in `curl -V`. The Microsoft-shipped Windows curl may not include it. Recommended download:
`https://curl.se/windows/`

Repository harnesses:

- `dotnet run --project src\\Test.CurlInterop\\Test.CurlInterop.csproj --framework net10.0 -c Release -- --curl "<path-to-curl.exe>"`
- `dotnet run --project src\\Test.Benchmark\\Test.Benchmark.csproj --framework net10.0 -c Release -- --targets watson,kestrel --protocols http2 --scenarios hello,echo,sse`

### Understanding HTTP/2 Modes

- **h2** — HTTP/2 over TLS. Standard mode. Negotiated via ALPN during TLS handshake.
- **h2c** — HTTP/2 cleartext (no TLS). Negotiated via HTTP/1.1 `Upgrade: h2c` header or "prior knowledge" (client assumes HTTP/2 without negotiation).

Most production HTTP/2 uses **h2** (TLS). Use **h2c** only for local testing without certificates.

### Windows Command Prompt

```cmd
REM HTTP/2 over TLS (standard)
curl --http2 https://127.0.0.1:8443/hello

REM HTTP/2 over TLS, accept self-signed cert
curl -k --http2 https://127.0.0.1:8443/hello

REM HTTP/2 cleartext (prior knowledge, no TLS)
curl --http2-prior-knowledge http://127.0.0.1:8080/hello

REM Verify HTTP/2 was negotiated
curl -v -k --http2 https://127.0.0.1:8443/hello 2>&1 | findstr "HTTP/2"

REM Show HTTP version in response
curl -k -o NUL -s -w "%%{http_version}" --http2 https://127.0.0.1:8443/hello

REM POST with body over HTTP/2
curl -k --http2 -X POST -d "hello" https://127.0.0.1:8443/test/echo

REM Parallel requests (tests multiplexing)
curl -k --http2 -Z https://127.0.0.1:8443/hello https://127.0.0.1:8443/hola https://127.0.0.1:8443/login

REM Show all request and response headers
curl -v -k --http2 https://127.0.0.1:8443/hello 2>&1
```

### PowerShell

```powershell
# HTTP/2 request (PowerShell 7+)
Invoke-WebRequest -Uri https://127.0.0.1:8443/hello -HttpVersion 2.0 -SkipCertificateCheck

# Check negotiated protocol version
$response = Invoke-WebRequest -Uri https://127.0.0.1:8443/hello -HttpVersion 2.0 -SkipCertificateCheck
$response.BaseResponse.Version   # Should show "2.0"

# Using HttpClient with explicit HTTP/2
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = { $true }
$client = [System.Net.Http.HttpClient]::new($handler)
$request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "https://127.0.0.1:8443/hello")
$request.Version = [Version]::new(2, 0)
$response = $client.SendAsync($request).Result
$response.Version          # Should show "2.0"
$response.StatusCode       # Should show "OK"
$response.Content.ReadAsStringAsync().Result

# Concurrent HTTP/2 requests (multiplexed on one connection)
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = { $true }
$client = [System.Net.Http.HttpClient]::new($handler)
$tasks = @()
for ($i = 0; $i -lt 10; $i++) {
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "https://127.0.0.1:8443/hello")
    $req.Version = [Version]::new(2, 0)
    $tasks += $client.SendAsync($req)
}
[System.Threading.Tasks.Task]::WaitAll($tasks)
$tasks | ForEach-Object { $_.Result.StatusCode }
```

### Bash

```bash
# HTTP/2 over TLS
curl --http2 -k https://127.0.0.1:8443/hello

# HTTP/2 cleartext (prior knowledge)
curl --http2-prior-knowledge http://127.0.0.1:8080/hello

# Verify HTTP/2 negotiation
curl -v -k --http2 https://127.0.0.1:8443/hello 2>&1 | grep '< HTTP/'

# Show protocol version
curl -k -o /dev/null -s -w '%{http_version}\n' --http2 https://127.0.0.1:8443/hello
# Expected output: "2"

# Verify response body
curl -k -s --http2 https://127.0.0.1:8443/hello
# Expected: "Hello static route"

# Verify response headers
curl -k -s -D - -o /dev/null --http2 https://127.0.0.1:8443/hello

# POST with body
curl -k --http2 -X POST -d "hello world" https://127.0.0.1:8443/test/echo

# Parallel requests (multiplexing test)
curl -k --http2 -Z \
  https://127.0.0.1:8443/hello \
  https://127.0.0.1:8443/hola \
  https://127.0.0.1:8443/login

# Using nghttp for detailed HTTP/2 frame inspection
nghttp -v https://127.0.0.1:8443/hello

# HTTP/2 benchmarking
h2load -n 1000 -c 10 -m 100 https://127.0.0.1:8443/hello
# -n: total requests, -c: connections, -m: max concurrent streams per connection
```

### Verification Checklist (HTTP/2)

For each test, verify:

```
[ ] Status code is correct (200, 302, 404, etc.)
[ ] Response body matches expected content
[ ] Content-Type header is correct
[ ] Content-Length header is present and correct
[ ] Protocol version is HTTP/2 (check with -w '%{http_version}' or -v)
[ ] Default headers present (Access-Control-Allow-Origin, Cache-Control, etc.)
[ ] Multiple concurrent requests complete successfully (multiplexing works)
[ ] Server doesn't crash under load
```

---

## Testing HTTP/3 (future — Phase 3+)

Assumes server running on `https://127.0.0.1:8443` with TLS and QUIC enabled.

Windows note: these commands require a curl build with `HTTP3` support in `curl -V`. The Microsoft-shipped Windows curl may not include it. Recommended download:
`https://curl.se/windows/`

### Windows Command Prompt

```cmd
REM HTTP/3 request (requires curl with HTTP/3 support)
curl --http3 -k https://127.0.0.1:8443/hello

REM HTTP/3 only (fail if not available, don't fall back)
curl --http3-only -k https://127.0.0.1:8443/hello

REM Verify HTTP/3 was negotiated
curl -v -k --http3 https://127.0.0.1:8443/hello 2>&1 | findstr "HTTP/3"

REM Show protocol version
curl -k -o NUL -s -w "%%{http_version}" --http3 https://127.0.0.1:8443/hello

REM Check Alt-Svc header (HTTP/3 discovery via HTTP/1.1 or HTTP/2)
curl -v -k https://127.0.0.1:8443/hello 2>&1 | findstr "alt-svc"

REM POST over HTTP/3
curl -k --http3 -X POST -d "hello" https://127.0.0.1:8443/test/echo
```

### PowerShell

```powershell
# HTTP/3 request (PowerShell 7.4+)
Invoke-WebRequest -Uri https://127.0.0.1:8443/hello -HttpVersion 3.0 -SkipCertificateCheck

# Check negotiated version
$response = Invoke-WebRequest -Uri https://127.0.0.1:8443/hello -HttpVersion 3.0 -SkipCertificateCheck
$response.BaseResponse.Version   # Should show "3.0"

# Using HttpClient with explicit HTTP/3
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.ServerCertificateCustomValidationCallback = { $true }
$client = [System.Net.Http.HttpClient]::new($handler)
$request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, "https://127.0.0.1:8443/hello")
$request.Version = [Version]::new(3, 0)
$request.VersionPolicy = [System.Net.Http.HttpVersionPolicy]::RequestVersionExact
$response = $client.SendAsync($request).Result
$response.Version          # Should show "3.0"
$response.Content.ReadAsStringAsync().Result

# Verify Alt-Svc discovery
$response = Invoke-WebRequest -Uri https://127.0.0.1:8443/hello -SkipCertificateCheck
$response.Headers["Alt-Svc"]   # Should contain "h3=\":8443\""
```

### Bash

```bash
# HTTP/3 request
curl --http3 -k https://127.0.0.1:8443/hello

# HTTP/3 only (fail if unavailable)
curl --http3-only -k https://127.0.0.1:8443/hello

# Verify HTTP/3 negotiation
curl -v -k --http3 https://127.0.0.1:8443/hello 2>&1 | grep 'using HTTP/3'

# Show protocol version
curl -k -o /dev/null -s -w '%{http_version}\n' --http3 https://127.0.0.1:8443/hello
# Expected output: "3"

# Check Alt-Svc discovery
curl -k -s -D - -o /dev/null https://127.0.0.1:8443/hello | grep -i alt-svc

# POST with body over HTTP/3
curl -k --http3 -X POST -d "hello world" https://127.0.0.1:8443/test/echo

# Using quiche-client for detailed QUIC inspection
quiche-client --no-verify https://127.0.0.1:8443/hello
```

### Verification Checklist (HTTP/3)

```
[ ] Status code is correct
[ ] Response body matches expected content
[ ] Protocol version is HTTP/3 (check with -w '%{http_version}')
[ ] Alt-Svc header present in HTTP/1.1 and HTTP/2 responses
[ ] Server handles UDP correctly (firewall allows UDP on port)
[ ] Multiple concurrent QUIC streams complete successfully
[ ] Connection works after client IP change (connection migration)
[ ] Server doesn't crash under load
```

---

## Testing Protocol Negotiation (future — Phase 4)

These tests verify that a multi-protocol server correctly handles clients requesting different HTTP versions.

### Client requests HTTP/2, server supports it

```bash
curl -v -k --http2 https://127.0.0.1:8443/hello 2>&1 | grep '< HTTP/'
# Expected: "< HTTP/2 200"
```

### Client requests HTTP/2, server only supports HTTP/1.1

```bash
curl -v --http2 http://127.0.0.1:8080/hello 2>&1 | grep '< HTTP/'
# Expected: "< HTTP/1.1 200 OK" (graceful fallback)
```

### Client requests HTTP/3, falls back to HTTP/2

```bash
curl -v -k --http3 https://127.0.0.1:8443/hello 2>&1 | grep 'using HTTP'
# If HTTP/3 unavailable, should fall back to HTTP/2 or HTTP/1.1
```

### Alt-Svc discovery flow

```bash
# Step 1: HTTP/1.1 request, discover Alt-Svc
curl -k -s -D - -o /dev/null https://127.0.0.1:8443/hello | grep -i alt-svc
# Expected: alt-svc: h3=":8443"; ma=86400

# Step 2: HTTP/3 request using discovered endpoint
curl -k --http3 https://127.0.0.1:8443/hello
```

### Protocol version comparison test

Run the same request across all three protocols and compare:

```bash
echo "=== HTTP/1.1 ==="
curl -s -o /dev/null -w 'Status: %{http_code}\nVersion: %{http_version}\nTime: %{time_total}s\n' http://127.0.0.1:8080/hello

echo "=== HTTP/2 ==="
curl -s -k -o /dev/null -w 'Status: %{http_code}\nVersion: %{http_version}\nTime: %{time_total}s\n' --http2 https://127.0.0.1:8443/hello

echo "=== HTTP/3 ==="
curl -s -k -o /dev/null -w 'Status: %{http_code}\nVersion: %{http_version}\nTime: %{time_total}s\n' --http3 https://127.0.0.1:8443/hello
```

All three should return the same status code and body, with potentially different timing.

---

## Benchmarking

### h2load (HTTP/2 benchmark tool)

```bash
# 10000 requests, 100 clients, 10 concurrent streams per connection
h2load -n 10000 -c 100 -m 10 https://127.0.0.1:8443/hello

# Compare HTTP/1.1 vs HTTP/2
h2load -n 10000 -c 100 --h1 https://127.0.0.1:8443/hello    # HTTP/1.1
h2load -n 10000 -c 100 -m 10 https://127.0.0.1:8443/hello   # HTTP/2
```

### wrk (general HTTP benchmark)

```bash
# HTTP/1.1 benchmark
wrk -t4 -c100 -d10s http://127.0.0.1:8080/hello
```

### .NET HttpClient benchmark

```csharp
// Simple C# benchmark for HTTP/2 multiplexing
var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
var client = new HttpClient(handler);

var sw = Stopwatch.StartNew();
var tasks = Enumerable.Range(0, 1000).Select(_ =>
{
    var req = new HttpRequestMessage(HttpMethod.Get, "https://127.0.0.1:8443/hello");
    req.Version = new Version(2, 0);
    return client.SendAsync(req);
}).ToArray();

await Task.WhenAll(tasks);
Console.WriteLine($"1000 HTTP/2 requests in {sw.ElapsedMilliseconds}ms");
```

### Browser Alt-Svc Interop Harness

The repository includes a browser interoperability harness in `src/Test.BrowserInterop`.

```bash
dotnet run --project src/Test.BrowserInterop/Test.BrowserInterop.csproj --framework net10.0 -c Release
```

Optional explicit browser path:

```bash
dotnet run --project src/Test.BrowserInterop/Test.BrowserInterop.csproj --framework net10.0 -c Release -- --browser "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
```

What it checks:

```text
[ ] A real Chromium browser receives Alt-Svc from Watson
[ ] Browser-side passive Alt-Svc HTTP/3 promotion is attempted using a persistent browser profile
[ ] Browser-side forced HTTP/3 navigation can be attempted independently of Alt-Svc promotion
```

Browser certificate model:

```text
The browser harness uses a dedicated loopback hostname and a temporary per-run certificate.
It pins that certificate directly into Chromium command-line trust overrides, so it does not depend on `dotnet dev-certs https --trust`.
```

Current interpretation:

```text
If Alt-Svc header validation passes but passive browser promotion still stays on h2, the remaining issue is browser-side HTTP/3 interoperability or discovery behavior rather than missing certificate trust setup.
If forced HTTP/3 navigation fails, the remaining issue is broader browser-to-Watson HTTP/3 interoperability rather than passive Alt-Svc discovery alone.
```

---

## Troubleshooting

### HTTP/2

| Problem | Cause | Fix |
|---------|-------|-----|
| `curl: (56) Failure when receiving data from the peer` | HTTP/2 frame error or TLS issue | Check server logs; try `curl -v` for details |
| `ALPN, server did not agree to a protocol` | Server not configured for HTTP/2 | Enable HTTP/2 in settings; ensure TLS is configured |
| Protocol stays HTTP/1.1 despite `--http2` | ALPN negotiation fell back | Server may not support HTTP/2; check with `-v` |
| `HTTP/2 stream 0 was not closed cleanly` | Stream error | Check for exceptions in route handler |
| `nghttp: error: HTTP/2 protocol error` | Frame format error | May indicate server bug; check frame parser |

### HTTP/3

| Problem | Cause | Fix |
|---------|-------|-----|
| `curl: (7) Failed to connect: connection refused` | UDP port blocked or QUIC not listening | Check firewall allows UDP; verify server is listening |
| `QUIC: not supported` | curl built without HTTP/3 | Install curl with HTTP/3 support or use newer version |
| `curl: (95) QUIC: connection refused` | Server not accepting QUIC connections | Ensure HTTP/3 is enabled in settings |
| No `alt-svc` header in response | Alt-Svc not configured | Enable `AltSvcSettings` in server settings |
| HTTP/3 works locally but not remotely | Firewall blocks UDP | Open UDP port in addition to TCP |
| `SSL certificate problem` | QUIC requires valid TLS 1.3 | Use `-k` for testing or install valid cert |

### General

| Problem | Cause | Fix |
|---------|-------|-----|
| `Invoke-WebRequest: The response ended prematurely` | PowerShell version too old | Use PowerShell 7+ for HTTP/2, 7.4+ for HTTP/3 |
| Status 000 or connection reset | Server crashed | Check server console/logs for exceptions |
| Correct status but empty body | HEAD request or response not sent | Verify route handler calls `Send()` |
| Headers missing | Response sent before headers set | Check header ordering in route handler |

## HTTP/2 resilience regressions

`Test.Shared/SharedHttp2ResilienceTests.cs` registers the `Http2Resilience` suite with the CLI, xUnit, and NUnit runners. It uses raw loopback HTTP/2 frames with ten-second deadlines and TLS ALPN checks. Run the full shared suite with:

```sh
dotnet run --project src/Test.Automated -c Debug -f net10.0 -- --results results.json
dotnet test src/Test.XUnit -c Debug
dotnet test src/Test.Nunit -c Debug
```

Coverage includes phantom PRIORITY streams, extension frames, invalid framing, writer cancellation before and after commit, transport faults, reset isolation over h2c and TLS, default-window byte-array/stream responses, exact connection-credit refunds, HPACK synchronization after resets, bounded reset history, rapid resets, and multiplexing soak tests. `Http2Golden/` fixtures were captured from 7.3.1 for small, multi-frame, CONTINUATION, chunked, trailer, and 4 MB in-window responses. Only the variable Date header value is normalized; frame types, flags, lengths, normalized HPACK bytes, and DATA payload hashes are compared.

The issue #197 standalone repro was run against local 7.3.1 and patched builds over both h2c and TLS. Baseline: A passed and B through F failed. Patched: A through F all passed. Release evidence and HTTP/3 audit details are recorded in the archived issue work order.

HTTP/3 audit gaps are tracked in issue #200 and are outside this HTTP/2 patch. This release does not advertise SETTINGS_NO_RFC7540_PRIORITIES.

### Closed-stream policy and RFC coverage

Watson records the reason for each of the most recent 128 stream closures. DATA on a known normally closed or client-reset stream receives GOAWAY(STREAM_CLOSED). DATA on a half-closed remote stream receives RST_STREAM(STREAM_CLOSED), leaving sibling requests usable. Late frames following a server reset receive minimal processing: DATA includes padding in connection flow-control accounting, and HEADERS update HPACK state before being discarded. Once closure history is evicted, Watson conservatively uses that same minimal processing, rather than risking a connection error for frames still in flight.

[RFC 9113 section 5.1](https://www.rfc-editor.org/rfc/rfc9113.html#section-5.1) permits minimal processing for all closed streams; the stricter response for known normal/client-reset closures is a compatibility policy. The active half-closed remote stream error is required by sections 5.1 and 6.1. There are no timer-based reset exemptions.

Ten additional shared cases cover normal closure, peer reset, half-closed remote state, and stream-zero DATA over h2c and TLS, plus bounded-history classification and HPACK synchronization after eviction. All four previously failing h2spec DATA checks now pass on both transports. h2spec 2.6.0 remains based on RFC 7540: its self-dependent PRIORITY rejection check is not required by RFC 9113, which deprecates those priority semantics. The subsequent protocol-validation fixes and their results are described below; passing this suite does not establish complete HTTP/2 conformance.

### RFC-aware h2spec interpretation

The shared `Http2Resilience` suite includes six explicit RFC 9113 cases for self-dependent priority signals: idle PRIORITY, open-stream PRIORITY, and priority metadata in HEADERS, each over h2c and TLS. Each case requires a correct 200 response, no reset or GOAWAY, connection reuse, and a PING acknowledgment. The idle-stream case also confirms that a priority-only stream does not prevent opening a lower request stream. Existing malformed-PRIORITY and header-block interleaving checks remain enforced.

Keep h2spec 2.6.0's original JUnit report and exit status. To interpret that report for a particular RFC, run:

```sh
h2spec -h localhost -p 8080 --junit-report artifacts/h2spec-raw.xml
python3 tools/summarize_h2spec.py artifacts/h2spec-raw.xml --rfc 9113 --output artifacts/h2spec-rfc9113.json
python3 tools/summarize_h2spec.py artifacts/h2spec-raw.xml --rfc 7540 --output artifacts/h2spec-rfc7540.json
python3 -m unittest discover -s src/Test.Shared/Http2Conformance -p 'test_*.py' -v
```

The interpreter requires an explicit RFC profile. For RFC 9113, only these two exact h2spec `http2/5.3.1` checks are classified as not applicable: `Sends PRIORITY frame that depend on itself` and `Sends HEADERS frame that depends on itself`. They test RFC 7540's dependency semantics, which RFC 9113 sections [5.3.2](https://www.rfc-editor.org/rfc/rfc9113.html#section-5.3.2) and [6.3](https://www.rfc-editor.org/rfc/rfc9113.html#section-6.3) do not retain. Both checks remain applicable under the RFC 7540 profile.

The JSON preserves every raw case status and raw total, lists separate applicable totals and not-applicable cases with their rationale, and records the SHA-256 of the untouched input. h2spec sometimes embeds XML-invalid control octets in diagnostic text; these are removed only from an in-memory parsing copy, with the count recorded. The interpreter exits nonzero whenever applicable failures remain. Unrelated failures, similarly named tests, and mandatory priority framing checks remain applicable. A not-applicable test is never counted as a pass.

### HTTP/2 protocol-validation follow-up

The shared resilience suite adds 36 checks for oversized DATA/HEADERS/CONTINUATION (rejecting the frame header without waiting for the payload), invalid SETTINGS acknowledgements, idle-stream control frames, invalid lower stream creation, HPACK table-update placement, stream and connection window overflow, incremental SETTINGS, negative stream windows, GOAWAY draining, and dedicated-listener handshakes. Each wire scenario runs over h2c and TLS. Positive checks preserve valid HPACK updates, sibling requests after a stream overflow, existing responses during draining, and valid dedicated-listener handshakes. The suite now contains 109 cases.

After these fixes, h2spec 2.6.0 was run three times on each transport against dedicated HTTP/2 listeners (`EnableHttp1 = false`). Every raw run reports 144 passed and two failed out of 146. Every RFC 9113 interpretation reports 144 passed, zero failed, and zero skipped out of 144 applicable checks, plus the two legacy dependency checks marked N/A. Raw runs still exit 1; the RFC 9113 interpreter exits 0. Raw XML, logs, profile summaries, and exit statuses are retained under `artifacts/http2-197-conformance/`.

Use a dedicated HTTP/2 listener for the malformed-preface conformance check. On a shared cleartext HTTP/1.1 + h2c listener, arbitrary text such as h2spec's `INVALID CONNECTION PREFACE` is routed to the HTTP/1.1 parser; its HTTP error response is not an HTTP/2 frame, so h2spec reports an unexpected EOF. TLS ALPN and dedicated h2c listeners identify HTTP/2 before validating its preface and return PROTOCOL_ERROR. Do not weaken HTTP/1.1 protocol detection to satisfy that ambiguous shared-listener input.

The existing two 32 KB body-access cases retain their byte-for-byte echo assertions. Their request helper now splits uploads into DATA frames within the server's advertised maximum, with END_STREAM only on the final fragment. Previously those tests sent an invalid single 32 KB frame against a 16 KB limit. Oversized-frame rejection is independently covered by the new negative cases.

Final verification for this follow-up: CLI, xUnit, and NUnit on both .NET 8 and .NET 10 each pass 1,243 tests with zero failures/skips. Fresh native AOT builds each pass 43 checks. Debug/Release builds and API package validation against 7.3.1 pass. The original six-scenario reproducer passes over both transports; Firefox 155 and Chrome 155.0.8059.85 pass repeated browser checks. Final runner logs and the initial two invalid-input failures are preserved alongside the conformance reports.
