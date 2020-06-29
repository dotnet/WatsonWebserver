using System.Threading.Tasks;
using WatsonWebserver;
using WatsonWebserver.Routes;

namespace Test.Routes
{
    class Program
    {
        static async Task Main()
        {
            // Create server and load all routes with the Route attribute in current assembly
            using var server = new Server("127.0.0.1", 8080, false, DefaultRoute).LoadRoutes();
            await Task.Delay(-1);
            
            // Load all methods with Route attribute from custom assembly
            // server.LoadRoutes(Assembly.GetExecutingAssembly());
        }

        static async Task DefaultRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the default route!");
        }
        
        [Route("hello")]
        public async Task HelloRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the hello route!");
        }
        
        [Route("post", HttpMethod.POST)]
        public async Task PostRoute(HttpContext context)
        {
            await context.Response.Send("Welcome to the post route!");
        }
    }
}