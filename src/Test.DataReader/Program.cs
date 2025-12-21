namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using GetSomeInput;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Lite;

    static class Program
    {
        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;
        static DefaultSerializationHelper _Serializer = new DefaultSerializationHelper();

        private enum Mode
        {
            Bytes,
            Text, 
            Json
        }

        private static Mode _Mode = Mode.Bytes;

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-lite", StringComparison.OrdinalIgnoreCase))
                    {
                        _UsingLite = true;
                        break;
                    }
                }
            }

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
                _Server = new Webserver(_Settings, DefaultRoute);
            }

            Console.WriteLine("Listening on " + _Settings.Prefix);
            _Server.Start();

            bool runForever = true;
            while (runForever)
            {
                string userInput = Inputty.GetString("Command [?/help]:", null, false);
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

                    case "stats":
                        Console.WriteLine(_Server.Statistics.ToString());
                        break;

                    case "stats reset":
                        _Server.Statistics.Reset();
                        break;

                    case "mode":
                        Console.WriteLine("Valid modes: Text, Bytes, Json");
                        _Mode = (Mode)(Enum.Parse(typeof(Mode), Inputty.GetString("Mode:", "Text", false)));
                        break;

                    case "dispose":
                        _Server.Dispose();
                        break;
                }
            }
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  q              quit the application");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  state          indicate whether or not the server is listening");
            Console.WriteLine("  stats          display webserver statistics");
            Console.WriteLine("  stats reset    reset webserver statistics");
            Console.WriteLine("  mode           set data mode, currently: " + _Mode);
            Console.WriteLine("  dispose        dispose of the server");
            Console.WriteLine("");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            Console.WriteLine(_Serializer.SerializeJson(ctx, true));

            string dataStr = null;
            byte[] dataBytes = null;
            Person p = null;

            switch (_Mode)
            {
                case Mode.Text:
                    dataStr = ctx.Request.DataAsString;
                    Console.WriteLine("String data:" + Environment.NewLine + dataStr != null ? dataStr : "(null)");
                    break;
                case Mode.Bytes:
                    dataBytes = ctx.Request.DataAsBytes;
                    Console.WriteLine("Byte data:" + Environment.NewLine + dataBytes != null ? ByteArrayToHex(dataBytes) : "(null)");
                    break; 
                case Mode.Json:
                    p = _Serializer.DeserializeJson<Person>(ctx.Request.DataAsString);
                    Console.WriteLine("Person data (from JSON):" + Environment.NewLine + p != null ? p.ToString() : "(null)");
                    break;
                default:
                    Console.WriteLine("Unknown mode '" + _Mode + "', reading bytes instead");
                    dataBytes = ctx.Request.DataAsBytes;
                    Console.WriteLine("Byte data:" + Environment.NewLine + ByteArrayToHex(dataBytes));
                    break; 
            }

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send();
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
