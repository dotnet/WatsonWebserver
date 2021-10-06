# Routing

All requests are processed through Watson's pre-routing handler, if it has been assigned.  The pre-routing handler indicates whether or not the request should be permitted to to continue.  

Beyond the pre-routing handler, Watson includes following types of routing:

- **Content routes** - specifically for serving objects (GET) or verifying existence (HEAD)
- **Static routes** - routes with specific URLs
- **Parameter routes** - routes with URLs that contain variables, for instance, ```/customer/{id}```
- **Dynamic routes** - routes with more complex URLs defined by regular expression
- **Default route** - any request not matching a content, static, parameter, or dynamic route is processed using the default route

## Pre-Routing Handler

All requests are processed through Watson's pre-routing handler, if it has been assigned.  

### Assigning the Pre-Routing Handler
```csharp
using WatsonWebserver;

Server server = new Server("localhost", 8000, false, DefaultRoute);
server.Routes.PreRouting = PreRoutingHandler;

static async Task<bool> PreRoutingHandler(HttpContext ctx)
{
  return false;  // allow the connection
}

static async Task DefaultRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Hello from the default route!");
}
```

The ```HttpContext``` is passed into the pre-routing handler, giving you visibility into everything you may want to know about the request.  If you ```return true```, this indicates that Watson should terminate the connection, i.e. it should not be allowed to continue through to other routes.  If you ```return false```, this indicates that Watson should allow the connection through to other routes.

The pre-routing handler is commonly used for tasks such as authentication, access control, and logging.

## Content Routes

Content routes are used to serve files from Watson (i.e. HTTP GET requests) and verify existence of files (i.e. HTTP HEAD requests).

### Set the Base Directory

You should not need to set the base directory unless it is different than the directory from which your server is running.

```csharp
server.Routes.Content.BaseDirectory = "./";
```

### Specify Content Routes
```csharp
server.Routes.Content.Add("/html/", true);            // the entire directory
server.Routes.Content.Add("/large/", true);           // the entire directory
server.Routes.Content.Add("/img/watson.jpg", false);  // individual files work too!
```

Content routes only work for HTTP GET and HEAD requests.

## Static Routes

Static routes are specific URL endpoints and specific HTTP methods without variables.  For example, ```POST /login``` may be a static route. 

Static routes can be defined multiple ways.  All three are functionally equivalent.

1) Using the ```StaticRoute``` attribute
2) Adding a method to the ```StaticRouteManager```
3) Adding an inline function to the ```StaticRouteManager```

### Using the StaticRoute Attribute

The ```StaticRoute``` attribute can be applied to a method.  This will automatically include the static route in Watson without any additional configuration.
```csharp
[StaticRoute(HttpMethod.GET, "/login")]
public static async Task LoginRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("You're in!");
}
```

### Adding a Method to StaticRouteManager
```csharp
public static async Task LoginRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("You're in!");
}

server.Routes.Static.Add(HttpMethod.POST, "/login", LoginRoute);
```

### Adding Inline Function to StaticRouteManager
```csharp
server.Routes.Static.Add(HttpMethod.POST, "/login", async (ctx) =>
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("You're in!");
  return;
});
```

## Parameter Routes

Parameter routes are specific URL endpoints and specific HTTP methods **with** variables.  For example, ```GET /order/{id}``` may be a parameter route, because ```{id}``` is a variable that is needed for processing the request.

Parameters, if matched within the URL, appear in ```HttpContext.Request.Url.Parameters```.  For instance, in the case of a parameter route ```GET /order/{id}``` and an incoming request of ```GET /order/123```, the ID (of ```123```) could be accessed using ```HttpContext.Request.Url.Parameters["id"]```.

Like static routes, parameter routes can be added using the ```ParameterRoute``` attribute, by adding a method to the ```ParameterRouteManager```, or by adding an inline function to the ```ParameterRouteManager```.

### Using the ParameterRoute Attribute

The ```ParameterRoute``` attribute can be applied to a method.  This will automatically include the parameter route in Watson without any additional configuration.
```csharp
[ParameterRoute(HttpMethod.GET, "/order/{id}")]
public static async Task GetOrderRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Parameters["id"] + "!");
}
```

### Adding a Method to ParameterRouteManager
```csharp
public static async Task GetOrderRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Parameters["id"] + "!");
}

server.Routes.Parameter.Add(HttpMethod.GET, "/order/{id}", GetOrderRoute);
```

### Adding Inline Function to ParameterRouteManager
```csharp
server.Routes.Parameter.Add(HttpMethod.GET, "/order/{id}", async (ctx) =>
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Parameters["id"] + "!");
});
```

## Dynamic Routes

Dynamic routes are URL endpoints defined by a regular expression and specific HTTP methods.  For example, ```GET ^/order/\\d+$``` would be a dynamic route, and would match ```/order/1``` and ```/order/abcd```, etc.

Like static and parameter routes, dynamic routes can be added using the ```DynamicRoute``` attribute, by adding a method to the ```DynamicRouteManager```, or by adding an inline function to the ```DynamicRouteManager```.

It is best to understand that URL elements can be accessed using ```HttpContext.Request.Url.Elements```.  For instance, in the case of ```GET /order/abcd```, ```order``` would be found in ```HttpContext.Request.Url.Elements[0]```, and ```abcd``` would be found in ```HttpContext.Request.Url.Elements[1]```.

### Using the DynamicRoute Attribute

The ```DynamicRoute``` attribute can be applied to a method.  This will automatically include the dynamic route in Watson without any additional configuration.
```csharp
[DynamicRoute(HttpMethod.GET, "^/order/\\d+$")]
public static async Task GetOrderRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Elements[1] + "!");
}
```

### Adding a Method to DynamicRouteManager
```csharp
public static async Task GetOrderRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Elements[1] + "!");
}

server.Routes.Dynamic.Add(HttpMethod.GET, "^/order/\\d+$", GetOrderRoute);
```

### Adding Inline Function to DynamicRouteManager
```csharp
server.Routes.Dynamic.Add(HttpMethod.GET, "^/order/\\d+$", async (ctx) =>
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Here are details for order ID " + ctx.Request.Url.Elements[1] + "!");
});
```

## Default Route

The default route is also known as the ```route of last resort```.  i.e. if no other route could be matched, this is the route that gets the job of handling the request.  Don't get me wrong, you could literally build your entire set of APIs within the default route (and it would work just fine).  But, it's a best practice to use the default route as just that - the ```route of last resort```.

### Defining Default Route During Instantiation
```csharp
Server server = new Server("localhost", 8000, false, DefaultRoute);

static async Task DefaultRoute(HttpContext ctx)
{
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Hello from the default route!");
}
```

### Defining Default Route in Settings
```csharp
server.Settings.Routes.Default = DefaultRoute;
```
