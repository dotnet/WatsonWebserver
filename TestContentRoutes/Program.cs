using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver;

namespace TestContentRoutes
{
    class Program
    {
        static void Main()
        {
            Server s = new Server("127.0.0.1", 9000, false, DefaultRoute, true);
            s.AddContentRoute("/", true);
            s.AddContentRoute("/html/", true);
            s.AddContentRoute("/img/watson.jpg", false);
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }
          
        static HttpResponse DefaultRoute(HttpRequest req)
        {
            return new HttpResponse(req, false, 404, null, "text/plain", "Not found", true);
        } 
    }
}
