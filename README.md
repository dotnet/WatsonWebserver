![Watson](https://github.com/jchristn/WatsonWebserver/blob/master/assets/watson.ico)

# Watson Webserver

Watson 7 is a simple, fast, async C# web server for building REST APIs and HTTP services with a unified programming model across HTTP/1.1, HTTP/2, and HTTP/3.

| Package | NuGet Version | Downloads |
|---|---|---|
| Watson | [![NuGet Version](https://img.shields.io/nuget/v/Watson.svg?style=flat)](https://www.nuget.org/packages/Watson/) | [![NuGet](https://img.shields.io/nuget/dt/Watson.svg)](https://www.nuget.org/packages/Watson) |
| Watson.Core | [![NuGet Version](https://img.shields.io/nuget/v/Watson.Core.svg?style=flat)](https://www.nuget.org/packages/Watson.Core/) | [![NuGet](https://img.shields.io/nuget/dt/Watson.Core.svg)](https://www.nuget.org/packages/Watson.Core) |

Special thanks to @DamienDennehy for allowing use of the `Watson.Core` package name in NuGet.

## .NET Foundation

This project is part of the [.NET Foundation](http://www.dotnetfoundation.org/projects).

## What Is New In 7.0

Watson 7 is a major consumer-facing release:

- Substantial performance improvements through hot-path optimization
- Native protocol selection through `WebserverSettings.Protocols`; HTTP/1.1, HTTP/2, and HTTP/3 support
- Runtime validation for unsupported protocol combinations
- HTTP/3 runtime normalization when QUIC is unavailable
- Alt-Svc support for advertising HTTP/3 endpoints
- Shared request and response semantics across protocols
- Built-in OpenAPI 3.0 document generation and Swagger UI
- Expanded automated coverage through `Test.Automated` and `Test.XUnit`

Refer to [CHANGELOG.md](CHANGELOG.md) for the full release history.

## Performance

Kestrel was treated as the gold standard throughout the 7.0 performance program, and that remains the benchmark Watson is chasing. The goal is not to pretend Watson has already reached Kestrel performance parity, because it hasn't. The goal is to keep closing the gap in throughput, response time, and requests per second while preserving Watson's programming model and correctness.

The important 7.0 story is that Watson has improved dramatically:

- Watson 6 is the real starting point for the 7.0 performance story. In a current short local validation run from this repository, Watson 6 delivered `437 req/s` on HTTP/1.1 `hello` and `330 req/s` on HTTP/1.1 `json`, while Watson 7 delivered `31,577 req/s` and `36,021 req/s` on the same scenarios.
- After the architectural jump from Watson 6 to Watson 7, the optimization plan then started from a Watson 7 sustained HTTP/1.1 baseline of roughly `~25k req/s` on `hello` and `~17k req/s` on `json`, against a Kestrel reference point of roughly `~146k/~139k`.
- Across the 7.0 optimizations, Watson 7 repeatedly moved into the `~80k-95k req/s` range on common HTTP/1.1 paths during longer benchmark runs, with corresponding latency improvements on the retained changes.
- Those short-run numbers are environment-sensitive, but they still illustrate the magnitude of the architectural jump from the legacy Watson 6 host to Watson 7.

Just as important as the raw benchmark gains: Watson 7 no longer depends on `http.sys`. Watson 6 relied on the operating system HTTP stack. Watson 7 now runs on Watson-owned transport paths built around `TcpListener` for HTTP/1.1 and HTTP/2, plus `QuicListener` for HTTP/3 where available. That shift is foundational. It gives the library direct control over the hot path, which is why the retained 7.0 optimizations were possible at all and why there is still a realistic path to continue narrowing the gap to Kestrel over time.

In summary:

- Kestrel is still ahead overall and remains the standard against which we compare
- Watson 7 is dramatically ahead of Watson 6
- Watson 7 established the transport and protocol architecture needed to keep improving from here

## Install

```powershell
dotnet add package Watson
```

If you are building extensions or shared components on top of the common abstractions:

```powershell
dotnet add package Watson.Core
```

## Quick Start

```csharp
using System;
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;

public class Program
{
    public static void Main(string[] args)
    {
        WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
        Webserver server = new Webserver(settings, DefaultRoute);

        server.Start();

        Console.WriteLine("Watson listening on http://127.0.0.1:9000");
        Console.ReadLine();

        server.Stop();
        server.Dispose();
    }

    private static async Task DefaultRoute(HttpContextBase ctx)
    {
        await ctx.Response.Send("Hello from Watson 7");
    }
}
```

Then browse to `http://127.0.0.1:9000/`.

## Protocol Support

### Default behavior

By default:

- HTTP/1.1 is enabled
- HTTP/2 is disabled
- HTTP/3 is disabled

### Protocol matrix

| Protocol | Package | Notes |
|---|---|---|
| HTTP/1.1 | `Watson` | Enabled by default |
| HTTP/2 over TLS | `Watson` | Supported |
| HTTP/2 cleartext prior knowledge (`h2c`) | `Watson` | Supported only when explicitly enabled |
| HTTP/3 over TLS/QUIC | `Watson` | Supported when QUIC is available |

### Important rules

Watson validates protocol settings before startup:

- At least one protocol must be enabled
- HTTP/2 without TLS requires `EnableHttp2Cleartext = true`
- HTTP/2 cleartext support is prior-knowledge mode, not opportunistic upgrade mode
- HTTP/3 requires TLS
- `AltSvc.Enabled` requires HTTP/3 to be enabled
- If HTTP/3 is enabled but QUIC is unavailable at runtime, Watson disables HTTP/3 and Alt-Svc for that server start

### Enable HTTP/2 over TLS

```csharp
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;

WebserverSettings settings = new WebserverSettings("localhost", 8443, true);
settings.Ssl.PfxCertificateFile = "server.pfx";
settings.Ssl.PfxCertificatePassword = "password";
settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = true;

Webserver server = new Webserver(settings, DefaultRoute);
server.Start();

static async Task DefaultRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("HTTP/1.1 and HTTP/2 enabled");
}
```

### Enable HTTP/2 cleartext prior knowledge

```csharp
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;

WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080);
settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = true;
settings.Protocols.EnableHttp2Cleartext = true;

Webserver server = new Webserver(settings, DefaultRoute);
server.Start();

static async Task DefaultRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("HTTP/1.1 and h2c prior knowledge enabled");
}
```

### Enable HTTP/3 and Alt-Svc

```csharp
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;

WebserverSettings settings = new WebserverSettings("localhost", 8443, true);
settings.Ssl.PfxCertificateFile = "server.pfx";
settings.Ssl.PfxCertificatePassword = "password";

settings.Protocols.EnableHttp1 = true;
settings.Protocols.EnableHttp2 = true;
settings.Protocols.EnableHttp3 = true;

settings.AltSvc.Enabled = true;
settings.AltSvc.Http3Alpn = "h3";
settings.AltSvc.MaxAgeSeconds = 86400;

Webserver server = new Webserver(settings, DefaultRoute);
server.Start();

static async Task DefaultRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("HTTP/1.1, HTTP/2, and HTTP/3 enabled");
}
```

If QUIC is unavailable on the current machine, Watson will disable HTTP/3 and Alt-Svc for that startup rather than advertise a protocol it cannot serve.

## Core Configuration Surface

The primary configuration object is `WebserverSettings`.

Important areas for consumers:

- `Protocols`
  - `EnableHttp1`
  - `EnableHttp2`
  - `EnableHttp3`
  - `EnableHttp2Cleartext`
  - `IdleTimeoutMs`
  - `MaxConcurrentStreams`
  - `Http2`
  - `Http3`
- `AltSvc`
  - `Enabled`
  - `Authority`
  - `Port`
  - `Http3Alpn`
  - `MaxAgeSeconds`
- `IO`
  - `StreamBufferSize`
  - `MaxRequests`
  - `ReadTimeoutMs`
  - `MaxIncomingHeadersSize`
  - `EnableKeepAlive`
  - `MaxRequestBodySize`
  - `MaxHeaderCount`
- `Ssl`
  - `Enable`
  - `SslCertificate`
  - `PfxCertificateFile`
  - `PfxCertificatePassword`
  - `MutuallyAuthenticate`
  - `AcceptInvalidAcertificates`
- `Headers`
  - `DefaultHeaders`
  - `IncludeContentLength`
- `AccessControl`
- `Debug`
- `UseMachineHostname`

### HTTP/3 tuning example

```csharp
WebserverSettings settings = new WebserverSettings("localhost", 8443, true);
settings.Ssl.PfxCertificateFile = "server.pfx";
settings.Ssl.PfxCertificatePassword = "password";

settings.Protocols.EnableHttp3 = true;
settings.Protocols.Http3.MaxFieldSectionSize = 32768;
settings.Protocols.Http3.QpackMaxTableCapacity = 4096;
settings.Protocols.Http3.QpackBlockedStreams = 16;
settings.Protocols.Http3.EnableDatagram = false;
```

## Request Consumption

The most important 7.0 consumption rule is this:

- For protocol-agnostic body handling, use `ctx.Request.Data`, `ReadBodyAsync()`, `DataAsBytes`, or `DataAsString`
- Only use `ReadChunk()` when you are explicitly handling HTTP/1.1 chunked transfer-encoding

### Recommended protocol-agnostic body read

```csharp
private static async Task EchoBody(HttpContextBase ctx)
{
    byte[] body = await ctx.Request.ReadBodyAsync(ctx.Token);

    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "application/octet-stream";
    await ctx.Response.Send(body, ctx.Token);
}
```

### Read as string

```csharp
private static async Task EchoText(HttpContextBase ctx)
{
    byte[] body = await ctx.Request.ReadBodyAsync(ctx.Token);
    string text = ctx.Request.DataAsString;

    ctx.Response.ContentType = "text/plain";
    await ctx.Response.Send("You sent: " + text, ctx.Token);
}
```

After `ReadBodyAsync()`, `DataAsBytes` and `DataAsString` use cached content.

### HTTP/1.1 chunked request bodies

```csharp
private static async Task UploadData(HttpContextBase ctx)
{
    if (ctx.Request.Protocol != HttpProtocol.Http1 || !ctx.Request.ChunkedTransfer)
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.Send("Expected HTTP/1.1 chunked request body", ctx.Token);
        return;
    }

    Boolean finalChunk = false;

    while (!finalChunk)
    {
        Chunk chunk = await ctx.Request.ReadChunk(ctx.Token);
        finalChunk = chunk.IsFinalChunk;

        if (chunk.Data != null && chunk.Data.Length > 0)
        {
            // Process the chunk payload here
        }
    }

    await ctx.Response.Send("Chunked upload complete", ctx.Token);
}
```

`ReadChunk()` is not available for HTTP/2 or HTTP/3 requests and throws if used there.

## Response Patterns

### Simple text or bytes

```csharp
private static async Task GetHello(HttpContextBase ctx)
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "text/plain";
    await ctx.Response.Send("Hello", ctx.Token);
}
```

### Stream a known-length payload

```csharp
using System.IO;

private static async Task DownloadFile(HttpContextBase ctx)
{
    using (FileStream fileStream = new FileStream("large.bin", FileMode.Open, FileAccess.Read))
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/octet-stream";
        await ctx.Response.Send(fileStream.Length, fileStream, ctx.Token);
    }
}
```

### Chunked or streaming response semantics

`SendChunk()` works across supported protocols, but the wire behavior depends on the protocol:

- HTTP/1.1: literal `Transfer-Encoding: chunked`
- HTTP/2 and HTTP/3: streamed response semantics on framed transports

```csharp
private static async Task StreamData(HttpContextBase ctx)
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "text/plain";
    ctx.Response.ChunkedTransfer = true;

    await ctx.Response.SendChunk(System.Text.Encoding.UTF8.GetBytes("part 1\n"), false, ctx.Token);
    await ctx.Response.SendChunk(System.Text.Encoding.UTF8.GetBytes("part 2\n"), false, ctx.Token);
    await ctx.Response.SendChunk(Array.Empty<byte>(), true, ctx.Token);
}
```

### Server-sent events

```csharp
private static async Task SendEvents(HttpContextBase ctx)
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ServerSentEvents = true;

    for (Int32 i = 1; i <= 5; i++)
    {
        ServerSentEvent serverEvent = new ServerSentEvent
        {
            Id = i.ToString(),
            Event = "counter",
            Data = "Event " + i.ToString()
        };

        Boolean isFinal = i == 5;
        await ctx.Response.SendEvent(serverEvent, isFinal, ctx.Token);
    }
}
```

## Routing

Watson routes requests in this order:

- `Preflight`
- `PreRouting`
- `PreAuthentication`
  - `Static`
  - `Content`
  - `Parameter`
  - `Dynamic`
- `AuthenticateRequest`
- `PostAuthentication`
  - `Static`
  - `Content`
  - `Parameter`
  - `Dynamic`
- `Default`
- `PostRouting`

### Route example

```csharp
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;

WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
Webserver server = new Webserver(settings, DefaultRoute);

server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hello", GetHelloRoute);
server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/users/{id}", GetUserRoute);
server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/items/\\d+$"), GetItemRoute);
server.Routes.PreAuthentication.Content.Add("./wwwroot", true);

server.Start();

static async Task GetHelloRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Hello from a static route");
}

static async Task GetUserRoute(HttpContextBase ctx)
{
    String id = ctx.Request.Url.Parameters["id"];
    await ctx.Response.Send("User " + id);
}

static async Task GetItemRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Dynamic route match");
}

static async Task DefaultRoute(HttpContextBase ctx)
{
    ctx.Response.StatusCode = 404;
    await ctx.Response.Send("Not found");
}
```

### Exception handler per route

```csharp
server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/boom", BoomRoute, BoomExceptionRoute);

static async Task BoomRoute(HttpContextBase ctx)
{
    throw new Exception("Whoops");
}

static async Task BoomExceptionRoute(HttpContextBase ctx, Exception e)
{
    ctx.Response.StatusCode = 500;
    await ctx.Response.Send(e.Message);
}
```

## Authentication And Metadata

Authentication is typically implemented in `Routes.AuthenticateRequest`. If authentication succeeds, place user or session data in `ctx.Metadata` and continue. If it fails, send a response there and return.

```csharp
server.Routes.AuthenticateRequest = AuthenticateRequest;

static async Task AuthenticateRequest(HttpContextBase ctx)
{
    if (ctx.Request.RetrieveHeaderValue("X-Api-Key") != "secret")
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.Send("Unauthorized");
        return;
    }

    ctx.Metadata = "authenticated-user";
}
```

Avoid sending a second response from `PostRouting` if a response has already been completed.

## Access Control

By default, Watson permits all inbound connections.

```csharp
server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
server.Settings.AccessControl.DenyList.Add("127.0.0.1", "255.255.255.255");
```

To default-deny and only allow certain addresses:

```csharp
server.Settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
server.Settings.AccessControl.PermitList.Add("192.168.1.0", "255.255.255.0");
```

## HostBuilder

`HostBuilder` offers a fluent setup API over `Webserver`.

```csharp
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;
using WatsonWebserver.Extensions.HostBuilderExtension;

Webserver server = new HostBuilder("127.0.0.1", 8000, false, DefaultRoute)
    .MapStaticRoute(HttpMethod.GET, "/links", GetLinksRoute)
    .MapStaticRoute(HttpMethod.POST, "/login", LoginRoute)
    .MapDefaultRoute(DefaultRoute)
    .Build();

server.Start();

static async Task GetLinksRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Here are your links");
}

static async Task LoginRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Checking credentials");
}

static async Task DefaultRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Hello from the default route");
}
```

## OpenAPI / Swagger

OpenAPI support is built in. No extra package is required beyond `Watson` or `Watson.Core`.

### Enable OpenAPI

```csharp
using WatsonWebserver;
using WatsonWebserver.Core;
using WatsonWebserver.Core.OpenApi;

WebserverSettings settings = new WebserverSettings("localhost", 8080);
Webserver server = new Webserver(settings, DefaultRoute);

server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "My API";
    openApi.Info.Version = "1.0.0";
    openApi.Info.Description = "Example API";
});

server.Start();

static async Task DefaultRoute(HttpContextBase ctx)
{
    await ctx.Response.Send("Hello");
}
```

Endpoints:

- OpenAPI JSON: `/openapi.json`
- Swagger UI: `/swagger`

### Document routes

```csharp
server.Routes.PreAuthentication.Static.Add(
    HttpMethod.GET,
    "/api/users",
    GetUsersHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Get users", "Users")
        .WithDescription("Returns all users")
        .WithParameter(OpenApiParameterMetadata.Query("active", "Active-only filter", false, OpenApiSchemaMetadata.Boolean()))
        .WithResponse(200, OpenApiResponseMetadata.Json(
            "Users",
            OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("User")))));
```

## Hostname Handling

`WebserverSettings.UseMachineHostname` controls the host value Watson uses when composing response host metadata.

- When `Hostname` is `*` or `+`, Watson forces `UseMachineHostname = true`
- For named hosts and concrete addresses, it is disabled by default
- You can opt in manually by setting `UseMachineHostname = true`

Example:

```csharp
WebserverSettings settings = new WebserverSettings("localhost", 9000);
settings.UseMachineHostname = true;

Webserver server = new Webserver(settings, DefaultRoute);
server.Start();
```

## Accessing From Outside Localhost

### Watson

When you bind Watson to `127.0.0.1` or `localhost`, only the local machine can reach it.

To expose Watson externally:

- Bind to the exact DNS hostname, `0.0.0.0`, `*`, or `+` as appropriate
- Ensure the HTTP `Host` header matches the binding constraints that apply to your environment
- Open the firewall port
- Run elevated when the operating system requires it
- Configure URL ACLs or certificate bindings when your OS requires them

On Windows, `netsh http show urlacl` and `netsh http add urlacl ...` are commonly needed when binding outside localhost.

## Operational Notes

- If `Settings.Ssl.Enable` is `true`, configure `SslCertificate` or a PFX file before calling `Start()`
- If you enable HTTP/3, also plan for QUIC runtime availability on the target machine
- If you enable `IO.MaxRequestBodySize`, Watson enforces it against declared request body sizes
- If you enable `IO.MaxHeaderCount`, Watson limits inbound header cardinality
- For long-lived HTTP/1.1 workloads, `IO.EnableKeepAlive` can reduce connection churn
- Debug logging options live under `Settings.Debug`; wire a logger into `server.Events.Logger` if you want to receive those messages

## Running In Docker

Refer to `src/Test.Docker` and its companion documentation.

## Testing

Automated validation is covered by two test projects:

- `src/Test.Automated`: primary console-based automated suite
- `src/Test.XUnit`: xUnit mirror of the same coverage surface

Recommended commands:

```powershell
dotnet run --project src\Test.Automated\Test.Automated.csproj
```

```powershell
powershell -ExecutionPolicy Bypass -File src\Test.XUnit\Run-Test.XUnit.ps1
```

For more detail, refer to [TESTING.md](TESTING.md).

## Special Thanks

Thanks to the contributors who have helped improve Watson Webserver:

- @notesjor @shdwp @Tutch @GeoffMcGrath @jurkovic-nikola @joreg @Job79 @at1993 @MartyIX
- @pocsuka @orinem @deathbull @binozo @panboy75 @iain-cyborn @gamerhost31 @lucafabbri
- @nhaberl @grgouala @sapurtcomputer30 @winkmichael @sqlnew @SaintedPsycho @Return25
- @marcussacana @samisil @Jump-Suit @ChZhongPengCheng33 @bobaoapae @rodgers-r
- @john144 @zedle @GitHubProUser67 @bemoty @bemon @nomadeon @Infiziert90 @kyoybs

## Version History

Refer to [CHANGELOG.md](CHANGELOG.md).
