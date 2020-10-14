using System;
using System.Threading;
using System.Threading.Tasks;
using RestWrapper;
using WatsonWebserver;

namespace Test.Dispose
{
    class Program
    {
        static Server _Server = null;

        static void Main(string[] args)
        {
            _Server = new Server("127.0.0.1", 9000, false, DefaultRoute);
            _Server.Start();

            // test1
            Console.WriteLine("Test 1 with server started: " + ClientTask());

            // test2
            _Server.Dispose();
            Console.WriteLine("Test 2 with server disposed: " + ClientTask());

            // test3
            _Server = new Server("127.0.0.1", 9000, false, DefaultRoute);
            _Server.Start();
            Console.WriteLine("Test 3 with server restarted: " + ClientTask());
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            await ctx.Response.Send();
        }

        static bool ClientTask()
        {
            RestRequest req = new RestRequest("http://127.0.0.1:9000", RestWrapper.HttpMethod.GET, null, null);
            RestResponse resp = req.Send();
            if (resp.StatusCode == 200) return true;
            return false;
        }
    }
}
