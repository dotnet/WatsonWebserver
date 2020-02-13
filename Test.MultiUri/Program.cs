using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test.MultiUri
{
    class Program
    {
        static Server _Server = null;

        static void Main(string[] args)
        {
            List<string> uris = new List<string>
            {
                "http://localhost:8000/",
                "http://localhost:8001/",
                "http://localhost:8002/"
            };

            _Server = new Server(uris, DefaultRoute);
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            Console.WriteLine(
                ctx.Request.SourceIp + 
                ":" + ctx.Request.SourcePort + 
                " -> " + ctx.Request.DestIp + 
                ":" + ctx.Request.DestPort + 
                " " + ctx.Request.Method.ToString() + 
                " " + ctx.Request.RawUrlWithQuery);

            await ctx.Response.Send();
            return;
        }
    }
}
