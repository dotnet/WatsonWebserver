using System;

namespace WatsonWebserver.Extensions.HostBuilderExtension
{
    public class HostBuilder : IHostBuilder<HostBuilder, Func<HttpContext, Task>>
    {
        string _ip = "";
        int _port = 0;
        bool _ssl = false;
        Func<HttpContext, Task> _indexRoute;

        Server server;



        public HostBuilder(string ip, int port, bool ssl, Func<HttpContext, Task> indexRoute)
        {
            _ip = ip;
            _port = port;
            _ssl = ssl;
            this._indexRoute = indexRoute;
        }


        public Server HttpServer
        {
            get
            {
                if (server is null)
                {
                    server = new Server(_ip, _port, _ssl, _indexRoute);
                    return server;
                }
                return server;
            }
        }

        public HostBuilder MapDynamicRoute(HttpMethod methid, Func<HttpContext, Task> action, Regex rx)
        {
            HttpServer.Routes.Dynamic.Add(methid, rx, action); return this;
        }

        public HostBuilder MapParameteRoute(HttpMethod methid, Func<HttpContext, Task> action, string routePath = "/home")
        {
            HttpServer.Routes.Parameter.Add(methid, routePath, action); return this;
        }

        public HostBuilder MapStaticRoute(HttpMethod methid, Func<HttpContext, Task> action, string routePath = "/home")
        {
            HttpServer.Routes.Static.Add(methid, routePath, action); return this;
        }

        public Server Build()
        {
            return HttpServer;
        }
    }
}
