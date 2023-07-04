using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WatsonWebserver.Extensions.HostBuilderExtension
{
    /// <summary>
    /// Host builder.
    /// </summary>
    public class HostBuilder : IHostBuilder<HostBuilder, Func<HttpContext, Task>>
    {
        #region Public-Members

        /// <summary>
        /// Webserver.
        /// </summary>
        public Server Server
        {
            get
            {
                if (_Server == null)
                {
                    _Server = new Server(_Ip, _Port, _Ssl, _DefaultRoute);
                    return _Server;
                }
                return _Server;
            }
        }

        #endregion

        #region Private-Members

        private string _Ip = "";
        private int _Port = 0;
        private bool _Ssl = false;
        private Func<HttpContext, Task> _DefaultRoute = null;
        private Server _Server = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ip">IP address on which to listen.</param>
        /// <param name="port">Port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="defaultRoute">Default route.</param>
        public HostBuilder(string ip, int port, bool ssl, Func<HttpContext, Task> defaultRoute)
        {
            _Ip = ip;
            _Port = port;
            _Ssl = ssl;
            _DefaultRoute = defaultRoute;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply a dynamic route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="regex">Regular expression.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapDynamicRoute(HttpMethod method, Func<HttpContext, Task> action, Regex regex)
        {
            Server.Routes.Dynamic.Add(method, regex, action); return this;
        }

        /// <summary>
        /// Apply a parameter route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="routePath">Route path.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapParameteRoute(HttpMethod method, Func<HttpContext, Task> action, string routePath = "/home")
        {
            Server.Routes.Parameter.Add(method, routePath, action); return this;
        }

        /// <summary>
        /// Apply a static route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="routePath">Route path.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapStaticRoute(HttpMethod method, Func<HttpContext, Task> action, string routePath = "/home")
        {
            Server.Routes.Static.Add(method, routePath, action); return this;
        }

        /// <summary>
        /// Build the server.
        /// </summary>
        /// <returns>Server.</returns>
        public Server Build()
        {
            return Server;
        }

        #endregion
    }
}
