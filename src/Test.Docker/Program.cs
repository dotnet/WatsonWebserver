namespace Test.Docker
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Lite;

    class Program
    {
        static bool _UsingLite = false;
        static string _Hostname = "0.0.0.0";
        static int _Port = 8080;
        static WebserverSettings _Settings = null;
        static WebserverBase _Server = null;

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

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            bool signal = false;
            do
            {
                signal = waitHandle.WaitOne(1000);
            } 
            while (!signal);
        }
         
        static async Task DefaultRoute(HttpContextBase ctx)
        {
            if (ctx.Request.Method == HttpMethod.GET)
            {
                if (ctx.Request.Url.Elements == null || ctx.Request.Url.Elements.Length == 0)
                {
                    Console.WriteLine("Sending default HTML page");

                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.Send(Html);
                    return;
                }
                else if (ctx.Request.Url.Elements.Length == 1)
                { 
                    if (ctx.Request.Url.Elements[0].Equals("watson.ico")
                        || ctx.Request.Url.Elements[0].Equals("favicon.ico"))
                    {
                        Console.WriteLine("Sending icon " + ctx.Request.Url.Elements[0]);

                        ctx.Response.ContentType = "image/png";
                        await ctx.Response.Send(File.ReadAllBytes("./watson.ico"));
                        return;
                    }
                }
            }

            Console.WriteLine("Sending 404 Not Found");

            ctx.Response.StatusCode = 404;
            await ctx.Response.Send();
            return;
        }

        static string Html = 
            "<html>" + Environment.NewLine +
            "  <head>" + Environment.NewLine +
            "    <title>Hello from Watson Webserver</title>" + Environment.NewLine +
            "  </head>" + Environment.NewLine +
            "  <body>" + Environment.NewLine +
            "    <div>" + Environment.NewLine +
            "      <img src='./watson.ico' />" + Environment.NewLine +
            "      <h2>Hello!</h2>" + Environment.NewLine +
            "      <p>Hello from Watson Webserver!  Your request has been received." + Environment.NewLine +
            "    </div>" + Environment.NewLine +
            "  </body>" + Environment.NewLine +
            "</html>";  
    }
}
