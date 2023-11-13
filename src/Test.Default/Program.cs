using GetSomeInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Core;
using WatsonWebserver.Lite;

namespace Test
{
    static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;
        static Dictionary<string, string> _Metadata = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };

        static void Main()
        {
            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };

            if (_UsingLite)
            {
                Console.WriteLine("Initializing webserver lite");
                _Server = new WatsonWebserver.Lite.WebserverLite(_Settings, DefaultRoute);
            }
            else
            {
                Console.WriteLine("Initializing webserver");
                _Server = new WatsonWebserver.Webserver(_Settings, DefaultRoute);
            }

            _Server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
            _Server.Settings.AccessControl.DenyList.Add("1.1.1.1", "255.255.255.255");
            _Server.Routes.PreRouting = PreRoutingHandler;
            _Server.Routes.PostRouting = PostRoutingHandler;

            _Server.Routes.PreAuthentication.Content.Add("/html/", true);
            _Server.Routes.PreAuthentication.Content.Add("/large/", true);
            _Server.Routes.PreAuthentication.Content.Add("/img/watson.jpg", false);
            _Server.Routes.PreAuthentication.ContentHandler = new ContentRouteHandler(_Server.Routes.PreAuthentication.Content);

            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hello", HelloRoute);
            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hola", HolaRoute);
            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/mirror", MirrorRoute);
            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/login", async (ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("Login static route");
                return;
            });

            _Server.Routes.PreAuthentication.Parameter.Matcher.Logger = Console.WriteLine;
            _Server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/user/{id}", GetUserByIdRoute);
            _Server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{version}/param1/{id}", ParameterRoute1);
            _Server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{version}/param1/{id}", ParameterRoute2, Guid.NewGuid(), "TestMetadata");

            _Server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/bar$"), BarRoute);
            _Server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.PUT, new Regex("^/foo$"), FooWithoutIdRoute);
            _Server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/foo/\\d+$"), FooWithIdRoute);

            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            Console.WriteLine("Starting server on: " + _Settings.Prefix);

            _Server.Start();

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
                        _Server.Start();
                        break;

                    case "stop":
                        _Server.Stop();
                        break;

                    case "stats":
                        Console.WriteLine(_Server.Statistics.ToString());
                        break;

                    case "stats reset":
                        _Server.Statistics.Reset();
                        break;

                    case "dispose":
                        _Server.Dispose();
                        break;
                }
            }
        }

        static void Menu()
        {
            bool isListening = false;
            if (_Server != null) isListening = _Server.IsListening;

            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  q              quit the application");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  state          indicate whether or not the server is listening");
            Console.WriteLine("  start          start listening for new connections (is listening: " + isListening + ")");
            Console.WriteLine("  stop           stop listening for new connections  (is listening: " + isListening + ")");
            Console.WriteLine("  stats          display webserver statistics");
            Console.WriteLine("  stats reset    reset webserver statistics");
            Console.WriteLine("  dispose        dispose of the server");
            Console.WriteLine("");
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        public static async Task MirrorRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(_Server.Serializer.SerializeJson(ctx, true));
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task HelloRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello static route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task GetUserByIdRoute(HttpContextBase ctx)
        {
            string id = ctx.Request.Url.Parameters["id"];
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Get user by ID " + id + " route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        static async Task HolaRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hola static route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task ParameterRoute1(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 1, with version " + ctx.Request.Url.Parameters["version"] + " and ID " + ctx.Request.Url.Parameters["id"]);
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task ParameterRoute2(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 2, with version " + ctx.Request.Url.Parameters["version"] + ", ID " + ctx.Request.Url.Parameters["id"] + ", and metadata");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task FooWithoutIdRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo dynamic route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        public static async Task FooWithIdRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo with ID dynamic route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        static async Task BarRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Bar dynamic route");
            _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
            return;
        }

        static async Task<bool> PreRoutingHandler(HttpContextBase ctx)
        {
            ctx.Metadata = "Hello, world!";
            return false;
        }

        static async Task PostRoutingHandler(HttpContextBase ctx)
        {
            Console.WriteLine(ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + ": " + ctx.Response.StatusCode + " (" + ctx.Timestamp.TotalMs + "ms)");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            try
            {
                ctx.Response.Headers.Add("Connection", "close");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("Default route");
                _Server.Events.Logger(_Server.Serializer.SerializeJson(ctx, true));
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(_Server.Serializer.SerializeJson(e, true));
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send();
                return;
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
