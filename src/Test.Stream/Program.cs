namespace Test
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    class Program
    {
        static bool _UsingLite = false;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        static string _Directory = "./uploads";
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
            _Server.Events.Logger = Console.WriteLine;
            _Server.Start();

            if (!Directory.Exists(_Directory)) Directory.CreateDirectory(_Directory);

            Console.WriteLine("Use GET /watson.jpg");
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task DefaultRoute(HttpContextBase ctx)
        {
            Console.WriteLine(ctx.Request.Method + " " + ctx.Request.Url.RawWithoutQuery);

            FileStream fs = null;
             
            switch (ctx.Request.Method)
            {
                case HttpMethod.GET:
                    long len = new System.IO.FileInfo("watson.jpg").Length;
                    using (fs = new FileStream("watson.jpg", FileMode.Open, FileAccess.Read))
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.Send(len, fs);
                        return;
                    }

                case HttpMethod.POST:
                    if (ctx.Request.Url.Elements == null || ctx.Request.Url.Elements.Length != 1)
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send("Bad request");
                        return;
                    }
                    else if (ctx.Request.Data == null || !ctx.Request.Data.CanRead)
                    {
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send("Bad request");
                        return;
                    }
                    else
                    {
                        using (fs = new FileStream(_Directory + "/" + ctx.Request.Url.Elements[0], FileMode.OpenOrCreate))
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[2048];
                            while ((bytesRead = ctx.Request.Data.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fs.Write(buffer, 0, bytesRead);
                            }
                        }
                        ctx.Response.StatusCode = 201;
                        await ctx.Response.Send();
                        return;
                    }

                default:
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.Send("Bad request");
                    return;
            } 
        }
    }
}
