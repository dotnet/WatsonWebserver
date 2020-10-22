using System;
using System.Threading.Tasks;
using WatsonWebserver;
using RestWrapper;

namespace Test.MaxConnections
{
    class Program
    {
        static string _Hostname = "127.0.0.1";
        static int _Port = 8000;
        static int _MaxConcurrentRequests = 5;
        static Server _Server = null;

        static void Main(string[] args)
        {
            _Server = new Server(_Hostname, _Port, false, DefaultRoute);
            _Server.MaxRequests = _MaxConcurrentRequests;
            _Server.Start();

            for (int i = 0; i < 25; i++)
            {
                Task.Run(() => ClientTask());
            }

            while (true)
            {
                Task.Delay(100).Wait();
                Console.Write("\r" + _Server.RequestCount + "                                 \r");
            }

            /*
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
            */
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            Console.WriteLine(ctx.Request.SourceIp + ":" + ctx.Request.SourcePort + " started");
            Task.Delay(2000).Wait();
            Console.WriteLine(ctx.Request.SourceIp + ":" + ctx.Request.SourcePort + " ended");
            await ctx.Response.Send();
            return;
        }

        static void ClientTask()
        {
            Console.WriteLine("Sending request");
            RestRequest req = new RestRequest("http://" + _Hostname + ":" + _Port + "/");
            RestResponse resp = req.Send();
            Console.WriteLine("Response received: " + resp.StatusDescription);
        }
    }
}
