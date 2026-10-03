namespace WatsonWebserver.Core
{
    using System;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Webserver settings.
    /// The settings own the <see cref="AccessControl"/> and <see cref="Telemetry"/> objects assigned to them, which
    /// hold IP matchers: an instance that is replaced, or held when the settings are disposed, is disposed.
    /// A server disposes its settings when it is disposed.
    /// </summary>
    public class WebserverSettings : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Hostname on which to listen.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// TCP port on which to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        /// <summary>
        /// Listener prefix, of the form 'http[s]://[hostname]:[port]/.
        /// </summary>
        public string Prefix
        {
            get
            {
                string ret = "";
                if (Ssl != null && Ssl.Enable) ret += "https://";
                else ret += "http://";
                ret += Hostname + ":" + Port + "/";
                return ret;
            }
        }

        /// <summary>
        /// Protocol enablement and limits.
        /// </summary>
        public ProtocolSettings Protocols
        {
            get
            {
                return _Protocols;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Protocols));
                _Protocols = value;
            }
        }

        /// <summary>
        /// Alt-Svc advertising settings.
        /// </summary>
        public AltSvcSettings AltSvc
        {
            get
            {
                return _AltSvc;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(AltSvc));
                _AltSvc = value;
            }
        }

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public IOSettings IO
        {
            get
            {
                return _IO;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(IO));
                _IO = value;
            }
        }

        /// <summary>
        /// SSL settings.
        /// </summary>
        public SslSettings Ssl
        {
            get
            {
                return _Ssl;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Ssl));
                _Ssl = value;
            }
        }

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public HeaderSettings Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Headers));
                _Headers = value;
            }
        }

        /// <summary>
        /// Access control manager, i.e. default mode of operation, permit list, and deny list.
        /// Assigning a different manager disposes the previous one.
        /// </summary>
        public AccessControlManager AccessControl
        {
            get
            {
                return _AccessControl;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(AccessControl));
                AccessControlManager previous = _AccessControl;
                _AccessControl = value;
                if (previous != null && !ReferenceEquals(previous, value)) previous.Dispose();
            }
        }

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public DebugSettings Debug
        {
            get
            {
                return _Debug;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Debug));
                _Debug = value;
            }
        }

        /// <summary>
        /// Request timeout settings for API route handlers.
        /// Set Timeout.DefaultTimeout to a positive TimeSpan to enable request timeouts.
        /// </summary>
        public TimeoutSettings Timeout
        {
            get
            {
                return _Timeout;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Timeout));
                _Timeout = value;
            }
        }

        /// <summary>
        /// WebSocket settings.
        /// </summary>
        public WebSocketSettings WebSockets
        {
            get
            {
                return _WebSockets;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(WebSockets));
                _WebSockets = value;
            }
        }

        /// <summary>
        /// Telemetry and instrumentation settings.
        /// Controls metric and trace emission, forwarded-header resolution for the client address, and
        /// the optional in-process Prometheus scrape endpoint.
        /// Assigning a different instance disposes the previous one.
        /// </summary>
        public TelemetrySettings Telemetry
        {
            get
            {
                return _Telemetry;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Telemetry));
                TelemetrySettings previous = _Telemetry;
                _Telemetry = value;
                if (previous != null && !ReferenceEquals(previous, value)) previous.Dispose();
            }
        }

        /// <summary>
        /// When true, the machine's hostname will be used instead of the value specified in Hostname.
        /// </summary>
        public bool UseMachineHostname
        {
            get
            {
                if (Hostname == "*" || Hostname == "+") return true;
                return _UseMachineHostname;
            }
            set
            {
                _UseMachineHostname = (Hostname == "*" || Hostname == "+") || value;
            }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8000;
        private ProtocolSettings _Protocols = new ProtocolSettings();
        private AltSvcSettings _AltSvc = new AltSvcSettings();
        private IOSettings _IO = new IOSettings();
        private SslSettings _Ssl = new SslSettings();
        private AccessControlManager _AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
        private DebugSettings _Debug = new DebugSettings();
        private HeaderSettings _Headers = new HeaderSettings();
        private TimeoutSettings _Timeout = new TimeoutSettings();
        private WebSocketSettings _WebSockets = new WebSocketSettings();
        private TelemetrySettings _Telemetry = new TelemetrySettings();
        private bool _UseMachineHostname = false;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings()
        {

        }

        /// <summary>
        /// Webserver settings.
        /// </summary>
        /// <param name="hostname">The hostname on which to listen.</param>
        /// <param name="port">The port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public WebserverSettings(string hostname, int port, bool ssl = false)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            if (hostname.Equals("::")) hostname = "[::]";

            _Ssl.Enable = ssl;
            _Hostname = hostname;
            _Port = port;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose the access control manager and telemetry settings, releasing the match caches of their IP matchers.
        /// The matchers continue to evaluate addresses after disposal, without caching.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;

            if (disposing)
            {
                _AccessControl?.Dispose();
                _Telemetry?.Dispose();
            }
        }

        #endregion
    }
}
