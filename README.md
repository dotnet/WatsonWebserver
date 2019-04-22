# Watson Webserver

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Watson/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# async web server for handling incoming RESTful HTTP/HTTPS requests. 

## New in v1.6.x

- Fix URL encoding (using System.Net.WebUtility.UrlDecode instead of Uri.EscapeString)
- Refactored content routes, static routes, and dynamic routes (breaking change)
- Added default permit/deny operation along with whitelist and blacklist

## Test App

A test project is included which will help you exercise the class library.

## Important Notes

- Using Watson may require elevation (administrative privileges) if binding an IP other than 127.0.0.1
- The HTTP HOST header must match the specified binding
- .NET Framework supports multiple bindings, .NET Core does not (yet)
- Watson Webserver will always check routes in the following order:
  - If the request is GET or HEAD, content routes will first be checked
  - Then static routes will be evaluated
  - Then dynamic (regex) routes will be evaluated
  - Then the default route
- When defining dynamic routes (regular expressions), be sure to add the most specific routes first.  Dynamic routes are evaluated in-order and the first match is used.
- If a matching content route exists:
  - And the content does not exist, a standard 404 is sent
  - And the content cannot be read, a standard 500 is sent
- By default, Watson will permit all inbound connections
  - If you want to block certain IPs or networks, use ```Server.AccessControl.Blacklist.Add(ip, netmask)```
  - If you only want to allow certain IPs or networks, and block all others, use:
    - ```Server.AccessControl.Mode = AccessControlMode.DefaultDeny```
    - ```Server.AccessControl.Whitelist.Add(ip, netmask)```
    
## Example using Routes
```
using WatsonWebserver;

static void Main(string[] args)
{
   List<string> hostnames = new List<string>();
   hostnames.Add("127.0.0.1");
   hostnames.Add("www.localhost.com");
   Server s = new Server(hostnames, 9000, false, DefaultRoute, true);

   // set default permit (permit any) with blacklist to block specific IP addresses or networks
   s.AccessControl.Mode = AccessControlMode.DefaultPermit;
   s.AccessControl.Blacklist.Add("127.0.0.1", "255.255.255.255");  

   // set default deny (deny all) with whitelist to permit specific IP addresses or networks
   s.AccessControl.Mode = AccessControlMode.DefaultDeny;
   s.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");

   // add content routes
   s.ContentRoutes.Add("/html/", true);
   s.ContentRoutes.Add("/img/watson.jpg", false);

   // add static routes
   s.StaticRoutes.Add(HttpMethod.GET, "/hello/", GetHelloRoute);
   s.StaticRoutes.Add(HttpMethod.GET, "/world/", GetWorldRoute);

   // add dynamic routes
   s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/\\d+$"), GetFooWithId);
   s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/(.*?)/(.*?)/?$"), GetFooMultipleChildren);
   s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/(.*?)/?$"), GetFooOneChild);
   s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/?$"), GetFoo); 

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

v1.5.x
- Added a new constructor allowing Watson to support multiple listener hostnames
- Retarget to support both .NET Core 2.0 and .NET Framework 4.6.2.
- Fix for attaching request body data to the HttpRequest object (thanks @user4000!)

v1.4.x
- Retarget to .NET Framework 4.6.2
- Enum for HTTP method instead of string (breaking change)

v1.2.x
- Bugfix for content routes that have spaces or ```+``` (thanks @Linqx)
- Support for passing an object as Data to HttpResponse (will be JSON serialized)
- Support for implementing your own OPTIONS handler (for CORS and other use cases)
- Bugfix for dispose (thank you @AChmieletzki)
- Static methods for building HttpRequest from various sources and conversion
- Static input methods
- Better initialization of object members
- More HTTP status codes (see https://en.wikipedia.org/wiki/List_of_HTTP_status_codes)
- Fix for content routes (thank you @KKoustas!)
- Fix for Xamarin IOS and Android (thank you @Tutch!)
- Added content routes for serving static files.
- Dynamic route support using C#/.NET regular expressions (see RegexMatcher library https://github.com/jchristn/RegexMatcher).
- IsListening property

v1.1.x
- Added support for static routes.  The default handler can be used for cases where a matching route isn't available, for instance, to build a custom 404 response.

v1.0.x
- Initial release.

## Running under Mono

While .NET Core is always preferred for non-Windows environments, Watson compiled using .NET Framework works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```