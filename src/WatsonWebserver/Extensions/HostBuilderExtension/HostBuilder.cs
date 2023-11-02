using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatsonWebserver.Core;

namespace WatsonWebserver.Extensions.HostBuilderExtension
{
    /// <summary>
    /// Host builder.
    /// </summary>
    public class HostBuilder : IHostBuilder<HostBuilder, Func<HttpContextBase, Task>>
    {
        #region Public-Members

        /// <summary>
        /// Webserver.
        /// </summary>
        public Webserver Server
        {
            get
            {
                if (_Server == null)
                {
                    _Server = new Webserver(_Settings, _DefaultRoute);
                    return _Server;
                }
                return _Server;
            }
        }

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Settings
        {
            get
            {
                return _Settings;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Settings));
                _Settings = Settings;
            }
        }

        #endregion

        #region Private-Members

        private Func<HttpContextBase, Task> _DefaultRoute = null;
        private Webserver _Server = null;
        private WebserverSettings _Settings = new WebserverSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="hostname">IP address on which to listen.</param>
        /// <param name="port">Port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="defaultRoute">Default route.</param>
        public HostBuilder(string hostname, int port, bool ssl, Func<HttpContextBase, Task> defaultRoute)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) port = 8000;
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));

            _Settings = new WebserverSettings();
            _Settings.Hostname = hostname;
            _Settings.Port = port;
            _Settings.Ssl.Enable = ssl;
            _Server = new Webserver(_Settings, defaultRoute);
            _DefaultRoute = defaultRoute;
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="defaultRoute">Default route.</param>
        public HostBuilder(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (defaultRoute == null) throw new ArgumentNullException(nameof(defaultRoute));

            _Settings = settings;
            _Server = new Webserver(_Settings, defaultRoute);
            _DefaultRoute = defaultRoute;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Map the pre-flight route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapPreflightRoute(Func<HttpContextBase, Task> handler)
        {
            Server.Routes.Preflight = handler;
            return this;
        }

        /// <summary>
        /// Map the pre-routing route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapPreRoutingRoute(Func<HttpContextBase, Task> handler)
        {
            Server.Routes.PreRouting = handler;
            return this;
        }

        /// <summary>
        /// Map an authentication route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapAuthenticationRoute(Func<HttpContextBase, Task> handler)
        {
            Server.Routes.AuthenticateRequest = handler;
            return this;
        }

        /// <summary>
        /// Map a content route.
        /// </summary>
        /// <param name="path">Route path.</param>
        /// <param name="isDirectory">Flag to indicate if the path is a directory.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapContentRoute(string path, bool isDirectory, bool requiresAuthentication = false)
        {
            if (!requiresAuthentication)
                Server.Routes.PreAuthentication.Content.Add(path, isDirectory);
            else
                Server.Routes.PostAuthentication.Content.Add(path, isDirectory);
            return this;
        }

        /// <summary>
        /// Apply a content handler.
        /// </summary>
        /// <param name="handler">Content route handler.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapContentHandler(ContentRouteHandler handler, bool requiresAuthentication = false)
        {
            if (!requiresAuthentication)
                Server.Routes.PreAuthentication.ContentHandler = handler;
            else
                Server.Routes.PostAuthentication.ContentHandler = handler;
            return this;
        }

        /// <summary>
        /// Apply a static route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Route path.</param>
        /// <param name="action">Action.</param>
        /// <param name="requiresAuthentication">Boolean to indicate if the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapStaticRoute(HttpMethod method, string path, Func<HttpContextBase, Task> action, bool requiresAuthentication = false)
        {
            if (!requiresAuthentication)
                Server.Routes.PreAuthentication.Static.Add(method, path, action);
            else
                Server.Routes.PostAuthentication.Static.Add(method, path, action);
            return this;
        }

        /// <summary>
        /// Apply a parameter route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Route path.</param>
        /// <param name="action">Action.</param>
        /// <param name="requiresAuthentication">Boolean to indicate if the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapParameteRoute(HttpMethod method, string path, Func<HttpContextBase, Task> action, bool requiresAuthentication = false)
        {
            if (!requiresAuthentication)
                Server.Routes.PreAuthentication.Parameter.Add(method, path, action);
            else
                Server.Routes.PostAuthentication.Parameter.Add(method, path, action);
            return this;
        }

        /// <summary>
        /// Apply a dynamic route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="regex">Regular expression.</param>
        /// <param name="action">Action.</param>
        /// <param name="requiresAuthentication">Boolean to indicate if the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapDynamicRoute(HttpMethod method, Regex regex, Func<HttpContextBase, Task> action, bool requiresAuthentication = false)
        {
            if (!requiresAuthentication)
                Server.Routes.PreAuthentication.Dynamic.Add(method, regex, action);
            else
                Server.Routes.PostAuthentication.Dynamic.Add(method, regex, action);
            return this;
        }

        /// <summary>
        /// Map the default route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapDefaultRoute(Func<HttpContextBase, Task> handler)
        {
            Server.Routes.Default = handler;
            return this;
        }

        /// <summary>
        /// Map the post-routing route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        public HostBuilder MapPostRoutingRoute(Func<HttpContextBase, Task> handler)
        {
            Server.Routes.PostRouting = handler;
            return this;
        }

        /// <summary>
        /// Build the server.
        /// </summary>
        /// <returns>Server.</returns>
        public Webserver Build()
        {
            return Server;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
