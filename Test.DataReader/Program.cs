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
        private enum Mode
        {
            Bytes,
            Text, 
            Json
        }

        private static Mode _Mode = Mode.Bytes;

        static void Main()
        {
            List<string> hostnames = new List<string>();
            hostnames.Add("127.0.0.1"); 
            Server server = new Server(hostnames, 9000, false, DefaultRoute);
            server.Start();

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

                    case "stats":
                        Console.WriteLine(server.Stats.ToString());
                        break;

                    case "stats reset":
                        server.Stats.Reset();
                        break;

                    case "mode":
                        Console.WriteLine("Valid modes: Text, Bytes, Json");
                        _Mode = (Mode)(Enum.Parse(typeof(Mode), InputString("Mode:", "Text", false)));
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("---");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  q              quit the application");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  state          indicate whether or not the server is listening");
            Console.WriteLine("  dispose        dispose the server object");
            Console.WriteLine("  stats          display webserver statistics");
            Console.WriteLine("  stats reset    reset webserver statistics");
            Console.WriteLine("  mode           set data mode, currently: " + _Mode);
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.ToString());

            string dataStr = null;
            byte[] dataBytes = null;
            Person p = null;

            switch (_Mode)
            {
                case Mode.Text:
                    dataStr = ctx.Request.DataAsString();
                    Console.WriteLine("String data:" + Environment.NewLine + dataStr != null ? dataStr : "(null)");
                    break;
                case Mode.Bytes:
                    dataBytes = ctx.Request.DataAsBytes();
                    Console.WriteLine("Byte data:" + Environment.NewLine + dataBytes != null ? ByteArrayToHex(dataBytes) : "(null)");
                    break; 
                case Mode.Json:
                    p = ctx.Request.DataAsJsonObject<Person>();
                    Console.WriteLine("Person data (from JSON):" + Environment.NewLine + p != null ? p.ToString() : "(null)");
                    break;
                default:
                    Console.WriteLine("Unknown mode '" + _Mode + "', reading bytes instead");
                    dataBytes = ctx.Request.DataAsBytes();
                    Console.WriteLine("Byte data:" + Environment.NewLine + ByteArrayToHex(dataBytes));
                    break; 
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send();
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

        private static string ByteArrayToHex(byte[] data)
        {
            StringBuilder hex = new StringBuilder(data.Length * 2);
            foreach (byte b in data) hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public int Age { get; set; }

            public override string ToString()
            {
                return "Hello, my name is " + FirstName + " " + LastName + " and I am " + Age + " years old!";
            }

            public Person()
            {

            }
        }
    }
}
