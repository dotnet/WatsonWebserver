namespace Test.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GetSomeInput;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
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
        static bool _UseDefaultSerializer = true;
        static Dictionary<string, string> _Metadata = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };
        static Random _Random = new Random();

        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args[0].Equals("lite")) _UsingLite = true;
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
            _Server.Events.Logger = Console.WriteLine;

            StartServer();

            bool runForever = true;
            while (runForever)
            {
                string userInput = Inputty.GetString("Command [? for help] >", null, false);
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

                    case "stats":
                        Console.WriteLine(_Server.Statistics.ToString());
                        break;

                    case "stats reset":
                        _Server.Statistics.Reset();
                        break;

                    case "dispose":
                        _Server.Dispose();
                        break;
                }
            }
        }

        static void Menu()
        {
            bool isListening = false;
            if (_Server != null) isListening = _Server.IsListening;

            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?              help, this menu");
            Console.WriteLine("  q              quit the application");
            Console.WriteLine("  cls            clear the screen");
            Console.WriteLine("  state          indicate whether or not the server is listening");
            Console.WriteLine("  start          start listening for new connections (is listening: " + isListening + ")");
            Console.WriteLine("  stop           stop listening for new connections  (is listening: " + isListening + ")");
            Console.WriteLine("  stats          display webserver statistics");
            Console.WriteLine("  stats reset    reset webserver statistics");
            Console.WriteLine("  dispose        dispose of the server");
            Console.WriteLine("");
        }

        static async void StartServer()
        {
            Console.WriteLine("Starting server");
            await _Server.StartAsync();
            Console.WriteLine("Server started");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            try
            {
                string serializer = "";

                if (_UseDefaultSerializer)
                {
                    serializer = "System.Text.Json";
                    _Server.Serializer = new DefaultSerializationHelper();
                }
                else
                {
                    serializer = "Newtonsoft.Json";
                    _Server.Serializer = new NewtonsoftSerializer();
                }

                Person p = Person.Random(_Random, serializer);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.Send(_Server.Serializer.SerializeJson(p, true));

                _UseDefaultSerializer = !_UseDefaultSerializer;
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }

    internal class NewtonsoftSerializer : ISerializationHelper
    {
        public T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public string SerializeJson(object obj, bool pretty = true)
        {
            if (!pretty)
            {
                return JsonConvert.SerializeObject(obj);
            }
            else
            {
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
        }
    }

    public class Person
    {
        public int Age { get; set; } = 0;
        public string FirstName { get; set; } = null;
        public string LastName { get; set; } = null;
        public string Serializer { get; set; } = null;

        internal List<string> FirstNames = new List<string>
        {
            "Joel",
            "Maria",
            "Jason",
            "Sienna",
            "Maribel",
            "Salma",
            "Khaleesi",
            "Watson",
            "Jenny",
            "Jessica",
            "Jesus",
            "Lila",
            "Tuco",
            "Walter",
            "Jesse",
            "Mike"
        };

        internal List<string> LastNames = new List<string>
        {
            "Christner",
            "Sanchez",
            "Mendoza",
            "White",
            "Salamanca",
            "Pinkman"
        };

        public static Person Random(Random rand, string serializer)
        {
            Person p = new Person();
            p.Age = rand.Next(0, 100);
            p.FirstName = p.FirstNames[rand.Next(0, (p.FirstNames.Count - 1))];
            p.LastName = p.LastNames[rand.Next(0, (p.LastNames.Count - 1))];
            p.Serializer = serializer;
            return p;
        }
    }
}
