using System;
using WatsonWebserver;

namespace TestStaticRoutes
{
    static class Program
    {
        static void Main()
        {
            Server s = new Server("127.0.0.1", 9000, false, DefaultRoute, true);
            s.AddStaticRoute("get", "/hello/", GetHelloRoute);
            s.AddStaticRoute("get", "/world/", GetWorldRoute);
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static HttpResponse GetHelloRoute(HttpRequest req)
        { 
            return ResponseBuilder(req, "Watson says hello from the GET /hello static route!");
        }

        static HttpResponse GetWorldRoute(HttpRequest req)
        { 
            return ResponseBuilder(req, "Watson says hello from the GET /world static route!");
        }

        static HttpResponse DefaultRoute(HttpRequest req)
        { 
            return ResponseBuilder(req, "Watson says hello from the default route!");
        }

        static HttpResponse ResponseBuilder(HttpRequest req, string text)
        {            
            // for an encapsulated JSON response:
            // {"success":true,"md5":"BE3DB22E4FDF3021162C013320CEED09","data":"Watson says hello!"}
            // resp = new HttpResponse(req, true, 200, null, "text/plain", "Watson says hello!", false);

            // for a response containing only the string data...
            // Watson says hello!
            return new HttpResponse(req, true, 200, null, "text/plain", text, true);
        }
    }
}
