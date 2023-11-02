using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace WatsonWebserver.Core
{
    /// <summary>
    /// Webserver base.
    /// </summary>
    public abstract class WebserverBase
    {
        #region Public-Members

        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public abstract bool IsListening { get; }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public abstract int RequestCount { get; }

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
                if (value == null) _Settings = new WebserverSettings();
                else _Settings = value;
            }
        }

        /// <summary>
        /// Webserver routes.
        /// </summary>
        public WebserverRoutes Routes
        {
            get
            {
                return _Routes;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Routes));
                _Routes = value;
            }
        }

        /// <summary>
        /// Webserver statistics.
        /// </summary>
        public WebserverStatistics Statistics
        {
            get
            {
                return _Statistics;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Statistics));
                _Statistics = value;
            }
        }

        /// <summary>
        /// Set specific actions/callbacks to use when events are raised.
        /// </summary>
        public WebserverEvents Events
        {
            get
            {
                return _Events;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Events));
                _Events = value;
            }
        }

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WebserverPages DefaultPages
        {
            get
            {
                return _DefaultPages;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(DefaultPages));
                _DefaultPages = value;
            }
        }

        /// <summary>
        /// JSON serialization helper.
        /// </summary>
        [JsonIgnore]
        public ISerializationHelper Serializer
        {
            get
            {
                return _Serializer;
            }
            set
            {
                _Serializer = value ?? throw new ArgumentNullException(nameof(Serializer));
            }
        }

        #endregion

        #region Private-Members

        private WebserverEvents _Events = new WebserverEvents();
        private WebserverPages _DefaultPages = new WebserverPages();
        private WebserverSettings _Settings = new WebserverSettings();
        private WebserverStatistics _Statistics = new WebserverStatistics();
        private WebserverRoutes _Routes = new WebserverRoutes();
        private ISerializationHelper _Serializer = new DefaultSerializationHelper();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Creates a new instance of the webserver.
        /// If you do not provide a settings object, default settings will be used, which will cause the webserver to listen on http://127.0.0.1:8000, and send events to the console.
        /// </summary>
        /// <param name="settings">Webserver settings.</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public WebserverBase(WebserverSettings settings, Func<HttpContextBase, Task> defaultRoute)
        {
            if (settings == null) settings = new WebserverSettings();

            _Settings = settings;
            _Routes = new WebserverRoutes(_Settings, defaultRoute);
        }

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRoute">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public WebserverBase(string hostname, int port, bool ssl, Func<HttpContextBase, Task> defaultRoute)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));

            _Settings = new WebserverSettings(hostname, port, ssl);
            _Routes = new WebserverRoutes(_Settings, defaultRoute);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        public abstract void Start(CancellationToken token = default);

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        /// <returns>Task.</returns>
        public abstract Task StartAsync(CancellationToken token = default);

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public abstract void Stop();

        #endregion

        #region Private-Members

        #endregion
    }
}
