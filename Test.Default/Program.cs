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
        static string _Hostname = "127.0.0.1";
        static int _Port = 8080;
        static bool _Ssl = false;
        static Server _Server = null;

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

            _Server.Routes.Dynamic.Add(HttpMethod.GET, new Regex("^/bar$"), BarRoute);
             
            _Server.Start();

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
                        _Server.Start();
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

        [StaticRoute(HttpMethod.GET, "/mirror")]
        static async Task MirrorRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(ctx.Request.ToJson(true));
            return;
        }

        [StaticRoute(HttpMethod.GET, "/hello")]
        static async Task HelloRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hello static route, defined using attributes");
            return;
        }

        static async Task HolaRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Hola static route");
            return;
        }

        [DynamicRoute(HttpMethod.PUT, "/foo")]
        static async Task FooWithoutIdRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo dynamic route, defined using attributes");
            return;
        }

        [DynamicRoute(HttpMethod.GET, "^/foo/\\d+$")]
        public static async Task FooWithIdRoute(HttpContext ctx)
        { 
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Foo with ID dynamic route, defined using attributes");
            return;
        }

        static async Task BarRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Bar dynamic route");
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
