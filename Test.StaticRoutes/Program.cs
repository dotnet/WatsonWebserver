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
            s.Start();

            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task GetHelloRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("Watson says hello from the GET /hello static route!");
            return;
        }

        static async Task GetWorldRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("Watson says hello from the GET /world static route!");
            return;
        }

        static async Task PostDataRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(ctx.Request.ContentLength, ctx.Request.Data);
            return;
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("Watson says hello from the default route!");
            return;
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
