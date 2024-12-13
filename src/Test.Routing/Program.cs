namespace Test
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
        static Dictionary<string, string> _Metadata = new Dictionary<string, string>
        {
            { "foo", "bar" }
        };

        static void Main()
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

            _Server.Settings.AccessControl.Mode = AccessControlMode.DefaultPermit;
            _Server.Settings.AccessControl.DenyList.Add("1.1.1.1", "255.255.255.255");
            _Server.Routes.PreRouting = PreRoutingHandler;
            _Server.Routes.PostRouting = PostRoutingHandler;

            #region Pre-Authentication-Routes

            _Server.Routes.PreAuthentication.Content.Add("/preauth/content", false);

            _Server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/preauth/static", async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from pre-authentication static route /preauth/static");
                await ctx.Response.Send();
                return;
            });

            _Server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/preauth/parameter/{id}", async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from pre-authentication parameter route /preauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                await ctx.Response.Send();
                return;
            });

            _Server.Routes.PreAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/preauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from pre-authentication dynamic route /preauth/dynamic");
                await ctx.Response.Send();
                return;
            });

            #endregion

            #region Authentication

            _Server.Routes.AuthenticateRequest = async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| In authentication");
            };

            #endregion

            #region Post-Authentication-Routes

            _Server.Routes.PostAuthentication.Content.Add("/postauth/content", false);

            _Server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/postauth/static", async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from post-authentication static route /postauth/static");
                await ctx.Response.Send();
                return;
            });

            _Server.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, "/postauth/parameter/{id}", async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from post-authentication parameter route /postauth/parameter/" + ctx.Request.Url.Parameters["id"]);
                await ctx.Response.Send();
                return;
            });

            _Server.Routes.PostAuthentication.Dynamic.Add(HttpMethod.GET, new Regex("^/postauth/dynamic/\\d+$"), async (HttpContextBase ctx) =>
            {
                Console.WriteLine("| Responding from post-authentication dynamic route /postauth/dynamic");
                await ctx.Response.Send();
                return;
            });

            #endregion

            _Server.Events.ExceptionEncountered += ExceptionEncountered;
            _Server.Events.ServerStopped += ServerStopped;
            _Server.Events.Logger = Console.WriteLine;

            Console.WriteLine("Starting server on: " + _Settings.Prefix);
            _Server.Start();

            List<string> urls = new List<string>
            {
                _Settings.Prefix + "preauth/static",
                _Settings.Prefix + "preauth/content",
                _Settings.Prefix + "preauth/parameter/5",
                _Settings.Prefix + "preauth/dynamic/10",
                _Settings.Prefix + "postauth/static",
                _Settings.Prefix + "postauth/content",
                _Settings.Prefix + "postauth/parameter/5",
                _Settings.Prefix + "postauth/dynamic/10",
                _Settings.Prefix + "foo1",
                _Settings.Prefix + "foo2"
            };

            foreach (string url in urls)
            {
                Console.WriteLine("");
                Console.WriteLine("URL: " + url);

                using (RestRequest req = new RestRequest(url))
                {
                    using (RestResponse resp = req.Send())
                    {
                        Console.WriteLine("Received response: " + resp.StatusCode);
                        Task.Delay(1000).Wait();
                    }
                }
            }
        }

        static void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        static void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        static async Task PreRoutingHandler(HttpContextBase ctx)
        {

        }

        static async Task PostRoutingHandler(HttpContextBase ctx)
        {
            Console.WriteLine(ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + ": " + ctx.Response.StatusCode + " (" + ctx.Timestamp.TotalMs + "ms)");
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            try
            {
                Console.WriteLine("| Responding from the default route");
                ctx.Response.Headers.Add("Connection", "close");
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send("Default route");
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send();
                return;
            }
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
