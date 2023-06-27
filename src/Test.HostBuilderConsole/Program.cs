using WatsonWebserver;
using WatsonWebserver.Extensions.HostBuilderExtension;

Console.WriteLine("Staring Host ...");



HostBuilder builder = new HostBuilder("localhost", 3000, false, Home);
builder.MapStaticRoute(WatsonWebserver.HttpMethod.GET, Route1, $"/{nameof(Route1)}")
    .MapStaticRoute(WatsonWebserver.HttpMethod.GET, Route2, $"/{nameof(Route2)}");


var app = builder.Build();
app.Start();


Console.WriteLine("watson webserver host started");



static async Task Home(HttpContext ctx)
    => await ctx.Response.Send("hello from home");



static async Task Route1(HttpContext ctx)
    => await ctx.Response.Send($"Hello from {nameof(Route1)}");

static async Task Route2(HttpContext ctx)
    => await ctx.Response.Send($"Hello from {nameof(Route2)}");