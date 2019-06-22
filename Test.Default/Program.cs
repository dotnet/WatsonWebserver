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

            Server server = new Server(hostnames, 9000, false, RequestReceived);

            // server.AccessControl.Mode = AccessControlMode.DefaultDeny;
            // server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");
            // server.AccessControl.Whitelist.Add("127.0.0.1", "255.255.255.255");

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
            /*
            Console.WriteLine(req.ToString());
            if (req.QuerystringEntries != null && req.QuerystringEntries.Count > 0)
            {
                Console.WriteLine("");
                Console.WriteLine("Querystring Entries:");
                foreach (KeyValuePair<string, string> curr in req.QuerystringEntries)
                {
                    Console.WriteLine(curr.Key + ": " + curr.Value);
                }
            }
            */

            return new HttpResponse(req, 200, null, "text/plain", Encoding.UTF8.GetBytes("Watson says hello from the default route!"));
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
