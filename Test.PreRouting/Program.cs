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
        static void Main()
        {
            List<string> hostnames = new List<string>();
            hostnames.Add("127.0.0.1");

            Server server = new Server(hostnames, 9000, false, DefaultRoute);
            server.PreRoutingHandler = PreRoutingHandler;
             
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
                        Console.WriteLine("Listening: " + server.IsListening);
                        break;

                    case "dispose":
                        server.Dispose();
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("---");
            Console.WriteLine("  ?        help, this menu");
            Console.WriteLine("  q        quit the application");
            Console.WriteLine("  cls      clear the screen");
            Console.WriteLine("  state    indicate whether or not the server is listening");
            Console.WriteLine("  dispose  dispose the server object");
        }

        static async Task<bool> PreRoutingHandler(HttpContext ctx)
        {
            if (ctx.Request.RawUrlWithoutQuery.Equals("/foo"))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("Prerouting handler says 'bad request'!");
                return true;
            }
            else
            {
                return false;
            }
        }

        static async Task DefaultRoute(HttpContext ctx)
        { 
            if ((ctx.Request.Method == HttpMethod.POST
                || ctx.Request.Method == HttpMethod.PUT)
                && ctx.Request.Data != null
                && ctx.Request.ContentLength > 0)
            {
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
