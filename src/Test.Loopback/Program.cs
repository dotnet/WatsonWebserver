using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test.Loopback
{
    static class Program
    {
        static string _Hostname = "127.0.0.1";
        static int _Port = 8080;

        static void Main()
        {
            Server server = new Server(_Hostname, _Port, false, DefaultRoute);
            server.Start();
            Console.WriteLine("WatsonWebserver listening on http://" + _Hostname + ":" + _Port);
            Console.WriteLine("ENTER to exit");
            Console.ReadLine();
        }
         
        static async Task DefaultRoute(HttpContext ctx)
        { 
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send();
            return; 
        } 
    }
}
