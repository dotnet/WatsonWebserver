using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test.Events
{
    static class Program
    {
        static Server _Server = null;

        static void Main()
        {
            _Server = new Server("127.0.0.1", 9000, false, DefaultRoute);
            // _Server.AccessControl.Mode = AccessControlMode.DefaultDeny;
            // _Server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");
            // _Server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");

            _Server.Events.AccessControlDenied = AccessControlDenied;
            _Server.Events.ConnectionReceived = ConnectionReceived;
            _Server.Events.RequestorDisconnected = RequestorDisconnected;
            _Server.Events.ExceptionEncountered = ExceptionEncountered;
            _Server.Events.RequestReceived = RequestReceived;
            _Server.Events.ResponseSent = ResponseSent;
            _Server.Events.ServerDisposed = ServerDisposed;
            _Server.Events.ServerStopped = ServerStopped;

            _Server.Start();

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
            Console.WriteLine("  start    start listening for new connections (is listening: " + (_Server != null ? _Server.IsListening.ToString() : "False"));
            Console.WriteLine("  stop     stop listening for new connections  (is listening: " + (_Server != null ? _Server.IsListening.ToString() : "False"));
            Console.WriteLine("  dispose  dispose the server object");
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.ToString());

            if (ctx.Request.Method == HttpMethod.GET)
            {
                if (ctx.Request.RawUrlWithoutQuery.Equals("/delay"))
                {
                    await Task.Delay(10000);
                }
            }

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

        static void AccessControlDenied(string ip, int port, string method, string url)
        {
            Console.WriteLine("AccessControlDenied [" + ip + ":" + port + "] " + method + " " + url); 
        }

        static void RequestorDisconnected(string ip, int port, string method, string url)
        {
            Console.WriteLine("RequestorDisconnected [" + ip + ":" + port + "] " + method + " " + url); 
        }

        static void ConnectionReceived(string ip, int port)
        {
            Console.WriteLine("ConnectionReceived [" + ip + ":" + port + "]"); 
        }

        static void ExceptionEncountered(string ip, int port, Exception e)
        {
            Console.WriteLine("ExceptionEncountered [" + ip + ":" + port + "]: " + Environment.NewLine + e.ToString()); 
        }

        static void RequestReceived(string ip, int port, string method, string url)
        {
            Console.WriteLine("RequestReceived [" + ip + ":" + port + "] " + method + " " + url); 
        }

        static void ResponseSent(string ip, int port, string method, string url, int status, double totalTimeMs)
        {
            Console.WriteLine("ResponseSent [" + ip + ":" + port + "] " + method + " " + url + " status " + status + " " + totalTimeMs + "ms"); 
        }

        static void ServerDisposed()
        {
            Console.WriteLine("ServerDisposed"); 
        }

        static void ServerStopped()
        {
            Console.WriteLine("ServerStopped"); 
        }
    }
}
