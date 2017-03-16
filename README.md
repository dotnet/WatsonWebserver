# Watson Webserver

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/Watson/
[nuget-img]: https://badge.fury.io/nu/Object.svg

A simple C# async web server for handling incoming RESTful HTTP/HTTPS requests. 

## New in v1.1.0
- Added support for routes.  The default handler can be used for cases where a matching route isn't available, for instance, to build a custom 404 response.

## Test App
A test project is included which will help you exercise the class library.

## Example using Routes
```
using WatsonWebserver;

static void Main(string[] args)
{
   Server s = new Server("127.0.0.1", 9000, false, DefaultRoute, false);
   s.AddRoute("get", "/hello/", GetHelloRoute);
   s.AddRoute("get", "/world/", GetWorldRoute);
   Console.WriteLine("Press ENTER to exit");
   Console.ReadLine();
}

static HttpResponse GetHelloRoute(HttpRequest req)
{
   HttpResponse resp = new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /hello/ route!", true);
   return resp;
}

static HttpResponse GetWorldRoute(HttpRequest req)
{
   HttpResponse resp = new HttpResponse(req, true, 200, null, "text/plain", "Hello from the GET /world/ route!", true);
   return resp;
}

static HttpResponse DefaultRoute(HttpRequest req)
{
   HttpResponse resp = new HttpResponse(req, true, 200, null, "text/plain", "Hello from the default route!", true);
   return resp;
}
```

## Example using Default Route
```
using WatsonWebserver;

static void Main(string[] args)
{
   Server s = new Server("127.0.0.1", 9000, false, RequestReceived, false);
   Console.WriteLine("Press ENTER to exit");
   Console.ReadLine();
}

static HttpResponse RequestReceived(HttpRequest req)
{
   Console.WriteLine(req.ToString());
   HttpResponse resp = new HttpResponse(req, true, 200, null, "text/plain", "Watson says hello!", true);
   return resp;
}
```

## Running under Mono
Watson works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```