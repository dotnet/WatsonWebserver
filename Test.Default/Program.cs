using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    static class Program
    {
        static Server _Server = null;

        static void Main()
        {
            List<string> hostnames = new List<string>();
            hostnames.Add("127.0.0.1");

            _Server = new Server(hostnames, 9000, false, DefaultRoute);
            _Server.Start();

            Console.WriteLine("Listening on http://127.0.0.1:9000");

            // _Server.AccessControl.Mode = AccessControlMode.DefaultDeny;
            // _Server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");
            // _Server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");

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
                        Console.WriteLine(_Server.Stats.ToString());
                        break;

                    case "stats reset":
                        _Server.Stats.Reset();
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

        static async Task DefaultRoute(HttpContext ctx)
        { 
            Console.WriteLine(ctx.Request.ToString());

            if ((ctx.Request.Method == HttpMethod.POST
                || ctx.Request.Method == HttpMethod.PUT)
                && ctx.Request.Data != null
                && ctx.Request.ContentLength > 0)
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send(ctx.Request.ContentLength, ctx.Request.Data);
                return;
            }
            else
            {
                ctx.Response.StatusCode = 200;
                await ctx.Response.Send("Watson says hello from the default route!");
                return;
            }
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
