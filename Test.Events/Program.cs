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
        static void Main()
        {
            Server server = new Server("127.0.0.1", 9000, false, RequestReceived);
            // server.AccessControl.Mode = AccessControlMode.DefaultDeny;
            // server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");
            // server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");

            server.Events.AccessControlDenied = AccessControlDenied;
            server.Events.ConnectionReceived = ConnectionReceived;
            server.Events.ExceptionEncountered = ExceptionEncountered;
            server.Events.RequestReceived = RequestReceived;
            server.Events.ResponseSent = ResponseSent;
            server.Events.ServerDisposed = ServerDisposed;
            server.Events.ServerStopped = ServerStopped;

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

        static HttpResponse RequestReceived(HttpRequest req)
        {
            Console.WriteLine(req.ToString());

            if ((req.Method == HttpMethod.POST
                || req.Method == HttpMethod.PUT)
                && req.Data != null
                && req.ContentLength > 0)
            {
                return new HttpResponse(req, 200, null, "text/plain", req.Data);
            }
            else
            {
                return new HttpResponse(req, 200, null, "text/plain", Encoding.UTF8.GetBytes("Watson says hello from the default route!"));
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

        static bool AccessControlDenied(string ip, int port, string method, string url)
        {
            Console.WriteLine("AccessControlDenied [" + ip + ":" + port + "] " + method + " " + url);
            return true;
        }

        static bool ConnectionReceived(string ip, int port)
        {
            Console.WriteLine("ConnectionReceived [" + ip + ":" + port + "]");
            return true;
        }

        static bool ExceptionEncountered(string ip, int port, Exception e)
        {
            Console.WriteLine("ExceptionEncountered [" + ip + ":" + port + "]: " + Environment.NewLine + e.ToString());
            return true;
        }

        static bool RequestReceived(string ip, int port, string method, string url)
        {
            Console.WriteLine("RequestReceived [" + ip + ":" + port + "] " + method + " " + url);
            return true;
        }

        static bool ResponseSent(string ip, int port, string method, string url, int status)
        {
            Console.WriteLine("ResponseSent [" + ip + ":" + port + "] " + method + " " + url + " status " + status);
            return true;
        }

        static bool ServerDisposed()
        {
            Console.WriteLine("ServerDisposed");
            return true;
        }

        static bool ServerStopped()
        {
            Console.WriteLine("ServerStopped");
            return true;
        }
    }
}
