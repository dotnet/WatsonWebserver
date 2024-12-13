namespace Test.MaxConnections
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using RestWrapper;

    class Program
    {
        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static int _MaxConcurrentRequests = 5;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;

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
            _Server.Settings.IO.MaxRequests = _MaxConcurrentRequests;
            _Server.Start();

            for (int i = 0; i < 25; i++)
            {
                // Task.Run(() => ClientTask());
            }

            while (true)
            {
                Task.Delay(100).Wait();
                Console.Write("\r" + _Server.RequestCount + "                                 \r");
            }

            /*
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
            */
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            Console.WriteLine(ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " started");
            Task.Delay(5000).Wait();
            Console.WriteLine(ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " ended");
            await ctx.Response.Send();
            return;
        }

        static async Task ClientTask()
        {
            Console.WriteLine("Sending request");
            using (RestRequest req = new RestRequest("http://" + _Hostname + ":" + _Port + "/"))
            {
                using (RestResponse resp = await req.SendAsync())
                {
                    Console.WriteLine("Response received: " + resp.StatusDescription);
                }
            }
        }
    }
}
