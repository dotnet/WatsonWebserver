![alt tag](https://github.com/jchristn/watsonwebserver/blob/master/assets/watson.ico)

# Watson Webserver

[![NuGet Version](https://img.shields.io/nuget/v/Watson.svg?style=flat)](https://www.nuget.org/packages/Watson/) [![NuGet](https://img.shields.io/nuget/dt/Watson.svg)](https://www.nuget.org/packages/Watson) [![StackShare](https://img.shields.io/badge/tech-stack-0690fa.svg?style=flat)](https://stackshare.io/jchristn/watsonwebserver)

Simple, scalable, fast, async web server for processing RESTful HTTP/HTTPS requests, written in C#.

## New in v4.0.0

- Breaking changes to improve simplicity and reliability
- Consolidated settings into the ```Settings``` property
- Consolidated routing into the ```Routing``` property
- Use of ```EventHandler``` for events instead of ```Action```
- Use of ```ConfigureAwait``` for reliability within your application
- Simplified constructors
- ```Pages``` property to set how 404 and 500 responses should be sent, if not handled within your application
- Consolidated test applications
- Attribute-based routes now loaded automatically, removed ```LoadRoutes``` method
- Restructured ```HttpContext```, ```HttpRequest```, and ```HttpResponse``` for better usability

## Special Thanks

I'd like to extend a special thanks to those that have helped make Watson Webserver better.

- @notesjor @shdwp @Tutch @GeoffMcGrath @jurkovic-nikola @joreg @Job79 @at1993 @MartyIX

## Important Notes

- Using Watson may require elevation (administrative privileges) if binding an IP other than 127.0.0.1 or localhost
- The HTTP HOST header must match the specified binding
- Multiple bindings are supported in .NET Framework, but not (yet) in .NET Core 
- When using SSL, Watson will interact with certificates in the computer certificate store.  Refer to the wiki for details
- Watson Webserver will always check routes in the following order:
  - All requests are marshaled through the pre-routing handler
  - If the request is GET or HEAD, content routes will be evaluated next
  - Followed by static routes (any HTTP method)
  - Then dynamic (regex) routes (any HTTP method)
  - Then the default route (any HTTP method)
- When defining dynamic routes (regex), the longest match is used
  - If you wish to use first match or shortest match, modify ```Server.DynamicRoutes.Matcher.MatchPreference``` 
- If a matching content route exists:
  - And the content does not exist, a standard 404 is sent
  - And the content cannot be read, a standard 500 is sent
- When using a pre-routing handler, your handler should return:
  - ```True``` if the connection should be terminated
  - ```False``` if the connection should continue with further routing
- By default, Watson will permit all inbound connections
  - If you want to block certain IPs or networks, use ```Server.AccessControl.DenyList.Add(ip, netmask)```
  - If you only want to allow certain IPs or networks, and block all others, use:
    - ```Server.AccessControl.Mode = AccessControlMode.DefaultDeny```
    - ```Server.AccessControl.PermitList.Add(ip, netmask)```

## Simple Example

```csharp
using System.IO;
using System.Text;
using WatsonWebserver;

static void Main(string[] args)
{
  Server s = new Server("127.0.0.1", 9000, false, DefaultRoute);
  s.Start();
  Console.ReadLine();
}

static HttpResponse DefaultRoute(HttpRequest req)
{  
  ctx.Response.StatusCode = 200;
  await ctx.Response.Send("Hello from the default route!");
}
```

Then, open your browser to ```http://127.0.0.1:9000/```.

## Example using Routes

```csharp
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WatsonWebserver;

static void Main(string[] args)
{
  Server s = new Server("127.0.0.1", 9000, false, DefaultRoute);

  // add content routes
  s.Routes.Content.Add("/html/", true);
  s.Routes.Content.Add("/img/watson.jpg", false);

  // add static routes
  s.Routes.Static.Add(HttpMethod.GET, "/hello/", GetHelloRoute); 

  // add dynamic routes
  s.Routes.Dynamic.Add(HttpMethod.GET, new Regex("^/foo/\\d+$"), GetFooWithId);  
  s.Routes.Dynamic.Add(HttpMethod.GET, new Regex("^/foo/?$"), GetFoo); 

  // start the server
  s.Start();

  Console.WriteLine("Press ENTER to exit");
  Console.ReadLine();
}

static async Task GetHelloRoute(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the GET /hello static route!");
}
 
static async Task GetFooWithId(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the GET /foo/[id] dynamic route!");
}
 
static async Task GetFoo(HttpContext ctx)
{  
  await ctx.Response.Send("Hello from the GET /foo/ dynamic route!");
}

static async Task DefaultRoute(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the default route!");
}
```

## Example using Route Attributes
 
```csharp
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using WatsonWebserver;

static void Main(string[] args)
{
  Server s = new Server("127.0.0.1", 9000, false, DefaultRoute).LoadRoutes(); 
  s.Start();

  Console.WriteLine("Press ENTER to exit");
  Console.ReadLine();
}
 
[StaticRoute(HttpMethod.GET, "/hello")]
static async Task GetHelloRoute(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the GET /hello static route!");
}

[DynamicRoute(HttpMethod.GET, "^/foo/\\d+$")]
static async Task GetFooWithId(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the GET /foo/[id] dynamic route!");
}
 
[DynamicRoute(HttpMethod.GET, "^/foo/")]
static async Task GetFoo(HttpContext ctx)
{  
  await ctx.Response.Send("Hello from the GET /foo/ dynamic route!");
}

static async Task DefaultRoute(HttpContext ctx)
{ 
  await ctx.Response.Send("Hello from the default route!");
}
```

## Permit or Deny by IP or Network

```csharp
Server s = new Server("127.0.0.1", 9000, false, DefaultRoute);

// set default permit (permit any) with deny list to block specific IP addresses or networks
s.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
s.Settings.AccessControl.DenyList.Add("127.0.0.1", "255.255.255.255");  

// set default deny (deny all) with permit list to permit specific IP addresses or networks
s.Settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
s.Settings.AccessControl.PermitList.Add("127.0.0.1", "255.255.255.255");
```

## Chunked Transfer-Encoding

Watson supports both receiving chunked data and sending chunked data (indicated by the header ```Transfer-Encoding: chunked```).

### Receiving Chunked Data

```csharp
static async Task UploadData(HttpContext ctx)
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
static async Task DownloadChunkedFile(HttpContext ctx)
{
  using (FileStream fs = new FileStream("./img/watson.jpg", , FileMode.Open, FileAccess.Read))
  {
    ctx.Response.StatusCode = 200;
    ctx.Response.ChunkedTransfer = true;

    byte[] buffer = new byte[65536];
    int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length);
    if (bytesRead > 0)
    {
      // you'll want to check bytesRead vs buffer.Length, of course!
      await ctx.Response.SendChunk(buffer);
    }
    else
    {
      await ctx.Response.SendFinalChunk(buffer);
    }
  }

  return;
}
```

## Accessing from Outside Localhost

When you configure Watson to listen on ```127.0.0.1``` or ```localhost```, it will only respond to requests received from within the local machine.

To configure access from other nodes outside of ```localhost```, use the following:

- Specify the exact DNS hostname upon which Watson should listen in the Server constructor. The HOST header on incoming HTTP requests MUST match this value (this is an operating system limitation)
- If you want to listen on more than one hostname or IP address, use ```*``` or ```+```. You MUST run Watson as administrator for this to work (this is an operating system limitation)
- If you want to use a port number less than 1024, you MUST run Watson as administrator (this is an operating system limitation)
- Open a port on your firewall to permit traffic on the TCP port upon which Watson is listening
- You may have to add URL ACLs, i.e. URL bindings, within the operating system using the ```netsh``` command:
  - Check for existing bindings using ```netsh http show urlacl```
  - Add a binding using ```netsh http add urlacl url=http://[hostname]:[port]/ user=everyone listen=yes```
  - Where ```hostname``` and ```port``` are the values you are using in the constructor
  - If you are using SSL, you will need to install the certificate in the certificate store and retrieve the thumbprint
  - Refer to https://github.com/jchristn/WatsonWebserver/wiki/Using-SSL-on-Windows for more information, or if you are using SSL
- If you're still having problems, please do not hesitate to file an issue here, and I will do my best to help and update the documentation.

## Running in Docker

Please refer to the ```Test.Docker``` project and the ```Docker.md``` file therein.

## Running in Mono

While .NET Core is always preferred for non-Windows environments, Watson compiled using .NET Framework works well in Mono environments to the extent that we have tested it. It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).

NOTE: Windows accepts '0.0.0.0' as an IP address representing any interface.  On Mac and Linux you must be specified ('127.0.0.1' is also acceptable, but '0.0.0.0' is NOT).

```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```

## Version History

Refer to CHANGELOG.md for version history.
