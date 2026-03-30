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
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;
        static string _Data = "Hello, world!";
        static int _Counter = 0;
        static int _Iterations = 10;

        static async Task Main(string[] args)
        {

            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };
            Console.WriteLine("Initializing webserver");
            _Server = new WatsonWebserver.Webserver(_Settings, DefaultRoute);

            Console.WriteLine("Listening on " + _Settings.Prefix);
            _Server.Start();

            Console.WriteLine("");
            Console.WriteLine("Available routes (all via default handler):");
            Console.WriteLine("  HEAD  (any path)  - Returns headers with content-length, no body");
            Console.WriteLine("  GET   (any path)  - Returns response with body");
            Console.WriteLine("");

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


