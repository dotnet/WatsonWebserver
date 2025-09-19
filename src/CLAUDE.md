# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Watson Webserver is a simple, scalable, fast, async web server for processing RESTful HTTP/HTTPS requests, written in C#. The solution consists of three main packages:

- **Watson** - Full-featured webserver built on top of http.sys
- **Watson.Lite** - Lightweight version with no http.sys dependency, using TCP implementation via CavemanTcp
- **Watson.Core** - Shared core library containing common functionality

## Architecture

The codebase is organized into three main projects and numerous test projects:

### Core Projects
- `WatsonWebserver.Core/` - Shared core library with base classes, routing, and common functionality
- `WatsonWebserver/` - Main Watson webserver implementation using http.sys
- `WatsonWebserver.Lite/` - Lightweight implementation without http.sys dependency

### Test Projects
- `Test.Default/` - Basic webserver example and testing
- `Test.Routing/` - Route handling examples
- `Test.ChunkServer/` - Chunked transfer encoding examples
- `Test.ServerSentEvents/` - Server-sent events implementation
- `Test.HostBuilder/` - HostBuilder pattern examples
- `Test.Authentication/` - Authentication examples
- `Test.Docker/` - Docker deployment examples
- Additional specialized test projects for various features

## Development Commands

### Building the Solution
```bash
dotnet build WatsonWebserver.sln
```

### Building Individual Projects
```bash
dotnet build WatsonWebserver/WatsonWebserver.csproj
dotnet build WatsonWebserver.Core/WatsonWebserver.Core.csproj
dotnet build WatsonWebserver.Lite/WatsonWebserver.Lite.csproj
```

### Running Test Projects
```bash
dotnet run --project Test.Default/Test.Default.csproj
dotnet run --project Test.Routing/Test.Routing.csproj
```

### Creating NuGet Packages
The projects are configured to generate NuGet packages on build via `GeneratePackageOnBuild=true`.

## Key Architectural Concepts

### Three-Layer Architecture
1. **Watson.Core** - Contains base classes, routing logic, HTTP context handling
2. **Watson/Watson.Lite** - Implementation-specific webserver logic
3. **Test Projects** - Usage examples and integration tests

### Routing System
Watson uses a hierarchical routing system that processes requests in this order:
1. `.Preflight` - Preflight requests (OPTIONS)
2. `.PreRouting` - Always executed before routing
3. `.PreAuthentication` - Contains Static, Content, Parameter, and Dynamic routes
4. `.AuthenticateRequest` - Authentication boundary
5. `.PostAuthentication` - Same structure as PreAuthentication
6. `.Default` - Fallback route
7. `.PostRouting` - Always executed, typically for logging

### Multi-Framework Support
All projects target multiple .NET frameworks:
- .NET Standard 2.0/2.1
- .NET Framework 4.6.2/4.8
- .NET 6.0/8.0

## Important Implementation Details

### Watson vs Watson.Lite
- **Watson**: Uses http.sys, requires elevation for non-localhost bindings, HOST header must match binding
- **Watson.Lite**: Pure TCP implementation, no http.sys dependency, HOST header matching not required

### HostBuilder Pattern
Use the HostBuilder extension for simplified server configuration:
```csharp
using WatsonWebserver.Extensions.HostBuilderExtension;
```

### Hostname Handling
- `UseMachineHostname` setting controls HOST header behavior
- Wildcard bindings (`*` or `+`) force machine hostname usage
- Important for proper URI handling in modern .NET runtimes

## Coding Standards and Style Guidelines

These standards MUST be followed strictly for all code in this repository:

### File Organization and Structure
- Namespace declaration at the top, using statements INSIDE the namespace block
- Microsoft/system library usings first (alphabetical), then other usings (alphabetical)
- One class or one enum per file - no nesting multiple classes/enums in single files
- Regions only for large files (500+ lines): Public-Members, Private-Members, Constructors-and-Factories, Public-Methods, Private-Methods

### Variable and Member Naming
- Private class member variables: underscore + PascalCase (e.g., `_FooBar`, NOT `_fooBar`)
- No `var` - always use explicit types
- Avoid tuples unless absolutely necessary

### Documentation Requirements
- ALL public members, constructors, and public methods MUST have XML documentation
- NO documentation on private members or private methods
- Document default values, minimum/maximum values where applicable
- Document nullability in XML comments
- Document thread safety guarantees
- Document exceptions using `/// <exception>` tags

### Public Members and Properties
- Explicit getters/setters with backing variables when validation needed
- Use configurable public members instead of constants where developers might want to change values
- Include meaningful default values in documentation

### Async Programming
- All async methods MUST accept CancellationToken parameter (unless class has CancellationToken member)
- Use `.ConfigureAwait(false)` where appropriate
- Check cancellation at appropriate places
- Create async variants for methods returning IEnumerable

### Error Handling and Exceptions
- Use specific exception types, not generic Exception
- Include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`

### Resource Management
- Implement IDisposable/IAsyncDisposable for unmanaged resources
- Use 'using' statements/declarations for IDisposable objects
- Follow full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### Null Safety and Input Validation
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Validate input parameters with guard clauses at method start
- Use `ArgumentNullException.ThrowIfNull()` (.NET 6+) or manual null checks
- Proactively eliminate null exception scenarios
- Consider Result pattern or Option/Maybe types for fallible methods

### Thread Safety and Concurrency
- Use Interlocked operations for simple atomic operations
- Prefer ReaderWriterLockSlim over lock for read-heavy scenarios

### LINQ and Collections
- Prefer LINQ methods over manual loops when readability isn't compromised
- Use `.Any()` instead of `.Count() > 0` for existence checks
- Be aware of multiple enumeration - consider `.ToList()` when needed
- Use `.FirstOrDefault()` with null checks rather than `.First()`

### Special Considerations
- Do not assume class members/methods exist - ask for implementation details
- Manual SQL strings are used intentionally - do not suggest ORMs without discussion
- Ensure README accuracy when it exists
- Compile code to ensure error/warning-free state

## Common Development Patterns

### Creating Test Applications
Follow the pattern established in `Test.Default/` - executable console applications that reference both Watson and Watson.Lite projects for comparison testing.

### Route Implementation
Routes should be implemented as async Task methods accepting HttpContextBase:
```csharp
static async Task MyRoute(HttpContextBase ctx) =>
    await ctx.Response.Send("Response content");
```

### Error Handling
Use exception routes for structured error handling:
```csharp
server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/path/", RouteHandler, ExceptionHandler);
```