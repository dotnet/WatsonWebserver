using System;
using System.Collections.Generic;
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
            s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/\\d+$"), GetFooWithId);
            s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/(.*?)/(.*?)/?$"), GetFooMultipleChildren);
            s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/(.*?)/?$"), GetFooOneChild);
            s.DynamicRoutes.Add(HttpMethod.GET, new Regex("^/foo/?$"), GetFoo);
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task GetFooWithId(HttpContext ctx)
        {
            await SendResponse(ctx, "Watson says hello from the GET /foo with ID dynamic route!");
            return;
        }

        static async Task GetFooMultipleChildren(HttpContext ctx)
        {
            await SendResponse(ctx, "Watson says hello from the GET /foo with multiple children dynamic route!");
            return;
        }

        static async Task GetFooOneChild(HttpContext ctx)
        {
            await SendResponse(ctx, "Watson says hello from the GET /foo with one child dynamic route!");
            return;
        }

        static async Task GetFoo(HttpContext ctx)
        {
            await SendResponse(ctx, "Watson says hello from the GET /foo dynamic route!");
            return;
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            await SendResponse(ctx, "Watson says hello from the default route!");
            return;
        }

        static async Task SendResponse(HttpContext ctx, string text)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send(text);
            return;
        }
    }
}
