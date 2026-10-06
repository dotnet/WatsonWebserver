# Native AOT and Trimming

Watson 7.3 and later support native AOT compilation and trimming on .NET 8 and .NET 10. The `Watson` and
`Watson.Clients` packages are marked `IsAotCompatible`, they build with no trim or AOT warnings, and every
release is validated by publishing a native executable that runs the server end to end.

`netstandard2.1` can't be AOT compiled, so this guide applies only to the `net8.0` and `net10.0` targets.

## Applications that don't use AOT

**Nothing changes, and nothing needs to change.** The default serializer still uses reflection, still
serializes any type (anonymous types included), and produces the same JSON it did before 7.3. The rest of this
guide only applies when you publish with `PublishAot` or `PublishTrimmed`.

## What works without any setup

The transport and request pipeline don't use reflection and produce no trim or AOT warnings, so all of this
works under native AOT unchanged:

- HTTP/1.1, HTTP/2 (TLS and h2c), and HTTP/3. HTTP/3 also needs the native `msquic` library on Linux and
  macOS, exactly as it does without AOT.
- Static, content, parameter, catch-all, and dynamic routes, routing groups, and middleware
- Low-level `Func<HttpContextBase, Task>` routes, request bodies, chunked transfer, and server-sent events
- WebSockets, on both the server and the `Watson.Clients` client
- Authentication callbacks, access control, timeouts, telemetry, and the Prometheus endpoint
- Watson's own JSON: API error responses (`ApiErrorResponse`), timeout, authentication, and deserialization
  errors, health-check results, and the OpenAPI document. These use source-generated metadata that ships
  inside the Watson package.

## What you need to do: register your own types

API routes serialize **your** response and request types with System.Text.Json. Under native AOT, reflection-based
JSON serialization is disabled, so System.Text.Json needs source-generated metadata for those types. Watson
can't generate it for you, because your types don't exist when the Watson package is compiled.

The setup has two steps.

**1. Declare a `JsonSerializerContext` that lists your types:**

```csharp
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(UserResponse))]
[JsonSerializable(typeof(CreateUserRequest))]
internal partial class AppJsonContext : JsonSerializerContext
{
}
```

**2. Give it to Watson:**

```csharp
Webserver server = new Webserver(settings, DefaultRoute);
server.Serializer = new DefaultSerializationHelper(AppJsonContext.Default);

server.Post<CreateUserRequest>("/users", async (req) =>
{
    CreateUserRequest body = req.GetData<CreateUserRequest>();
    return new UserResponse { Id = Guid.NewGuid(), Name = body.Name };
});
```

Your route code doesn't change. Watson combines your context with its own, so you never need to register
`ApiErrorResponse`, `HealthCheckResult`, or the primitive and collection types that appear in them.

### What to register

Register the top-level types that pass through Watson's serializer:

- the values your API routes return (`server.Get(...)` and the other helpers)
- the `T` of `Post<T>`, `Put<T>`, and `Patch<T>`
- values you assign to `WebserverException.Data` or `HealthCheckResult.Data` that aren't strings, numbers,
  booleans, `Guid`, `DateTime`, `List<object>`, or `Dictionary<string, object>`
- anything you pass to `server.Serializer.SerializeJson(...)` yourself

Types reached through properties (nested objects, `List<T>` elements) are picked up automatically. Types that
appear only in low-level routes, where you write the response bytes yourself, don't involve Watson.

## Differences from a non-AOT application

| Topic | Without AOT | With AOT and a context |
|---|---|---|
| Anonymous types (`return new { Id = 1 };`) | Supported | Not supported by System.Text.Json source generation; return a named type |
| Enums in your types | Written as strings | Written as strings only with `UseStringEnumConverter = true` on your context (otherwise numbers). Watson's own enums are always strings |
| `object` and `Dictionary<string, object>` values | Any runtime type | Each runtime type must be registered, unless it's a string, number, boolean, `Guid`, `DateTime`, or a list or dictionary of those |
| Exception objects in a response body | Every public property, including ones your exception type adds | Not supported: registering an exception type makes the source generator reference `Exception.TargetSite`, which raises trim warning IL2026. Put the details you need in your own type |
| Serializing `HttpContextBase`, `HttpRequestBase`, or `HttpResponseBase` for debugging | Supported | Not supported; serialize the fields you need into your own type |
| `DateTime`, `IPAddress`, `NameValueCollection` formatting | Watson's converters | Same converters, same output |

Watson's own null handling (null properties omitted), pretty versus compact output, and date format apply in
both modes.

## When a type is missing

If a type isn't registered, Watson throws an `InvalidOperationException` that names the type and shows the
fix:

```
No JSON serialization metadata is available for type 'MyApp.UserResponse'. Declare it with
[JsonSerializable(typeof(UserResponse))] on a JsonSerializerContext and assign
new DefaultSerializationHelper(YourJsonContext.Default) to the webserver's Serializer property. ...
```

From an API route, this becomes a structured `500` JSON response carrying that message, so the client sees it
too. A type nested inside an `object`-typed member fails with System.Text.Json's `NotSupportedException`, which
also names the type.

If you never assign `server.Serializer`, the default serializer detects that reflection is disabled and falls
back to Watson's metadata only. Watson's own responses keep working, and your types fail with the message
above. If you explicitly construct `new DefaultSerializationHelper()` in an AOT or trimmed application, the
build warns (IL2026 and IL3050), because that constructor is reflection-based.

## OpenAPI

The OpenAPI document serializes without reflection. Example, default, and enum values that are strings,
numbers, booleans, or lists and dictionaries of them need nothing more. If you use one of your own types as an
example value, register it and set `OpenApiSettings.TypeInfoResolver`:

```csharp
server.UseOpenApi(api =>
{
    api.Info.Title = "My API";
    api.TypeInfoResolver = AppJsonContext.Default;
    api.Schemas["User"] = new OpenApiSchemaMetadata
    {
        Type = "object",
        Example = new UserResponse { Name = "example" }
    };
});
```

Example values still follow the OpenAPI generator's camel-case naming policy.

## Custom serializers

If you replace `server.Serializer` with your own `ISerializationHelper`, it must be AOT-safe itself. Under native
AOT it also has to serialize `ApiErrorResponse` and `HealthCheckResult`, because Watson sends those through
`server.Serializer`. Adding them to your own `JsonSerializerContext` is enough.

## Publishing

```
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
```

Watson adds no special publish requirements. Native AOT needs the platform's native toolchain (on Windows, the
Visual Studio "Desktop development with C++" workload; on Linux, `clang` and the zlib development package; on
macOS, the Xcode command-line tools).

## How Watson validates AOT

- The library builds with `IsAotCompatible` and warnings as errors on `net8.0` and `net10.0`, so any new trim or
  AOT warning fails the build.
- The shared test suite (`AotSerialization`, in `src/Test.Shared/SharedAotSerializationTests.cs`) runs the
  source-generated serialization path without reflection and compares it with the reflection-based path for
  API errors, health results, application types, exceptions, and the OpenAPI document.
- `src/Test.Aot` is a native AOT application. It is published as a native executable for both `net8.0` and
  `net10.0`, then exercises routing, API routes, typed bodies, errors, timeouts, authentication, health
  checks, OpenAPI, middleware, chunked responses, server-sent events, the Prometheus endpoint, and WebSockets
  (through `Watson.Clients`) over HTTP/1.1 and HTTP/2 (h2c). See [TESTING.md](TESTING.md).
