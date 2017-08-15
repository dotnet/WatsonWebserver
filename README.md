# Watson Webserver

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Watson/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# async web server for handling incoming RESTful HTTP/HTTPS requests. 

## New in v1.2.6
- IsListening property

## Test App
A test project is included which will help you exercise the class library.

## Important Notes
- Watson Webserver will always check routes in the following order:
  - If the request is GET or HEAD, content routes will first be checked
  - Then static routes will be evaluated
  - Then dynamic (regex) routes will be evaluated
  - Then the default route
- When defining dynamic routes (regular expressions), be sure to add the most specific routes first.  Dynamic routes are evaluated in-order and the first match is used.
- If a matching content route exists:
  - And the content does not exist, a standard 404 is sent
  - And the content cannot be read, a standard 500 is sent
  
## Example using Routes
```
using WatsonWebserver;

static void Main(string[] args)
{
   Server s = new Server("127.0.0.1", 9000, false, DefaultRoute, true);

   // add content routes
   s.AddContentRoute("/html/", true);
   s.AddContentRoute("/img/watson.jpg", false);

   // add static routes
   s.AddStaticRoute("get", "/hello/", GetHelloRoute);
   s.AddStaticRoute("get", "/world/", GetWorldRoute);

   // add dynamic routes
   s.AddDynamicRoute("get", new Regex("^/foo/\\d+$"), GetFooWithId);
   s.AddDynamicRoute("get", new Regex("^/foo/(.*?)/(.*?)/?$"), GetFooMultipleChildren);
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
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /foo with ID dynamic route!", true);
}

static HttpResponse GetFooMultipleChildren(HttpRequest req)
{ 
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /foo with multiple children dynamic route!", true);
}

static HttpResponse GetFooOneChild(HttpRequest req)
{ 
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /foo with one child dynamic route!", true);
}

static HttpResponse GetFoo(HttpRequest req)
{ 
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /foo dynamic route!", true);
}

static HttpResponse DefaultRoute(HttpRequest req)
{
   return new HttpResponse(req, true, 200, null, "text/plain", "Hello from the default route!", true);
}
```

## Version History
Notes from previous versions are shown below (summarized to minor build)

v1.2.x
- Bugfix for dispose (thank you @AChmieletzki)
- Static methods for building HttpRequest from various sources and conversion
- Static input methods
- Better initialization of object members
- More HTTP status codes (see https://en.wikipedia.org/wiki/List_of_HTTP_status_codes)
- Fix for content routes (thank you @KKoustas!)
- Fix for Xamarin IOS and Android (thank you @Tutch!)
- Added content routes for serving static files.
- Dynamic route support using C#/.NET regular expressions (see RegexMatcher library https://github.com/jchristn/RegexMatcher).

v1.1.x
- Added support for static routes.  The default handler can be used for cases where a matching route isn't available, for instance, to build a custom 404 response.

v1.0.x
- Initial release.

## Running under Mono
Watson works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```