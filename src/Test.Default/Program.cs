using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GetSomeInput;
using WatsonWebserver;

namespace Test
{
    static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        static string _Hostname = "0.0.0.0";
        static int _Port = 8080;
        static bool _Ssl = false;
        static Server _Server = null;
        static Dictionary<string, string> _Metadata = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };

        static void Main()
        {
            _Server = new Server(_Hostname, _Port, _Ssl, DefaultRoute);

            /*
            _Server = new Server();
            _Server.Routes.Default = DefaultRoute;
            */

            _Server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
            _Server.Settings.AccessControl.DenyList.Add("1.1.1.1", "255.255.255.255");
            _Server.Routes.PreRouting = PreRoutingHandler;
            _Server.Routes.PostRouting = PostRoutingHandler;
            
            _Server.Routes.Content.Add("/html/", true);
            _Server.Routes.Content.Add("/large/", true);
            _Server.Routes.Content.Add("/img/watson.jpg", false);

            _Server.Routes.Static.Add(HttpMethod.GET, "/hola", HolaRoute);
            _Server.Routes.Static.Add(HttpMethod.GET, "/login", async (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("Login static route");
                return;
            });

            _Server.Routes.Parameter.Matcher.Logger = Console.WriteLine;
            _Server.Routes.Parameter.Add(HttpMethod.GET, "/user/{id}", GetUserByIdRoute);
            _Server.Routes.Dynamic.Add(HttpMethod.GET, new Regex("^/bar$"), BarRoute);
            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            StartServer();

            Console.Write("Listening on:");
            foreach (string prefix in _Server.Settings.Prefixes)
            {
                Console.Write(" " + prefix);
            }
            Console.WriteLine();
 
            bool runForever = true;
            while (runForever)
            {
                string userInput = Inputty.GetString("Command [? for help] >", null, false);
                switch (userInput.ToLower())
                {
                    case "?":
                        Menu();
                        break;

                    case "q":
                        runForever = false;
                        break;

                    case "c":
                    case "cls":
                        Console.Clear();
                        break;

                    case "state":
                        Console.WriteLine("Listening: " + _Server.IsListening);
                        break;

                    case "start":
                        StartServer();
                        break;

                    case "stop":
                        _Server.Stop();
                        break;

                    case "dispose":
                        _Server.Dispose();
                        break;

                    case "stats":
                        Console.WriteLine(_Server.Statistics.ToString());
                        break;

                    case "stats reset":
                        _Server.Statistics.Reset();
                        break;
                }
            }
        }

        static void Menu()
        {
            bool isListening = false;
            if (_Server != null) isListening = _Server.IsListening;

            Console.WriteLine("---");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  q              quit the application");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  state          indicate whether or not the server is listening");
            Console.WriteLine("  start          start listening for new connections (is listening: " + isListening + ")");
            Console.WriteLine("  stop           stop listening for new connections  (is listening: " + isListening + ")");
            Console.WriteLine("  dispose        dispose the server object");
            Console.WriteLine("  stats          display webserver statistics");
            Console.WriteLine("  stats reset    reset webserver statistics");
        }

        static async void StartServer()
        {
            Console.WriteLine("Starting server");
            await _Server.StartAsync();
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        [StaticRoute(HttpMethod.GET, "/mirror")]
        public static async Task MirrorRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(_Server.SerializationHelper.SerializeJson(ctx, true));
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        [StaticRoute(HttpMethod.POST, "/mirror/1")]
        public static async Task MirrorPostRoute1(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(ctx.Request.DataAsString);
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            _Server.Events.Logger("Request data  : " + ctx.Request.DataAsString);
            _Server.Events.Logger("Response data : " + ctx.Response.DataAsString);
        }

        [StaticRoute(HttpMethod.POST, "/mirror/2")]
        public static async Task MirrorPostRoute2(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(ctx.Request.DataAsBytes);
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            _Server.Events.Logger("Request data  : " + ctx.Request.DataAsString);
            _Server.Events.Logger("Response data : " + ctx.Response.DataAsString);
        }

        [StaticRoute(HttpMethod.POST, "/mirror/3")]
        public static async Task MirrorPostRoute3(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(ctx.Request.ContentLength, ctx.Request.Data);
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            _Server.Events.Logger("Request data  : " + ctx.Request.DataAsString);
            _Server.Events.Logger("Response data : " + ctx.Response.DataAsString);
        }

        [StaticRoute(HttpMethod.GET, "/hello")]
        public static async Task HelloRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello static route, defined using attributes");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        public static async Task GetUserByIdRoute(HttpContext ctx)
        {
            string id = ctx.Request.Url.Parameters["id"];
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Get user by ID " + id + " route");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        static async Task HolaRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hola static route");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/{version}/param1/{id}")]
        public static async Task ParameterRoute1(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 1, with version " + ctx.Request.Url.Parameters["version"] + " and ID " + ctx.Request.Url.Parameters["id"]);
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/{version}/param2/{id}", null, "Test Metadata")]
        public static async Task ParameterRoute2(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 2, with version " + ctx.Request.Url.Parameters["version"] + ", ID " + ctx.Request.Url.Parameters["id"] + ", and metadata");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        [DynamicRoute(HttpMethod.PUT, "/foo")]
        public static async Task FooWithoutIdRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo dynamic route, defined using attributes");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        [DynamicRoute(HttpMethod.GET, "^/foo/\\d+$")]
        public static async Task FooWithIdRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo with ID dynamic route, defined using attributes");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        static async Task BarRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Bar dynamic route");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

        static async Task<bool> PreRoutingHandler(HttpContext ctx)
        {
            ctx.Metadata = "Hello, world!";
            return false;
        }

        static async Task PostRoutingHandler(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + ": " + ctx.Response.StatusCode + " (" + ctx.Timestamp.TotalMs + "ms)");
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            // Console.WriteLine(ctx.Metadata.ToString());
            ctx.Response.Headers.Add("Connection", "close");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Default route");
            _Server.Events.Logger(_Server.SerializationHelper.SerializeJson(ctx, true));
            return;
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
