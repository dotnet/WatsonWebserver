namespace Test.HostBuilder
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using RestWrapper;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Lite;

    public static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static bool _Ssl = false;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;

        public static async Task Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args[0].Equals("lite")) _UsingLite = true;
            }

            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };

            if (_UsingLite)
            {
                Console.WriteLine("Initializing webserver lite");
                _Server = new WatsonWebserver.Lite.Extensions.HostBuilderExtension.HostBuilder(_Hostname, _Port, _Ssl, DefaultRoute)
                    .MapPreRoutingRoute(PreRoutingHandler)
                    .MapContentRoute("/preauth/content", false, false)
                    .MapStaticRoute(HttpMethod.GET, "/preauth/static", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication static route /preauth/static");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapParameterRoute(HttpMethod.GET, "/preauth/parameter/{id}", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication parameter route /preauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapDynamicRoute(HttpMethod.GET, new Regex("^/preauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication dynamic route /preauth/dynamic");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapAuthenticationRoute(AuthenticationRoute)
                    .MapContentRoute("/postauth/content", false, true)
                    .MapStaticRoute(HttpMethod.GET, "/postauth/static", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication static route /postauth/static");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapParameterRoute(HttpMethod.GET, "/postauth/parameter/{id}", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication parameter route /postauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapDynamicRoute(HttpMethod.GET, new Regex("^/postauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication dynamic route /postauth/dynamic");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapPostRoutingRoute(PostRoutingHandler)
                    .Build();
            }
            else
            {
                Console.WriteLine("Initializing webserver");
                _Server = new WatsonWebserver.Extensions.HostBuilderExtension.HostBuilder(_Hostname, _Port, _Ssl, DefaultRoute)
                    .MapPreRoutingRoute(PreRoutingHandler)
                    .MapContentRoute("/preauth/content", false, false)
                    .MapStaticRoute(HttpMethod.GET, "/preauth/static", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication static route /preauth/static");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapParameterRoute(HttpMethod.GET, "/preauth/parameter/{id}", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication parameter route /preauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapDynamicRoute(HttpMethod.GET, new Regex("^/preauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from pre-authentication dynamic route /preauth/dynamic");
                        await ctx.Response.Send();
                        return;
                    }, null, false)
                    .MapAuthenticationRoute(AuthenticationRoute)
                    .MapContentRoute("/postauth/content", false, true)
                    .MapStaticRoute(HttpMethod.GET, "/postauth/static", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication static route /postauth/static");
                        await ctx.Response.Send();
                        return;
                    }, null, true)
                    .MapParameterRoute(HttpMethod.GET, "/postauth/parameter/{id}", async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication parameter route /postauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                        await ctx.Response.Send();
                        return;
                    }, null, true)
                    .MapDynamicRoute(HttpMethod.GET, new Regex("^/postauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
                    {
                        Console.WriteLine("| Responding from post-authentication dynamic route /postauth/dynamic");
                        await ctx.Response.Send();
                        return;
                    }, null, true)
                    .MapPostRoutingRoute(PostRoutingHandler)
                    .Build();
            }

            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            Console.WriteLine("Starting server on: " + _Settings.Prefix);
            _Server.Start();

            List<string> urls = new List<string>
            {
                _Settings.Prefix + "preauth/static",
                _Settings.Prefix + "preauth/content",
                _Settings.Prefix + "preauth/parameter/5",
                _Settings.Prefix + "preauth/dynamic/10",
                _Settings.Prefix + "postauth/static",
                _Settings.Prefix + "postauth/content",
                _Settings.Prefix + "postauth/parameter/5",
                _Settings.Prefix + "postauth/dynamic/10",
                _Settings.Prefix + "foo1",
                _Settings.Prefix + "foo2"
            };

            foreach (string url in urls)
            {
                Console.WriteLine("");
                Console.WriteLine("URL: " + url);

                using (RestRequest req = new RestRequest(url))
                {
                    using (RestResponse resp = await req.SendAsync())
                    {
                        Console.WriteLine("Received response: " + resp.StatusCode);
                        Task.Delay(1000).Wait();
                    }
                }
            }
        }

        private static async Task AuthenticationRoute(HttpContextBase @base)
        {
            Console.WriteLine("| In authentication");
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        static async Task PreRoutingHandler(HttpContextBase ctx)
        {

        }

        static async Task PostRoutingHandler(HttpContextBase ctx)
        {
            Console.WriteLine(ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + ": " + ctx.Response.StatusCode + " (" + ctx.Timestamp.TotalMs + "ms)");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            try
            {
                Console.WriteLine("| Responding from the default route");
                ctx.Response.Headers.Add("Connection", "close");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("Default route");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send();
                return;
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
