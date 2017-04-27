# Watson Webserver

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Watson/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# async web server for handling incoming RESTful HTTP/HTTPS requests. 

## New in v1.2.0
- Dynamic route support using C#/.NET regular expressions (see RegexMatcher library https://github.com/jchristn/RegexMatcher).

## Test App
A test project is included which will help you exercise the class library.

## Important Notes
- Watson Webserver will always check for static routes, then dynamic (regex) routes, then the default route.  
- When defining dynamic routes (regular expressions), be sure to add the most specific routes first.  Dynamic routes are evaluated in-order and the first match is used.

## Example using Routes
```
using WatsonWebserver;

static void Main(string[] args)
{
   Server s = new Server("127.0.0.1", 9000, false, DefaultRoute, true);

   // add static routes
   s.AddStaticRoute("get", "/hello/", GetHelloRoute);
   s.AddStaticRoute("get", "/world/", GetWorldRoute);

   // add dynamic routes
   s.AddDynamicRoute("get", new Regex("^/foo/\\d+$"), GetFooWithId);
   s.AddDynamicRoute("get", new Regex("^/foo/(.*?)/(.*?)/?$"), GetFooTwoChildren);
   s.AddDynamicRoute("get", new Regex("^/foo/(.*?)/?$"), GetFooOneChild);
   s.AddDynamicRoute("get", new Regex("^/foo/?$"), GetFoo); 

   Console.WriteLine("Press ENTER to exit");
   Console.ReadLine();
}

static HttpResponse GetHelloRoute(HttpRequest req)
{
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /hello static route!", true);
}

static HttpResponse GetWorldRoute(HttpRequest req)
{
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /world static route!", true);
}

static HttpResponse GetFooWithId(HttpRequest req)
{
   return ResponseBuilder(req, "Watson says hello from the GET /foo with ID dynamic route!");
}

static HttpResponse GetFooTwoChildren(HttpRequest req)
{ 
   return ResponseBuilder(req, "Watson says hello from the GET /foo with multiple children dynamic route!");
}

static HttpResponse GetFooOneChild(HttpRequest req)
{ 
   return ResponseBuilder(req, "Watson says hello from the GET /foo with one child dynamic route!");
}

static HttpResponse GetFoo(HttpRequest req)
{ 
   return ResponseBuilder(req, "Watson says hello from the GET /foo dynamic route!");
}

static HttpResponse DefaultRoute(HttpRequest req)
{
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the default route!", true);
}
```

## Version History
Notes from previous versions are shown below (summarized to minor build)

v1.1.0
- Added support for routes.  The default handler can be used for cases where a matching route isn't available, for instance, to build a custom 404 response.

v1.0.0
- Initial release.

## Running under Mono
Watson works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```