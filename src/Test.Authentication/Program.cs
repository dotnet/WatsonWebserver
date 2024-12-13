namespace Test.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using RestWrapper;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Lite;

    static class Program
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;
        static int _Counter = 0;
        static int _Iterations = 10;

        static async Task Main()
        {
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
                _Server = new WatsonWebserver.Webserver(_Settings, DefaultRoute);
            }

            _Server.Routes.AuthenticateRequest = AuthenticateRequest;
            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            Console.WriteLine("Starting server on: " + _Settings.Prefix);

            _Server.Start();

            for (int i = 0; i < _Iterations; i++)
            {
                using (RestRequest req = new RestRequest(_Settings.Prefix))
                {
                    using (RestResponse resp = await req.SendAsync())
                    {
                        Console.WriteLine(resp.StatusCode + ": " + resp.DataAsString);
                    }
                }
            }
        }

        private static async Task AuthenticateRequest(HttpContextBase ctx)
        {
            if (_Counter % 2 == 0)
            {
                // do nothing, permit
            }
            else
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.Send("Denied");
            }

            _Counter++;
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Permitted");
            return;
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
