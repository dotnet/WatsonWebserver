using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace WatsonWebserverTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Server s = new Server("127.0.0.1", 9000, false, RequestReceived);
            s.DebugRestRequests = false;    // we will show them in our callback below
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static HttpResponse RequestReceived(HttpRequest req)
        {
            Console.WriteLine(req.ToString());
            HttpResponse resp = new HttpResponse(req, true, 200, null, "text/plain", "Watson says hello!", true);
            return resp;
        }
    }
}
