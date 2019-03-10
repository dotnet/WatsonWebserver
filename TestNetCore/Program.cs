using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace TestNetCore
{
    static class Program
    {
        static void Main()
        {
            Server server = new Server("127.0.0.1", 9000, false, RequestReceived, true);

            bool runForever = true;
            while (runForever)
            {
                string userInput = WatsonCommon.InputString("Command [? for help] >", null, false);
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
            // for an encapsulated JSON response:
            // {"success":true,"md5":"BE3DB22E4FDF3021162C013320CEED09","data":"Watson says hello!"}
            // resp = new HttpResponse(req, true, 200, null, "text/plain", "Watson says hello!", false);

            // for a response containing only the string data...
            // Watson says hello!
            return new HttpResponse(req, true, 200, null, "text/plain", "Watson says hello from the default route!", true);
        }
    }
}
