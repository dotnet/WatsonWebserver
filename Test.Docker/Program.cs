using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test.Docker
{
    class Program
    {
        static Server _Server;
        static string _Hostname = "localhost";
        static int _Port = 8080;
        
        static void Main(string[] args)
        {
            _Server= new Server(_Hostname, _Port, false, DefaultRoute);
            _Server.Start();

            Console.WriteLine("Watson Webserver started on http://" + _Hostname + ":" + _Port);

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            bool signal = false;
            do
            {
                signal = waitHandle.WaitOne(1000);
            } 
            while (!signal);
        }
         
        static async Task DefaultRoute(HttpContext ctx)
        {
            if (ctx.Request.Method == HttpMethod.GET)
            {
                if (ctx.Request.Url.Elements == null || ctx.Request.Url.Elements.Length == 0)
                {
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.Send(Html);
                    return;
                }
                else if (ctx.Request.Url.Elements.Length == 1)
                { 
                    if (ctx.Request.Url.Elements[0].Equals("watson.ico")
                        || ctx.Request.Url.Elements[0].Equals("favicon.ico"))
                    {
                        ctx.Response.ContentType = "image/png";
                        await ctx.Response.Send(File.ReadAllBytes("./watson.ico"));
                        return;
                    }
                }
            }

            ctx.Response.ContentType = "text/plain";
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
