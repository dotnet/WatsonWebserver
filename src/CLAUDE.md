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

### Key Architectural Concepts

**Routing Priority Order:**
Watson always routes in this specific order:
1. `.Preflight` - handling preflight requests (OPTIONS)
2. `.PreRouting` - always invoked before routing determination
3. `.PreAuthentication` routing group:
   - `.Static` - exact URL matches
   - `.Content` - file serving routes
   - `.Parameter` - routes with variables like `/user/{id}`
   - `.Dynamic` - regex-based routes
4. `.AuthenticateRequest` - authentication demarcation
5. `.PostAuthentication` - same structure as PreAuthentication
6. `.Default` - catch-all route
7. `.PostRouting` - always invoked for logging/telemetry

**HttpContext Pipeline:**
- All requests flow through `HttpContextBase` which provides unified access to request/response
- Routes are async methods that accept `HttpContextBase ctx`
- Responses are sent via `ctx.Response.Send()`

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
dotnet run --project Test.Default --framework net8.0
dotnet run --project Test.Routing --framework net8.0
dotnet run --project Test.ChunkServer --framework net8.0

# Other available test projects:
# Test.Authentication, Test.DataReader, Test.Docker, Test.HeadResponse
# Test.HostBuilder, Test.Loopback, Test.MaxConnections, Test.Parameters
# Test.Serialization, Test.ServerSentEvents, Test.Stream
```

### Package Creation
The projects are configured with `GeneratePackageOnBuild` so NuGet packages are automatically created during build.

## Target Frameworks

All main projects multi-target:
- .NET Standard 2.0, 2.1
- .NET Framework 4.6.2, 4.8
- .NET 6.0, 8.0

Test projects typically target .NET 8.0, 6.0, 4.8, and 4.6.2.

## Watson vs Watson.Lite Key Differences

- **Watson**: Uses `http.sys`, requires administrative privileges for non-localhost bindings, HOST header must match binding
- **Watson.Lite**: Pure TCP implementation, no `http.sys` dependency, must bind to IP addresses (not hostnames), HOST header matching not required

## Important Development Notes

- When working with authentication, implement in `.AuthenticateRequest` route
- Never send data in `.PostRouting` if response already sent (will fail)
- For access control, use `Server.AccessControl.DenyList` or set `DefaultDeny` mode with `PermitList`
- Watson.Lite generally less performant than Watson due to user-space HTTP implementation
- Use chunked transfer encoding for streaming large responses
- Server-sent events supported via `ctx.Response.ServerSentEvents = true`

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

## Docker Support

The project includes Docker support - see `Test.Docker/Docker.md` for detailed instructions. Key requirement: must run containers with `--user root` due to HttpListener restrictions.

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