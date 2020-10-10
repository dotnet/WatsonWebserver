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
        static string _Hostname = "*";
        static int _Port = 8000;
        
        static void Main(string[] args)
        {
            _Server= new Server(_Hostname, _Port, false, DefaultRoute);
            _Server.Start();

            Console.WriteLine("Watson Webserver started on http://*:8000");

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
                if (ctx.Request.RawUrlEntries == null || ctx.Request.RawUrlEntries.Count == 0)
                {
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.Send(Html(ctx));
                    return;
                }
                else if (ctx.Request.RawUrlEntries.Count == 1)
                {
                    if (ctx.Request.RawUrlEntries[0].Equals("watson.ico")
                        || ctx.Request.RawUrlEntries[0].Equals("favicon.ico"))
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

        static string Html(HttpContext ctx)
        {
            string html =
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
            return html;
        }
    }
}
