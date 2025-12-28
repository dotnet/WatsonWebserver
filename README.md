![alt tag](https://github.com/jchristn/watsonwebserver/blob/master/assets/watson.ico)

# Watson Webserver

Simple, scalable, fast, async web server for processing RESTful HTTP/HTTPS requests, written in C#.

| Package       | NuGet Version  | Downloads |
|---------------|----------------|-----------|
| Watson        | [![NuGet Version](https://img.shields.io/nuget/v/Watson.svg?style=flat)](https://www.nuget.org/packages/Watson/) | [![NuGet](https://img.shields.io/nuget/dt/Watson.svg)](https://www.nuget.org/packages/Watson) |
| Watson.Lite   | [![NuGet Version](https://img.shields.io/nuget/v/Watson.Lite.svg?style=flat)](https://www.nuget.org/packages/Watson.Lite/) | [![NuGet](https://img.shields.io/nuget/dt/Watson.Lite.svg)](https://www.nuget.org/packages/Watson.Lite) |
| Watson.Core   | [![NuGet Version](https://img.shields.io/nuget/v/Watson.Core.svg?style=flat)](https://www.nuget.org/packages/Watson.Core/) | [![NuGet](https://img.shields.io/nuget/dt/Watson.Core.svg)](https://www.nuget.org/packages/Watson.Core) |

Special thanks to @DamienDennehy for allowing us the use of the ```Watson.Core``` package name in NuGet!

## .NET Foundation

This project is part of the [.NET Foundation](http://www.dotnetfoundation.org/projects) along with other projects like [the .NET Runtime](https://github.com/dotnet/runtime/).

## New in v6.5.x

- OpenAPI and Swagger support, refer to `Test.OpenApi` project and `README.md`

## Special Thanks

I'd like to extend a special thanks to those that have helped make Watson Webserver better.

- @notesjor @shdwp @Tutch @GeoffMcGrath @jurkovic-nikola @joreg @Job79 @at1993 @MartyIX 
- @pocsuka @orinem @deathbull @binozo @panboy75 @iain-cyborn @gamerhost31 @lucafabbri 
- @nhaberl @grgouala @sapurtcomputer30 @winkmichael @sqlnew @SaintedPsycho @Return25 
- @marcussacana @samisil @Jump-Suit @ChZhongPengCheng33 @bobaoapae @rodgers-r 
- @john144 @zedle @GitHubProUser67 @bemoty @bemon @nomadeon @Infiziert90 @kyoybs 

## Watson vs Watson.Lite

Watson is a webserver that operates on top of the underlying `http.sys` within the operating system.  Watson.Lite was created by merging [HttpServerLite](https://github.com/jchristn/HttpServerLite).  Watson.Lite does not have a dependency on `http.sys`, and is implemented using a TCP implementation provided by [CavemanTcp](https://github.com/jchristn/cavemantcp).

The dependency on `http.sys` (or lack thereof) creates subtle differences between the two libraries, however, the configuration and management of each should be consistent.

Watson.Lite is generally less performant than Watson, because the HTTP implementation is in user space.  

## Important Notes

- Elevation (administrative privileges) may be required if binding an IP other than `127.0.0.1` or `localhost`
- For Watson:
  - The HTTP HOST header must match the specified binding
  - For SSL, the underlying computer certificate store will be used
- For Watson.Lite:
  - Watson.Lite uses a TCP listener; your server must be started with an IP address, not a hostname
  - The HTTP HOST header does not need to match, since the listener must be defined by IP address
  - For SSL, the certificate filename, filename and password, or `X509Certificate2` must be supplied
  - When a request body is present, certain browsers may require that you fully read the request body server-side before redirecting or responding to the request

## Routing

Watson and Watson.Lite always routes in the following order (configure using `Webserver.Routes`):

- `.Preflight` - handling preflight requests (generally with HTTP method `OPTIONS`)
- `.PreRouting` - always invoked before any routing determination is made
- `.PreAuthentication` - a routing group, comprised of:
  - `.Static` - static routes, e.g. an HTTP method and an explicit URL
  - `.Content` - file serving routes, e.g. a directory where files can be read
  - `.Parameter` - routes where variables are specified in the path, e.g. `/user/{id}`
  - `.Dynamic` - routes where the URL is defined by a regular expression
- `.AuthenticateRequest` - demarcation route between unauthenticated and authenticated routes
- `.PostAuthentication` - a routing group with a structure identical to `.PreAuthentication`
- `.Default` - the default route; all requests go here if not routed previously
- `.PostRouting` - always invoked, generally for logging and telemetry

If you do not wish to use authentication, you should map your routes in the `.PreAuthentication` routing group (though technically they can be placed in `.PostAuthentication` or `.Default` assuming the `AuthenticateRequest` method is null.

As a general rule, never try to send data to an `HttpResponse` while in the `.PostRouting` route.  If a response has already been sent, the attempt inside of `.PostRouting` will fail.

## Authentication

It is recommended that you implement authentication in `.AuthenticateRequest`.  Should a request fail authentication, return a response within that route.  The `HttpContextBase` class has properties that can hold authentication-related or session-related metadata, specifically, `.Metadata`.

## Access Control

By default, Watson and Watson.Lite will permit all inbound connections.

- If you want to block certain IPs or networks, use `Server.AccessControl.DenyList.Add(ip, netmask)`
- If you only want to allow certain IPs or networks, and block all others, use:
  - `Server.AccessControl.Mode = AccessControlMode.DefaultDeny`
  - `Server.AccessControl.PermitList.Add(ip, netmask)`
  
## Simple Example

Refer to `Test.Default` for a full example.

```csharp
using System.IO;
using System.Text;
using WatsonWebserver;

static void Main(string[] args)
{
  WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
  WebserverBase server = new Webserver(settings, DefaultRoute);
  server.Start();
  Console.ReadLine();
}

static async Task DefaultRoute(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the default route!");
```

Then, open your browser to `http://127.0.0.1:9000/`.

## Example with Routes

Refer to `Test.Routing` for a full example.

```csharp
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WatsonWebserver;

static void Main(string[] args)
{
  WebserverSettings settings = new WebserverSettings("127.0.0.1", 9000);
  WebserverBase server = new Webserver(settings, DefaultRoute);

  // add content routes
  server.Routes.PreAuthentication.Content.Add("/html/", true);
  server.Routes.PreAuthentication.Content.Add("/img/watson.jpg", false);

  // add static routes
  server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hello/", GetHelloRoute);
  server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/howdy/", async (HttpContextBase ctx) =>
  {
      await ctx.Response.Send("Hello from the GET /howdy static route!");
      return;
  });

  // add parameter routes
  server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{version}/bar", GetBarRoute);

  // add dynamic routes
  server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/foo/\\d+$"), GetFooWithId);  
  server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/foo/?$"), GetFoo); 

  // start the server
  server.Start();

  Console.WriteLine("Press ENTER to exit");
  Console.ReadLine();
}

static async Task GetHelloRoute(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the GET /hello static route!");

static async Task GetBarRoute(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the GET /" + ctx.Request.Url.Parameters["version"] + "/bar route!");

static async Task GetFooWithId(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the GET /foo/[id] dynamic route!");
 
static async Task GetFoo(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the GET /foo/ dynamic route!");

static async Task DefaultRoute(HttpContextBase ctx) =>
  await ctx.Response.Send("Hello from the default route!");
```

## Route with Exception Handler

```csharp
server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hello/", GetHelloRoute, MyExceptionRoute);

static async Task GetHelloRoute(HttpContextBase ctx) => throw new Exception("Whoops!");

static async Task MyExceptionRoute(HttpContextBase ctx, Exception e)
{
  ctx.Response.StatusCode = 500;
  await ctx.Response.Send(e.Message);
}
```

## Permit or Deny by IP or Network

```csharp
Webserver server = new Webserver("127.0.0.1", 9000, false, DefaultRoute);

// set default permit (permit any) with deny list to block specific IP addresses or networks
server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
server.Settings.AccessControl.DenyList.Add("127.0.0.1", "255.255.255.255");  

// set default deny (deny all) with permit list to permit specific IP addresses or networks
server.Settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
server.Settings.AccessControl.PermitList.Add("127.0.0.1", "255.255.255.255");
```

## Chunked Transfer-Encoding

Watson supports both receiving chunked data and sending chunked data (indicated by the header `Transfer-Encoding: chunked`).

Refer to `Test.ChunkServer` for a sample implementation.

### Receiving Chunked Data

```csharp
static async Task UploadData(HttpContextBase ctx)
{
  if (ctx.Request.ChunkedTransfer)
  {
    bool finalChunk = false;
    while (!finalChunk)
    {
      Chunk chunk = await ctx.Request.ReadChunk();
      // work with chunk.Length and chunk.Data (byte[])
      finalChunk = chunk.IsFinalChunk;
    }
  }
  else
  {
    // read from ctx.Request.Data stream   
  }
}
```

### Sending Chunked Data

```csharp
static async Task DownloadChunkedFile(HttpContextBase ctx)
{
  using (FileStream fs = new FileStream("./img/watson.jpg", , FileMode.Open, FileAccess.Read))
  {
    ctx.Response.StatusCode = 200;
    ctx.Response.ChunkedTransfer = true;

    byte[] buffer = new byte[4096];
    while (true)
    {
      int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
      byte[] data = new byte[bytesRead];
      Buffer.BlockCopy(buffer, 0, bytesRead, data, 0); // only copy the read data

      if (bytesRead > 0)
      {
        await ctx.Response.SendChunk(data, false);
      }
      else
      {
        await ctx.Response.SendChunk(Array.Empty<byte>(), true);
        break;
      }
    }
  }

  return;
}
```

## Server-Sent Events

Watson supports sending server-sent events.  Refer to `Test.ServerSentEvents` for a sample implementation.  The `SendEvent` method handles formatting `ServerSentEvent` objects for transmission to the client.

### Sending Events

```csharp
static async Task SendEvents(HttpContextBase ctx)
{
  ctx.Response.StatusCode = 200;
  ctx.Response.ServerSentEvents = true;

  for (int i = 1; i <= 10; i++)
  {
    ServerSentEvent ev = new ServerSentEvent 
    {
      Id = i.ToString(),
      Event = "my-event-type",
      Data = $"Event number {i.ToString()}",
    };

    bool isFinal = (i == 10);
    await ctx.Response.SendEvent(ev, isFinal);
  }

  return;
}
```

## HostBuilder

`HostBuilder` helps you set up your server much more easily by introducing a chain of settings and routes instead of using the server class directly.  Special thanks to @sapurtcomputer30 for producing this fine feature!

Refer to `Test.HostBuilder` for a full sample implementation.

```csharp
using WatsonWebserver.Extensions.HostBuilderExtension;

Webserver server = new HostBuilder("127.0.0.1", 8000, false, DefaultRoute)
  .MapStaticRoute(HttpMethod.GET, GetUrlsRoute, "/links")
  .MapStaticRoute(HttpMethod.POST, CheckLoginRoute, "/login")
  .MapStaticRoute(HttpMethod.POST, TestRoute, "/test")
  .Build();

server.Start();

Console.WriteLine("Server started");
Console.ReadKey();

static async Task DefaultRoute(HttpContextBase ctx) => 
    await ctx.Response.Send("Hello from default route!"); 

static async Task GetUrlsRoute(HttpContextBase ctx) => 
    await ctx.Response.Send("Here are your links!"); 

static async Task CheckLoginRoute(HttpContextBase ctx) => 
    await ctx.Response.Send("Checking your login!"); 

static async Task TestRoute(HttpContextBase ctx) =>
    await ctx.Response.Send("Hello from the test route!");
```

## OpenAPI / Swagger Support

Watson and Watson.Lite include built-in OpenAPI 3.0 documentation generation and Swagger UI. No additional packages are required - OpenAPI support is included in `WatsonWebserver.Core`.

Refer to `Test.OpenApi` for a complete working example.

### Basic Setup

```csharp
using WatsonWebserver;
using WatsonWebserver.Core;
using WatsonWebserver.Core.OpenApi;

WebserverSettings settings = new WebserverSettings("localhost", 8080);
Webserver server = new Webserver(settings, DefaultRoute);

// Enable OpenAPI with Swagger UI
server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "My API";
    openApi.Info.Version = "1.0.0";
    openApi.Info.Description = "My API description";
});

server.Start();
```

After starting the server:
- **OpenAPI JSON:** `http://localhost:8080/openapi.json`
- **Swagger UI:** `http://localhost:8080/swagger`

### Enabling/Disabling OpenAPI and Swagger

By default, both OpenAPI and Swagger UI are enabled when you call `UseOpenApi()`. You can control this behavior using the `EnableOpenApi` and `EnableSwaggerUi` settings:

```csharp
// Disable OpenAPI entirely (no endpoints registered)
server.UseOpenApi(openApi =>
{
    openApi.EnableOpenApi = false;
});

// Enable OpenAPI JSON but disable Swagger UI
server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "My API";
    openApi.Info.Version = "1.0.0";
    openApi.EnableOpenApi = true;      // Default: true
    openApi.EnableSwaggerUi = false;   // Disables /swagger endpoint
});

// Enable both (default behavior)
server.UseOpenApi(openApi =>
{
    openApi.Info.Title = "My API";
    openApi.Info.Version = "1.0.0";
    openApi.EnableOpenApi = true;      // Default: true
    openApi.EnableSwaggerUi = true;    // Default: true
});
```

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableOpenApi` | `true` | When `false`, no OpenAPI endpoints are registered (both `/openapi.json` and `/swagger` are disabled) |
| `EnableSwaggerUi` | `true` | When `false`, only the Swagger UI (`/swagger`) is disabled; OpenAPI JSON remains available |

### Documenting Routes

Add OpenAPI documentation to routes using the `openApiMetadata` parameter. The fluent API makes it easy to build complete documentation:

```csharp
using WatsonWebserver.Core.OpenApi;

// GET route with query parameters
server.Routes.PreAuthentication.Static.Add(
    HttpMethod.GET,
    "/api/users",
    GetUsersHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Get all users", "Users")
        .WithDescription("Returns a list of all users, optionally filtered by status")
        .WithParameter(OpenApiParameterMetadata.Query("active", "Filter by active status", false, OpenApiSchemaMetadata.Boolean()))
        .WithParameter(OpenApiParameterMetadata.Query("limit", "Maximum number of results", false, OpenApiSchemaMetadata.Integer()))
        .WithResponse(200, OpenApiResponseMetadata.Json(
            "List of users",
            OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("User")))));

// GET route with path parameter
server.Routes.PreAuthentication.Parameter.Add(
    HttpMethod.GET,
    "/api/users/{id}",
    GetUserByIdHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Get user by ID", "Users")
        .WithDescription("Retrieves a specific user by their unique identifier")
        .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()))
        .WithResponse(200, OpenApiResponseMetadata.Json("User found", OpenApiSchemaMetadata.CreateRef("User")))
        .WithResponse(404, OpenApiResponseMetadata.NotFound()));

// POST route with request body
server.Routes.PreAuthentication.Static.Add(
    HttpMethod.POST,
    "/api/users",
    CreateUserHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Create user", "Users")
        .WithDescription("Creates a new user in the system")
        .WithRequestBody(OpenApiRequestBodyMetadata.Json(
            new OpenApiSchemaMetadata
            {
                Type = "object",
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["name"] = OpenApiSchemaMetadata.String(),
                    ["email"] = OpenApiSchemaMetadata.String("email"),
                    ["age"] = OpenApiSchemaMetadata.Integer()
                },
                Required = new List<string> { "name", "email" }
            },
            "User data to create",
            required: true))
        .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaMetadata.CreateRef("User")))
        .WithResponse(400, OpenApiResponseMetadata.BadRequest()));

// DELETE route
server.Routes.PreAuthentication.Parameter.Add(
    HttpMethod.DELETE,
    "/api/users/{id}",
    DeleteUserHandler,
    openApiMetadata: OpenApiRouteMetadata.Create("Delete user", "Users")
        .WithParameter(OpenApiParameterMetadata.Path("id", "User ID", OpenApiSchemaMetadata.Integer()))
        .WithResponse(204, OpenApiResponseMetadata.NoContent())
        .WithResponse(404, OpenApiResponseMetadata.NotFound()));
```

### OpenAPI Settings

```csharp
server.UseOpenApi(openApi =>
{
    // API Information (required)
    openApi.Info.Title = "My API";
    openApi.Info.Version = "1.0.0";
    openApi.Info.Description = "API description";
    openApi.Info.Contact = new OpenApiContact
    {
        Name = "Support",
        Email = "support@example.com",
        Url = "https://example.com/support"
    };
    openApi.Info.License = new OpenApiLicense
    {
        Name = "MIT",
        Url = "https://opensource.org/licenses/MIT"
    };

    // Tags for grouping endpoints in Swagger UI
    openApi.Tags.Add(new OpenApiTag { Name = "Users", Description = "User management operations" });
    openApi.Tags.Add(new OpenApiTag { Name = "Products", Description = "Product catalog operations" });

    // Security schemes (for authenticated APIs)
    openApi.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
    {
        Type = "apiKey",
        Name = "X-API-Key",
        In = "header",
        Description = "API key for authorization"
    };

    // Or use Bearer token authentication
    openApi.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = "http",
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT authorization header"
    };

    // Enable/disable OpenAPI and Swagger
    openApi.EnableOpenApi = true;             // Default (set to false to disable all OpenAPI endpoints)
    openApi.EnableSwaggerUi = true;           // Default (set to false to disable only Swagger UI)

    // Customize endpoint paths
    openApi.DocumentPath = "/openapi.json";   // Default
    openApi.SwaggerUiPath = "/swagger";       // Default

    // Control which routes are documented
    openApi.IncludePreAuthRoutes = true;      // Default
    openApi.IncludePostAuthRoutes = true;     // Default
    openApi.IncludeContentRoutes = false;     // Default (file-serving routes)
});
```

### Schema Helper Methods

The `OpenApiSchemaMetadata` class provides convenient factory methods for common types:

```csharp
OpenApiSchemaMetadata.String()              // string
OpenApiSchemaMetadata.String("email")       // string with format (email, date-time, uri, etc.)
OpenApiSchemaMetadata.Integer()             // integer (int32)
OpenApiSchemaMetadata.Long()                // integer (int64)
OpenApiSchemaMetadata.Number()              // number (double)
OpenApiSchemaMetadata.Boolean()             // boolean
OpenApiSchemaMetadata.CreateArray(items)    // array of items
OpenApiSchemaMetadata.CreateRef("User")     // $ref to #/components/schemas/User
```

### Response Helper Methods

The `OpenApiResponseMetadata` class provides factory methods for common responses:

```csharp
OpenApiResponseMetadata.Json("Description", schema)  // 200 with JSON body
OpenApiResponseMetadata.Created(schema)              // 201 Created
OpenApiResponseMetadata.NoContent()                  // 204 No Content
OpenApiResponseMetadata.BadRequest()                 // 400 Bad Request
OpenApiResponseMetadata.Unauthorized()               // 401 Unauthorized
OpenApiResponseMetadata.Forbidden()                  // 403 Forbidden
OpenApiResponseMetadata.NotFound()                   // 404 Not Found
```

## Hostname Handling for HTTP Responses

To correctly handle the `Host` HTTP header, a new boolean property, `UseMachineHostname`, has been introduced in `WebserverSettings`. This is especially important when binding to all network interfaces.

-   **All-Interface Binding Behavior**: When `Hostname` is set to `0.0.0.0` (or `*`/`+` for Watson only), the server will **mandatorily** use the machine's actual hostname for the `Host` header in HTTP responses. This prevents `UriFormatException` on modern .NET runtimes. In this scenario, `UseMachineHostname` is forced to `true`.

-   **Default Behavior**: For any other hostname (e.g., `localhost` or a specific IP address), this feature is **disabled by default**. The `Host` header will use the value specified in the `Hostname` setting.

-   **Manual Activation**: You can force the use of the machine's hostname for any binding by setting `UseMachineHostname = true` in the settings.

> **Note**: Watson.Lite does not support `*` or `+` as hostname values. Use `0.0.0.0` to listen on all interfaces.

### Usage

**Example 1: Binding to All Interfaces**

```csharp
// The server detects 0.0.0.0 and uses the machine's hostname for the Host header.
// Use 0.0.0.0 for Watson.Lite; Watson also supports "*" or "+".
var server = new Server("0.0.0.0", 9000, false, DefaultRoute);
server.Start();
```

**Example 2: Manually Enabling for a Specific Hostname**

```csharp
// By default, the Host header would be "localhost:9000".
// By setting UseMachineHostname = true, we force it to use the machine's actual hostname.
var settings = new WebserverSettings("localhost", 9000);
settings.UseMachineHostname = true; 
var server = new Server(settings, DefaultRoute);
server.Start();
```

### Hostname Examples

When `UseMachineHostname` is active, the retrieved hostname will vary depending on the operating system and network configuration. Here are some typical examples (after sanitization):

-   **Windows**: `desktop-a1b2c3d`
-   **macOS**: `marcos-macbook-pro.local`
-   **Linux**: `ubuntu-server`
-   **Android**: `pixel-7-pro`
-   **iOS**: `marcos-iphone.local`

## Accessing from Outside Localhost

### Watson

When you configure Watson to listen on `127.0.0.1` or `localhost`, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of `localhost`, use the following:

- Specify the exact DNS hostname upon which Watson should listen in the Server constructor
- The HOST header on HTTP requests MUST match the supplied listener value (operating system limitation)
- If you want to listen on more than one hostname or IP address, use `*`, `+`, or `0.0.0.0`.  You MUST run as administrator (operating system limitation)
- If you want to use a port number less than 1024, you MUST run as administrator (operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which Watson is listening
- You may have to add URL ACLs, i.e. URL bindings, within the operating system using the `netsh` command:
  - Check for existing bindings using `netsh http show urlacl`
  - Add a binding using `netsh http add urlacl url=http://[hostname]:[port]/ user=everyone listen=yes`
  - Where `hostname` and `port` are the values you are using in the constructor
  - If you are using SSL, you will need to install the certificate in the certificate store and retrieve the thumbprint
  - Refer to https://github.com/jchristn/WatsonWebserver/wiki/Using-SSL-on-Windows for more information, or if you are using SSL
- If you're still having problems, start a discussion here, and I will do my best to help and update the documentation.

### Watson.Lite

When you configure Watson.Lite to listen on `127.0.0.1`, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of the local machine, use the following:

- Specify the IP address of the network interface on which Watson.Lite should listen
- If you want to listen on more than one network interface, use `0.0.0.0`.  You MUST run as administrator (operating system limitation)
- Note: Watson.Lite does not support `*` or `+` as hostname values; use `0.0.0.0` instead
- If you want to use a port number less than 1024, you MUST run as administrator (operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which Watson is listening
- If you are using SSL, you will need to have one of the following when instantiating:
  - The `X509Certificate2` object
  - The filename and password to the certificate
- If you're still having problems, start a discussion here, and I will do my best to help and update the documentation.

## Running in Docker

Please refer to the `Test.Docker` project and the `Docker.md` file therein.

## Version History

Refer to CHANGELOG.md for version history.
