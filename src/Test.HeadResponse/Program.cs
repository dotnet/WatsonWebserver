namespace Test.HeadResponse
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using RestWrapper;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    class Program
    {
        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;
        static string _Data = "Hello, world!";
        static int _Counter = 0;
        static int _Iterations = 10;

        static async Task Main(string[] args)
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
            _Server.Start();

            for (_Counter = 0; _Counter < _Iterations; _Counter++)
            {
                await SendHeadRequest();
            }

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;

            if (_Counter % 2 == 0)
            {
                Console.WriteLine("Responding using ctx.Response.Send");
                await Task.Delay(250);
                ctx.Response.ContentLength = _Data.Length;
                await ctx.Response.Send();
            }
            else
            {
                Console.WriteLine("Responding using ctx.Response.Send(len)");
                await Task.Delay(250);
                ctx.Response.ContentLength = _Data.Length;
                await ctx.Response.Send(_Data.Length);
            }

            return;
        }

        static async Task SendHeadRequest()
        {
            using (RestRequest req = new RestRequest(_Settings.Prefix, System.Net.Http.HttpMethod.Head)) 
            {
                Console.WriteLine("Sending REST request");

                using (RestResponse resp = await req.SendAsync())
                {
                    Console.WriteLine(resp.ToString());    
                }
            }
        }
    }
}
