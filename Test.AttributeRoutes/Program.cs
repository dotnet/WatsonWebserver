using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver; 

namespace Test.AttributeRoutes
{
    class Program
    {
        static string _Hostname = "127.0.0.1";
        static int _Port = 8080;
        static bool _Ssl = false;

        static void Main(string[] args)
        { 
            using (var server = new Server(_Hostname, _Port, _Ssl, DefaultRoute).LoadRoutes())
            { 
                server.Start();

                Console.Write("Listening on ");
                if (_Ssl) Console.Write("https://");
                else Console.Write("http://");
                Console.WriteLine(_Hostname + ":" + _Port);
                Console.WriteLine("Press ENTER to exit");
                Console.ReadLine();
            } 
        }

        static async Task DefaultRoute(HttpContext ctx)
        {
            await ctx.Response.Send("Default route");
        }
        
        [StaticRoute(HttpMethod.GET, "hello")]
        public async Task HelloRoute(HttpContext ctx)
        {
            await ctx.Response.Send("Static route GET /hello");
        }
        
        [StaticRoute(HttpMethod.POST, "submit")]
        public async Task PostRoute(HttpContext ctx)
        {
            await ctx.Response.Send("Static route POST /submit");
        }

        [DynamicRoute(HttpMethod.PUT, "^/foo/")]
        public async Task PutRoute(HttpContext ctx)
        {
            await ctx.Response.Send("Dynamic route PUT /foo/");
        }

        [DynamicRoute(HttpMethod.PUT, "^/foo/\\d+$")]
        public async Task PutRouteWithId(HttpContext ctx)
        {
            await ctx.Response.Send("Dynamic route PUT /foo/[id]");
        }
    }
}