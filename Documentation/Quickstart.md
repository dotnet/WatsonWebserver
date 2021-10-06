# Quickstart

Want to get up and running quickly with Watson webserver?  Here you go!

## Step 1 - Install with NuGet
```csharp
PM> Install-Package Watson
```

## Step 2 - Reference Within Your Code
```csharp
using WatsonWebserver;
```

## Step 3 - Create Default Route
```csharp
static async Task DefaultRoute(HttpContext ctx)
{ 
  ctx.Response.StatusCode = 200;
  ctx.Response.ContentType = "text/plain";
  await ctx.Response.Send("Hello from Watson!");
  return; 
}
```

## Step 4 - Instantiate and Start
```csharp
static void Main(string[] args)
{
  Server s = new Server("localhost", 8000, false, DefaultRoute);
  s.Start();
  Console.ReadLine();
}
```

## Step 5 - Test with cURL
```
> curl http://localhost:8000
Hello from Watson!
```
