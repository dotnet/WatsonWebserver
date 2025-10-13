namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Route manager.
    /// </summary>
    public class WebserverRoutes
    {
        /// <summary>
        /// Method to invoke when an OPTIONS request is received.
        /// </summary>
        public Func<HttpContextBase, Task> Preflight { get; set; } = null;

        /// <summary>
        /// Method to invoke prior to routing.
        /// </summary>
        public Func<HttpContextBase, Task> PreRouting { get; set; } = null;

        /// <summary>
        /// Pre-authentication routes.
        /// </summary>
        public RoutingGroup PreAuthentication
        {
            get
            {
                return _PreAuthentication;
            }
            set
            {
                if (value == null) _PreAuthentication = new RoutingGroup();
                else _PreAuthentication = value;
            }
        }

        /// <summary>
        /// Method to invoke to authenticate a request.
        /// Attach any session-related metadata to the HttpContextBase.Metadata property.
        /// </summary>
        public Func<HttpContextBase, Task> AuthenticateRequest { get; set; } = null;

        /// <summary>
        /// Post-authentication routes.
        /// </summary>
        public RoutingGroup PostAuthentication
        {
            get
            {
                return _PostAuthentication;
            }
            set
            {
                if (value == null) PostAuthentication = new RoutingGroup();
                else _PostAuthentication = value;
            }
        }

        /// <summary>
        /// Default route, when no other routes are available.
        /// </summary>
        public Func<HttpContextBase, Task> Default
        {
            get
            {
                return _Default;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Default));
                _Default = value;
            }
        }

        /// <summary>
        /// Catch-all exception route; used as an exception route of last resort.
        /// </summary>
        public Func<HttpContextBase, Exception, Task> Exception { get; set; } = null;

        /// <summary>
        /// Method invoked after routing, primarily to emit logging and telemetry.
        /// </summary>
        public Func<HttpContextBase, Task> PostRouting { get; set; } = null;

        private WebserverSettings _Settings = null;
        private RoutingGroup _PreAuthentication = new RoutingGroup();
        private RoutingGroup _PostAuthentication = new RoutingGroup();
        private Func<HttpContextBase, Task> _Default = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WebserverRoutes()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Settings.</param>
        /// <param name="defaultRoute">Default route.</param>
        public WebserverRoutes(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Default = defaultRoute ?? throw new ArgumentNullException(nameof(defaultRoute));
        }
    }
}
