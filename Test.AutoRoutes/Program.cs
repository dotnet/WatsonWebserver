using System.Reflection;
using System.Threading.Tasks;
using WatsonWebserver; 

namespace Test.AutoRoutes
{
    class Program
    {
        static async Task Main()
        {
            // Create server and load all routes with the Route attribute in current assembly
            using (var server = new Server("127.0.0.1", 8080, false, DefaultRoute))
            {
                //Auto register all methods from all public classes in current assembly or specific assemblies.
                server.Register(Assembly.GetExecutingAssembly() /*you can add more assembly here, Assembly1, Assembly2*/);
                server.Start();

                await Task.Delay(-1);
            }
            
        }

        static async Task DefaultRoute(HttpContext context)
        {
            await context.Response.Send("No Route. The Urls incorrect!");
        }        
    }

    [Route("/api/")]
    public class MyClassOne
    {
        //http://127.0.0.1:8080/api/hello
        [Route("hello")]
        public async Task HelloRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassOne hello route!");
        }

        //http://127.0.0.1:8080/api/shout
        public async Task Shout(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassOne shout route!");
        }

        //http://127.0.0.1:8080/api/hey/[number]
        [DRoute("^hey/\\d+$", HttpMethod.GET)]
        public async Task HeyRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassOne hey route!");
        }

        [Route("post", HttpMethod.POST)]
        public async Task PostRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassOne post route!");
        }

        //The private method will not auto routed.
        private async Task NonPublic(HttpContext context)
        {
            await context.Response.Send("You will see url incorrect.");
        }
    }

    [Route("/api2/")]
    public class MyClassTwo
    {
        //http://127.0.0.1:8080/api2/hello
        [Route("hello")]
        public async Task HelloRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassTwo hello route!");
        }

        //http://127.0.0.1:8080/api2/shout
        public async Task Shout(HttpContext context)
        {
            await context.Response.Send("Welcome to the MyClassTwo shout route!");
        }
    }
}