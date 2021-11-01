using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatsonWebserver;

namespace Test.Routing
{
    public class IntermediateServer
    {
        private Server _Server = null;

        public IntermediateServer(string hostname, int port, bool ssl = false)
        {
            _Server = new Server(hostname, port, ssl, DefaultRoute);
            _Server.Start();
        }

        public async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send();
        }
    }
}
