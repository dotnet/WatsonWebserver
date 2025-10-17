# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Watson Webserver is a simple, scalable, fast, async C# web server for processing RESTful HTTP/HTTPS requests. The project consists of three main packages:

- **Watson** - Main webserver that operates on top of `http.sys`
- **Watson.Lite** - Webserver without `http.sys` dependency, using TCP implementation via CavemanTcp
- **Watson.Core** - Core library shared by both Watson and Watson.Lite

## Architecture

The solution is organized into:

- `WatsonWebserver/` - Main Watson package (depends on http.sys)
- `WatsonWebserver.Lite/` - Watson.Lite package (TCP-based, no http.sys dependency)
- `WatsonWebserver.Core/` - Shared core functionality
- `Test.*` directories - Various test/example projects demonstrating different features

### Core Architecture Pattern

**Three-Layer Design:**
1. **WatsonWebserver.Core** - Contains ALL abstract base classes (`WebserverBase`, `HttpContextBase`, `HttpRequestBase`, `HttpResponseBase`) and shared routing logic. This is pure abstraction with no transport-specific code.
2. **WatsonWebserver** - Concrete implementation using .NET's `HttpListener` (wraps http.sys kernel driver). High performance, kernel-level HTTP parsing.
3. **WatsonWebserver.Lite** - Concrete implementation using `CavemanTcpServer`. User-space TCP with manual HTTP parsing. No admin privileges required.

**Class Hierarchy:**
```
WebserverBase (abstract)
├── Webserver (wraps HttpListener/http.sys)
└── WebserverLite (wraps CavemanTcpServer)

HttpResponseBase (abstract)
├── HttpResponse (Watson - wraps HttpListenerResponse)
└── HttpResponse (Lite - writes to TCP stream)
```

Both implementations follow the **Template Method Pattern** - `WebserverBase` defines the routing pipeline, concrete classes only override transport-specific operations.

### Request/Response Lifecycle

1. **Connection received** → Creates `HttpContextBase` wrapper
2. **Routing pipeline executes** (see order below)
3. **Route handler sends response** via `ctx.Response.Send*()`
4. **Statistics updated** and connection closed

**Critical: Each route MUST send a response or throw an exception. If no response is sent, a default 500 error is returned.**

### Routing Priority Order (MEMORIZE THIS)

Watson routes requests through a **multi-stage pipeline** in this exact order:

1. **`.Preflight`** - Runs ONLY for OPTIONS requests
2. **`.PreRouting`** - Always runs before routing. Can short-circuit by sending response.
3. **`.PreAuthentication`** routing group (checks in order):
   - `.Static` - Exact URL + method match (Dictionary lookup, O(1))
   - `.Content` - File serving (GET/HEAD only)
   - `.Parameter` - URL variables like `/users/{id}` (parameters in `ctx.Request.Url.Parameters["id"]`)
   - `.Dynamic` - Regex-based matching (most flexible, slowest)
4. **`.AuthenticateRequest`** - Authentication demarcation point. Attach identity to `ctx.Metadata`.
5. **`.PostAuthentication`** - Identical structure to PreAuthentication (Static/Content/Parameter/Dynamic)
6. **`.Default`** - **REQUIRED** catch-all route (typically 404 handler)
7. **`.PostRouting`** - Always runs (even on errors) for logging/telemetry. **DO NOT send response here** - response already sent!

**Route Matching:**
- Within each route group, **first match wins**
- Static routes checked before parameter routes, parameter before dynamic
- Each route can have an optional exception handler: `server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/path", handler, exceptionHandler)`

### Response Patterns (Mutually Exclusive)

**Normal Response:**
```csharp
await ctx.Response.Send("data");
```

**Chunked Transfer Encoding** (streaming large responses):
```csharp
ctx.Response.ChunkedTransfer = true;  // Set BEFORE any Send call
await ctx.Response.SendChunk(data, isFinal: false);
await ctx.Response.SendChunk(lastData, isFinal: true);
```

**Server-Sent Events** (SSE):
```csharp
ctx.Response.ServerSentEvents = true;  // Set BEFORE any Send call
ServerSentEvent evt = new ServerSentEvent { Data = "message", Event = "update", Id = "123" };
await ctx.Response.SendEvent(evt, isFinal: false);
```

**Reading Chunked Requests:**
```csharp
if (ctx.Request.ChunkedTransfer)
{
    while (true)
    {
        Chunk chunk = await ctx.Request.ReadChunk(ctx.Token);
        // Process chunk.Data (byte[])
        if (chunk.IsFinalChunk) break;
    }
}
```

### HttpContext Pipeline

- All requests flow through `HttpContextBase` which provides unified access to request/response
- Routes are async methods: `Func<HttpContextBase, Task>`
- Context properties:
  - `ctx.Request` - HTTP request (headers, body, URL, method)
  - `ctx.Response` - HTTP response (send data, set headers)
  - `ctx.Token` - `CancellationToken` for graceful shutdown
  - `ctx.Metadata` - `Dictionary<string, object>` for passing data between routes (e.g., authentication info)

## Build and Development Commands

### Building
```bash
# Build entire solution
dotnet build WatsonWebserver.sln

# Build in Release mode
dotnet build WatsonWebserver.sln -c Release

# Build specific project
dotnet build WatsonWebserver/WatsonWebserver.csproj
```

### Running Test Projects
```bash
# Run test projects (need to specify framework due to multi-targeting)
dotnet run --project src/Test.Default --framework net8.0
dotnet run --project src/Test.Routing --framework net8.0
dotnet run --project src/Test.ChunkServer --framework net8.0
dotnet run --project src/Test.ServerSentEvents --framework net8.0

# Other available test projects and their purposes:
# Test.Authentication - Auth flow and metadata usage
# Test.ChunkServer - Chunked transfer encoding examples
# Test.DataReader - Reading request body data
# Test.Default - Basic server setup and default route
# Test.Docker - Docker containerization
# Test.HeadResponse - HEAD request handling
# Test.HostBuilder - HostBuilder fluent API usage
# Test.Loopback - Localhost-specific features
# Test.MaxConnections - Connection limiting
# Test.Parameters - Parameter route examples ({id} variables)
# Test.Routing - Comprehensive routing examples
# Test.Serialization - JSON serialization helpers
# Test.ServerSentEvents - SSE implementation
# Test.Stream - Streaming request/response
```

### Testing Workflow
1. **Build the Core library first** - changes in Core require rebuilding Watson/Watson.Lite
2. **Run relevant test project** to verify behavior
3. **Test both Watson and Watson.Lite** if making Core changes - implementations may behave differently

### Package Creation
The projects are configured with `GeneratePackageOnBuild` so NuGet packages are automatically created during build in `bin/[Debug|Release]/`.

## Target Frameworks

All main projects multi-target:
- .NET Standard 2.0, 2.1
- .NET Framework 4.6.2, 4.8
- .NET 6.0, 8.0

Test projects typically target .NET 8.0, 6.0, 4.8, and 4.6.2.

## Watson vs Watson.Lite - Critical Differences

| Feature | Watson (http.sys) | Watson.Lite (TCP) |
|---------|------------------|-------------------|
| **Performance** | High (kernel-level HTTP parsing) | Lower (user-space parsing) |
| **Admin Privileges** | Required for non-localhost bindings | Not required |
| **Binding** | Hostname/IP, must use prefix format | IP addresses only |
| **HOST Header** | Must match binding | No requirement to match |
| **SSL Certificate** | Uses OS certificate store | Requires `X509Certificate2` object or file |
| **HTTP Parsing** | Automatic via http.sys | Manual parsing from TCP stream |
| **Best For** | Production servers, high traffic | Embedded apps, cross-platform, restricted environments |

**When to Use Each:**
- Use **Watson** for production web servers where you have admin rights and need maximum performance
- Use **Watson.Lite** for embedded scenarios, Docker containers, or when you can't modify OS settings

## Important Development Notes

### Authentication
- Implement authentication logic in the `.AuthenticateRequest` route
- Store authentication state in `ctx.Metadata` for use in downstream routes
- Pre-authentication routes (`.PreAuthentication`) run before auth check - use for public endpoints
- Post-authentication routes (`.PostAuthentication`) run after auth check - use for protected endpoints

### Response Handling
- **NEVER send response data in `.PostRouting`** - response is already sent, attempt will fail
- Each route must either send a response OR throw an exception
- First call to any `Send*()` method sends HTTP headers automatically
- `ResponseSent` flag prevents duplicate responses

### Access Control
- Default mode: `AccessControlMode.DefaultPermit` (allow all, deny specific IPs)
- Restrictive mode: `AccessControlMode.DefaultDeny` (deny all, allow specific IPs)
- Add rules: `server.Settings.AccessControl.DenyList.Add(ipAddress, netmask)`
- Permit rules: `server.Settings.AccessControl.PermitList.Add(ipAddress, netmask)`

### Performance Considerations
- Static routes are fastest (O(1) dictionary lookup)
- Parameter routes are fast (pattern matching with extraction)
- Dynamic routes are slowest (regex evaluation on every request)
- Watson.Lite is generally 20-40% less performant than Watson due to user-space HTTP implementation
- Use `Settings.IO.StreamBufferSize` to tune I/O performance (default: 65536 bytes)

### Streaming and Events
- **Chunked Transfer Encoding**: Set `ctx.Response.ChunkedTransfer = true` before first `SendChunk()` call
- **Server-Sent Events**: Set `ctx.Response.ServerSentEvents = true` before first `SendEvent()` call
- These modes are mutually exclusive with normal `Send()` - will throw exception if mixed
- SSE automatically sets `Content-Type: text/event-stream; charset=utf-8` and appropriate cache headers

## Example Usage Patterns

Most routes follow this pattern:
```csharp
static async Task MyRoute(HttpContextBase ctx)
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "text/plain";
    await ctx.Response.Send("Response data");
}
```

Parameter routes access URL parameters via:
```csharp
string id = ctx.Request.Url.Parameters["id"];
```

## Key Files and Locations

Understanding where functionality lives helps you make changes efficiently:

### Core Library (WatsonWebserver.Core)
- **`WebserverBase.cs`** - Abstract base class defining server interface and lifecycle
- **`HttpContextBase.cs`** - Request/response container passed through routing pipeline
- **`HttpRequestBase.cs`** - Abstract request interface (headers, body, URL parsing)
- **`HttpResponseBase.cs`** - Abstract response interface (Send methods, headers)
- **`WebserverRoutes.cs`** - Route collection root (Preflight, PreRouting, etc.)
- **`RoutingGroup.cs`** - Holds Pre/PostAuthentication route groups
- **`StaticRoute.cs`, `ParameterRoute.cs`, `DynamicRoute.cs`, `ContentRoute.cs`** - Route type implementations
- **`ServerSentEvent.cs`** - SSE data model with `ToEventString()` formatting
- **`Chunk.cs`** - Chunked transfer encoding data model
- **`WebserverSettings.cs`** - Configuration (IO, SSL, AccessControl, Headers, Debug)
- **`WebserverStatistics.cs`** - Request counters and bandwidth tracking

### Watson Implementation (WatsonWebserver)
- **`Webserver.cs`** - Main implementation using `HttpListener`
- **`HttpContext.cs`**, **`HttpRequest.cs`**, **`HttpResponse.cs`** - Concrete wrappers around `HttpListenerContext`

### Watson.Lite Implementation (WatsonWebserver.Lite)
- **`WebserverLite.cs`** - TCP-based implementation using `CavemanTcpServer`
- **`HttpContext.cs`**, **`HttpRequest.cs`**, **`HttpResponse.cs`** - Manual HTTP parsing implementations

### When Making Changes

**Adding a new feature:**
1. Add abstract method/property to base class in Core (e.g., `HttpResponseBase`)
2. Implement in both Watson and Watson.Lite concrete classes
3. Add tests to appropriate Test.* project
4. Update XML documentation

**Fixing a bug:**
1. Identify if bug is in Core (shared logic) or implementation-specific
2. Check if bug affects both Watson and Watson.Lite
3. Add/update test case to prevent regression

**Changing routing logic:**
- Routing pipeline is in `WebserverBase` (shared by both implementations)
- Route managers (Static, Parameter, Dynamic, Content) are in Core
- Changes affect both Watson and Watson.Lite automatically

## Docker Support

The project includes Docker support - see `Test.Docker/Docker.md` for detailed instructions. Key requirement: must run containers with `--user root` due to HttpListener restrictions.

## Common Issues and Debugging

### Route Not Matching
1. **Check route order** - Static routes are checked before Parameter routes, which are checked before Dynamic routes
2. **Verify route group** - Are you adding to PreAuthentication or PostAuthentication?
3. **Enable debug logging**: `server.Settings.Debug.Routing = true;`
4. **Check HTTP method** - Routes are method-specific (GET ≠ POST)

### "Access Denied" or Permission Errors (Watson)
- **Windows**: Run as administrator OR add URL ACL: `netsh http add urlacl url=http://hostname:port/ user=everyone listen=yes`
- **Linux/Mac**: Use `sudo` for ports < 1024 OR bind to 127.0.0.1
- **Alternative**: Use Watson.Lite which doesn't require admin privileges

### Response Not Sent / 500 Error
- **Every route MUST send a response or throw** - check that you're calling `await ctx.Response.Send(...)`
- **Check for exceptions** - Add exception handlers to routes or check server logs
- **PostRouting issue** - Never send response in PostRouting (response already sent)

### Chunked Transfer / SSE Not Working
- **Set flag BEFORE first Send call**: `ctx.Response.ChunkedTransfer = true` or `ctx.Response.ServerSentEvents = true`
- **Don't mix modes** - Cannot use `Send()` and `SendChunk()` in same response
- **Watson.Lite**: Ensure you're calling `SendChunk(..., isFinal: true)` to close connection

### Performance Issues
1. **Use static routes** instead of dynamic (regex) routes when possible
2. **Check route count** - Too many dynamic routes slow down matching
3. **Consider Watson instead of Watson.Lite** for production (20-40% faster)
4. **Tune buffer size**: `server.Settings.IO.StreamBufferSize = 131072;`
5. **Monitor statistics**: `server.Statistics.ReceivedPayloadBytes` and request counts

### SSL/TLS Issues
- **Watson**: Certificate must be in OS certificate store, bound to port using `netsh`
- **Watson.Lite**: Provide certificate directly: `settings.Ssl.PfxCertificateFile = "cert.pfx";`
- **Common error**: "The credentials supplied to the package were not recognized" - certificate not found or incorrect

### Connection Limits
- **Set max connections**: `server.Settings.IO.MaxRequests = 2048;`
- **Check current count**: `server.RequestCount`
- When limit reached, new connections wait or are rejected

## Coding Standards and Style Rules

**THESE RULES MUST BE FOLLOWED STRICTLY:**

### File Organization and Namespaces
- Namespace declaration must be at the top
- Using statements must be contained INSIDE the namespace block
- Microsoft and standard system library usings first, in alphabetical order
- Other using statements follow, in alphabetical order
- Limit each file to exactly one class or exactly one enum
- No nested classes or enums in a single file

### Code Documentation
- All public members, constructors, and public methods MUST have XML documentation
- NO code documentation on private members or private methods
- Document which exceptions public methods can throw using `/// <exception>` tags
- Document nullability, default values, minimum/maximum values in XML comments
- Document thread safety guarantees in XML comments
- Specify what different values mean or their effects

### Variable and Member Naming
- Private class member variables must start with underscore and be PascalCased: `_FooBar` (NOT `_fooBar`)
- Do NOT use `var` - always use the actual type when defining variables
- All public members should have explicit getters/setters with backing variables when validation is needed

### Async Programming
- Async calls should use `.ConfigureAwait(false)` where appropriate
- Every async method should accept a `CancellationToken` parameter unless:
  - The class has a `CancellationToken` as a class member, OR
  - The class has a `CancellationTokenSource` as a class member
- Check for cancellation at appropriate places in async methods
- When implementing methods returning `IEnumerable`, also create async variants with `CancellationToken`

### Exception Handling
- Use specific exception types rather than generic `Exception`
- Always include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`

### Resource Management and Disposal
- Implement `IDisposable`/`IAsyncDisposable` when holding unmanaged resources or disposable objects
- Use `using` statements or `using` declarations for `IDisposable` objects
- Follow the full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### Null Safety and Validation
- Use nullable reference types (enable `<Nullable>enable</Nullable>` in project files)
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+ or manual null checks
- Document nullability in XML comments
- Proactively eliminate situations where null might cause exceptions

### LINQ and Collections
- Prefer LINQ methods over manual loops when readability is not compromised
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration issues - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()` when element might not exist

### Threading and Concurrency
- Use `Interlocked` operations for simple atomic operations
- Prefer `ReaderWriterLockSlim` over `lock` for read-heavy scenarios

### Configuration and Extensibility
- Avoid hardcoded constant values for things developers may want to configure
- Use public members with backing private members set to reasonable defaults
- Make behavior configurable rather than fixed

### Prohibited Patterns
- **DO NOT** use tuples unless absolutely necessary
- **DO NOT** make assumptions about opaque class members/methods - ask for implementations
- **DO NOT** assume SQL string preparation is wrong - there may be good reasons

### General Principles
- Consider using the Result pattern or Option/Maybe types for methods that can fail
- Regions are NOT required for files under 500 lines