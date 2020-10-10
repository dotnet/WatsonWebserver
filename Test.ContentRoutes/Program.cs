using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    class Program
    {
        static void Main()
        {
            Server s = new Server("127.0.0.1", 9000, false, DefaultRoute);
            // s.ContentRoutes.BaseDirectory = "/";
            s.ContentRoutes.Add("/", true);
            s.ContentRoutes.Add("/html/", true);
            s.ContentRoutes.Add("/large/", true);
            s.ContentRoutes.Add("/img/watson.jpg", false);

            s.Events.ExceptionEncountered = ExceptionEncountered;
            s.Events.RequestorDisconnected = RequestorDisconnected;

            s.Start();

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 404;
            await ctx.Response.Send();
            return; 
        }

        static void ExceptionEncountered(string ip, int port, Exception e)
        {
            Console.WriteLine("ExceptionEncountered [" + ip + ":" + port + "]: " + Environment.NewLine + e.ToString());
        }

        static void RequestorDisconnected(string ip, int port, string method, string url)
        {
            Console.WriteLine("RequestorDisconnected [" + ip + ":" + port + "]: " + method + " " + url);
        }
    }
}
