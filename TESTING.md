# Testing

This repository has two automated test entry points:

- `src/Test.Automated`
- `src/Test.XUnit`

It also includes a benchmark harness:

- `src/Test.Benchmark`

It also includes a native AOT validation application:

- `src/Test.Aot`

It also includes interactive websocket sample applications:

- `src/Test.WebsocketServer`
- `src/Test.WebsocketClient`

## Test.Automated

`Test.Automated` is the primary automated console suite. It contains the migrated legacy coverage plus the added optimization-safety coverage.

Run it with:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

Behavior:

- Prints one normalized pass/fail line per test
- Prints an overall pass/fail summary with total runtime
- Enumerates failed tests at the end when failures occur

### WebSocket coverage in Test.Automated

The websocket shared scenarios are also surfaced through `Test.Automated` via `SharedCoreUnitCoverageSuite`.

Run the full automated suite:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

If you want to inspect the websocket case names first, refer to:

- `src/Test.Shared/SharedWebSocketTests.cs`
- `src/Test.Automated/SharedCoreUnitCoverageSuite.cs`

### Access control and IP matcher coverage

`MatcherDisposal` (`src/Test.Shared/SharedMatcherDisposalTests.cs`) covers IP matcher ownership: replacing `DenyList`, `PermitList`, `TrustedProxies`, `AccessControl`, and `Telemetry` disposes the replaced instance, reassigning the same instance or one still held by the other list does not, and disposing `AccessControlManager`, `TelemetrySettings`, `WebserverSettings`, or `Webserver` disposes the matchers they hold. It also covers the `IpMatcher` cache behavior Watson relies on (subnet matches are cached, `Remove` clears the cache, a disposed matcher still matches without caching) and end-to-end access-control denials (`403` with and without a configured 403 page, and a client denied after its permit entry is removed).

### Routing and UrlMatcher coverage

Parameter and WebSocket route matching is provided by the `UrlMatcher` package. Two groups of suites in `Test.Shared` cover it, and all three runners execute them:

- `CatchAllRouting` (`src/Test.Shared/SharedCatchAllRoutingTests.cs`) exercises catch-all (`{*name}`) routing through Watson: `ParameterRouteManager` and `WebSocketRouteManager` matching, precedence (routes without a catch-all are evaluated before catch-all routes), registration-time rejection of invalid catch-alls, `ParameterRoute` path and method changes, API routes, authenticated routes, `HostBuilder`, OpenAPI output, and end-to-end HTTP and WebSocket requests.
- `UrlMatcher.*` (`src/Test.Shared/UrlMatching/`) is the UrlMatcher library's own test suite (358 cases), copied from the UrlMatcher repository and compiled against the `UrlMatcher` NuGet package Watson references. When the `UrlMatcher` dependency is updated, refresh these files from the UrlMatcher repository's `src/Test.Shared` (namespaces become `Test.Shared.UrlMatching`, and suite ids are prefixed with `UrlMatcher.`).

### Native AOT serialization coverage

`AotSerialization` (`src/Test.Shared/SharedAotSerializationTests.cs`) covers the source-generated serialization path used under native AOT, running it without reflection inside the regular test process. It compares `DefaultSerializationHelper(IJsonTypeInfoResolver)` with the reflection-based serializer for API errors, health results, an application type, and exceptions, and the OpenAPI generator with and without reflection for 3.0, 3.1, and 3.2 documents. It also covers the failure messages for unregistered types, enum handling with and without `UseStringEnumConverter`, and end-to-end API routes, typed bodies, structured errors, timeouts, authentication failures, and health checks through a resolver-configured server. The application types and contexts it uses live in `src/Test.Shared/Aot/`.

## Test.Aot

`src/Test.Aot` validates Watson and `Watson.Clients` as a real native AOT executable, which the in-process runners can't do. It is a separate console application, not part of `WatsonTestSuites.All`, because it has to be compiled by the native AOT toolchain.

A regular build runs the trim and AOT analyzers over the application and both packages with warnings as errors:

```powershell
dotnet build src\Test.Aot\Test.Aot.csproj -c Debug
```

Publishing produces the native executable, which exits `0` when every check passes. Pass `--require-native` to fail when the executable isn't native:

```powershell
dotnet publish src\Test.Aot\Test.Aot.csproj -c Release -f net10.0 -r win-x64 -o artifacts\aot
artifacts\aot\Test.Aot.exe --require-native
```

Use the runtime identifier for your platform (`linux-x64`, `osx-arm64`, and so on), and repeat with `-f net8.0` to cover both runtimes. Native AOT publishing needs the platform's native toolchain (see [AOT.md](AOT.md#publishing)).

The executable starts HTTP/1.1 and HTTP/2 (h2c) servers configured the way an AOT application configures them and checks, over both protocols: plain, parameter, and catch-all routes, API routes returning application types, typed request bodies, malformed JSON, structured errors, timeouts, authentication failures, health checks, OpenAPI, middleware, chunked responses, server-sent events, the Prometheus endpoint, and the default route. It also checks a WebSocket round trip through `Watson.Clients`, the default serializer's fallback when reflection is disabled, and HTTP/3 runtime detection.

## Test.XUnit

`Test.XUnit` is the CI-oriented xUnit runner. It shares reusable logic through `Test.Shared` and does not invoke `Test.Automated`.

The stable execution flow is:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

That script performs these steps:

1. Builds `src/Test.XUnit/Test.XUnit.csproj`
2. Executes `dotnet test --no-build`

If you want console output that shows each xUnit test with its pass/fail result and runtime, run:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj --no-build -c Debug -f net10.0 --logger "console;verbosity=detailed"
```

Notes:

- `verbosity=detailed` is what causes per-test output to appear
- `--no-build` keeps the console output focused on test execution if the project is already built
- omit `--no-build` if you want `dotnet test` to build first

### WebSocket shared coverage

The current websocket shared scenarios are surfaced through `Test.XUnit` and `Test.Automated`.

For the focused websocket-oriented xUnit slice:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj -c Debug --filter SharedCoreUnitCasePasses
```

For a detailed console listing of each websocket shared case:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj --no-build -c Debug -f net10.0 --filter SharedCoreUnitCasePasses --logger "console;verbosity=detailed"
```

## Test.Benchmark

`Test.Benchmark` is the performance harness used to compare Watson 7 against Watson 6, WatsonLite6, and Kestrel across supported protocols and scenarios.

Run it with:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets all --protocols http1,http2,http3 --scenarios hello,json
```

Behavior:

- Prints one formatted line per benchmark combination during the live run
- Prints a summary table after completion
- Prints protocol comparison tables after the summary

Useful examples:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1 --scenarios hello,json --warmup-seconds 2 --duration-seconds 5 --concurrency 16
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets all --protocols http3 --scenarios echo,json-echo --warmup-seconds 2 --duration-seconds 5 --concurrency 16
```

Websocket benchmark examples:

```powershell
dotnet run --framework net10.0 --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1 --scenarios websocket-echo --warmup-seconds 1 --duration-seconds 1 --concurrency 2
dotnet run --framework net10.0 --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1 --scenarios websocket-connect-close,websocket-client-text,websocket-server-text --warmup-seconds 1 --duration-seconds 1 --concurrency 2
```

Notes:

- `Watson6`, `WatsonLite6`, and `Kestrel` availability depends on the selected protocol and scenario
- some combinations are intentionally skipped when the harness does not support them
- HTTP/3 benchmarking depends on local QUIC support
- WebSocket benchmarking currently targets Watson 7 on HTTP/1.1 only
- Current websocket scenarios include echo/request-reply, connect-close, client-to-server text with ack, and server-to-client text after a trigger

## WebSocket Sample Apps

`Test.WebsocketServer` is a menu-driven websocket host for manual validation. It exposes HTTP routes plus websocket routes and allows:

- listing active websocket clients
- kicking a connected client
- sending one or many unsolicited messages to a selected client
- sending a message to all connected clients

Run it with:

```powershell
dotnet run --project src\Test.WebsocketServer\Test.WebsocketServer.csproj
```

`Test.WebsocketClient` is a menu-driven websocket client for manual validation. It supports:

- endpoint presets and custom URIs
- custom request headers
- requested subprotocols
- text, burst-text, and binary sends
- explicit close with status and reason
- background receive logging

Run it with:

```powershell
dotnet run --project src\Test.WebsocketClient\Test.WebsocketClient.csproj
```

## Environment Notes

WebSocket coverage in the current repository is primarily validated against loopback `ws://` paths.

Important notes:

- `wss://` manual validation requires a certificate and SSL-enabled Watson configuration
- HTTP/2 and HTTP/3 websocket runtime scenarios are not yet implemented
- Browser-oriented websocket validation is still pending; current automated validation uses `ClientWebSocket`
- HTTP/3 benchmarks and tests depend on local QUIC support

## Supporting Scripts

### `src/Test.XUnit/Run-Test.XUnit.ps1`

Wrapper for the full stable xUnit flow.

## Recommended Usage

For direct coverage validation during development:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

For CI-style xUnit validation in this repository:

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

For local xUnit runs where you want to see each test result and elapsed time:

```powershell
dotnet test src\Test.XUnit\Test.XUnit.csproj --no-build -c Debug -f net10.0 --logger "console;verbosity=detailed"
```

For native AOT validation (see [Test.Aot](#testaot)):

```powershell
dotnet publish src\Test.Aot\Test.Aot.csproj -c Release -f net10.0 -r win-x64 -o artifacts\aot
artifacts\aot\Test.Aot.exe --require-native
```

For local performance validation:

```powershell
dotnet run --project src\Test.Benchmark\Test.Benchmark.csproj -- --targets watson7 --protocols http1,http2,http3 --scenarios hello,json,echo,json-echo
```
