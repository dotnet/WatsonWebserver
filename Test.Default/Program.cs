using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    static class Program
    {
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static bool _Ssl = false;
        static Server _Server = null;
        static Dictionary<string, string> _Metadata = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };

        static void Main()
        {
            _Server = new Server(_Hostname, _Port, false, DefaultRoute);

            _Server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
            _Server.Settings.AccessControl.DenyList.Add("1.1.1.1", "255.255.255.255");
            _Server.Routes.PreRouting = PreRoutingHandler;
            
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
            _Server.Routes.Dynamic.Add(HttpMethod.GET, new Regex("^/bar$"), BarRoute);
            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            StartServer();

            if (_Ssl) Console.WriteLine("Listening on https://" + _Hostname + ":" + _Port);
            else Console.WriteLine("Listening on http://" + _Hostname + ":" + _Port);
             
            bool runForever = true;
            while (runForever)
            {
                string userInput = InputString("Command [? for help] >", null, false);
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
            Console.WriteLine("Server started");
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            Console.WriteLine(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            Console.WriteLine("*** Server stopped");
        }

        [StaticRoute(HttpMethod.GET, "/mirror")]
        public static async Task MirrorRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(ctx.Request.ToJson(true));
            
            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        [StaticRoute(HttpMethod.GET, "/hello")]
        public static async Task HelloRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello static route, defined using attributes");
            
            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        static async Task HolaRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hola static route");
            
            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/{version}/param1/{id}")]
        public static async Task ParameterRoute1(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 1, with version " + ctx.Request.Url.Parameters["version"] + " and ID " + ctx.Request.Url.Parameters["id"]);

            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        [ParameterRoute(HttpMethod.GET, "/{version}/param2/{id}", null, "Test Metadata")]
        public static async Task ParameterRoute2(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Parameter route 2, with version " + ctx.Request.Url.Parameters["version"] + ", ID " + ctx.Request.Url.Parameters["id"] + ", and metadata");

            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        [DynamicRoute(HttpMethod.PUT, "/foo")]
        public static async Task FooWithoutIdRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo dynamic route, defined using attributes");

            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        [DynamicRoute(HttpMethod.GET, "^/foo/\\d+$")]
        public static async Task FooWithIdRoute(HttpContext ctx)
        { 
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo with ID dynamic route, defined using attributes");
            
            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        static async Task BarRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Bar dynamic route");

            Console.WriteLine(ctx.ToJson(true));
            return;
        }

        static async Task<bool> PreRoutingHandler(HttpContext ctx)
        {
            return false;
        }

        static async Task DefaultRoute(HttpContext ctx)
        { 
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Default route");

            Console.WriteLine(ctx.ToJson(true));
            return; 
        }

        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }
    }
}
