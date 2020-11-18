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
        static void Main()
        {
            Server server = new Server("localhost", 8080, false, DefaultRoute);
            server.Start();
            Console.WriteLine("WatsonWebserver listening on http://localhost:8080");
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
