using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test
{
    static class Program
    {
        static void Main()
        {
            Server s = new Server("127.0.0.1", 9000, false, DefaultRoute);
            s.StaticRoutes.Add(HttpMethod.GET, "/hello/", GetHelloRoute);
            s.StaticRoutes.Add(HttpMethod.GET, "/world/", GetWorldRoute);
            s.StaticRoutes.Add(HttpMethod.POST, "/data/", PostDataRoute);
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static HttpResponse GetHelloRoute(HttpRequest req)
        {
            return new HttpResponse(req, 200, null, "text/plain", Encoding.UTF8.GetBytes("Watson says hello from the GET /hello static route!"));
        }

        static HttpResponse GetWorldRoute(HttpRequest req)
        {
            return new HttpResponse(req, 200, null, "text/plain", Encoding.UTF8.GetBytes("Watson says hello from the GET /world static route!"));
        }

        static HttpResponse PostDataRoute(HttpRequest req)
        { 
            return new HttpResponse(req, 200, null, req.ContentType, req.Data);
        }

        static HttpResponse DefaultRoute(HttpRequest req)
        { 
            return new HttpResponse(req, 200, null, "text/plain", Encoding.UTF8.GetBytes("Hello from the default route!")); 
        }
         
        static string InputString(string question, string defaultAnswer, bool allowNull)
        {
            while (true)
            {
                Console.Write(question);

                if (!String.IsNullOrEmpty(defaultAnswer))
                {
                    Console.Write(" [" + defaultAnswer + "]");
                }

                Console.Write(" ");

                string userInput = Console.ReadLine();

                if (String.IsNullOrEmpty(userInput))
                {
                    if (!String.IsNullOrEmpty(defaultAnswer)) return defaultAnswer;
                    if (allowNull) return null;
                    else continue;
                }

                return userInput;
            }
        }
    }
}
