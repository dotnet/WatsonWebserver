namespace WatsonWebserver.Core
{
    using System;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
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
                _AccessControl = value;
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
        private bool _UseMachineHostname = false;

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

        #endregion

        #region Private-Methods

        #endregion
    }
}
