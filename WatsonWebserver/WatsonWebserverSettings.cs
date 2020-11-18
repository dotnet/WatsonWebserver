using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Watson webserver settings.
    /// </summary>
    public class WatsonWebserverSettings
    {
        #region Public-Members

        /// <summary>
        /// The hostname or IP addresses on which to listen.
        /// </summary>
        public List<string> Hostnames
        {
            get
            {
                return _Hostnames;
            }
            set
            {
                if (value == null) _Hostnames = new List<string> { "localhost" };
                else _Hostnames = value;
            }
        }

        /// <summary>
        /// The URIs on which to listen.  Setting this value will clear any previously-configured hostnames, set SSL to enabled or disabled, and set the port.
        /// </summary>
        public List<Uri> Uris
        {
            get
            {
                List<Uri> ret = new List<Uri>();

                if (_Ssl.Enable)
                {
                    foreach (string host in _Hostnames) ret.Add(new Uri("https://" + host + ":" + _Port));
                }
                else
                {
                    foreach (string host in _Hostnames) ret.Add(new Uri("http://" + host + ":" + _Port));
                }

                return ret;
            }
            set
            {
                if (value == null || value.Count < 1) value = new List<Uri> { new Uri("http://localhost:8080/") };

                int comparePort = -1;
                string compareProtocol = "";

                foreach (Uri uri in value)
                {
                    #region Compare-Protocol

                    if (String.IsNullOrEmpty(compareProtocol))
                    {
                        if (uri.ToString().StartsWith("https://"))
                        {
                            compareProtocol = "https://";
                        }
                        else if (uri.ToString().StartsWith("http://"))
                        {
                            compareProtocol = "http://";
                        }
                        else
                        {
                            throw new ArgumentException("Unknown protocol in URI " + uri.ToString());
                        }
                    }
                    else
                    {
                        if (!uri.ToString().StartsWith(compareProtocol)) throw new ArgumentException("All URIs must begin with the same protocol.");
                    }

                    #endregion

                    #region Compare-Port

                    if (comparePort == -1)
                    {
                        comparePort = uri.Port;
                    }
                    else
                    {
                        if (uri.Port != comparePort) throw new ArgumentException("All URIs must use the same port number.");
                    }

                    #endregion
                }

                if (compareProtocol.Equals("https://")) _Ssl.Enable = true;
                else _Ssl.Enable = false;

                _Port = value[0].Port;
                _Hostnames = new List<string>();
                
                foreach (Uri uri in value)
                {
                    _Hostnames.Add(uri.Host);
                }
            }
        }

        /// <summary>
        /// The port number on which to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0) throw new ArgumentException("Port must be zero or greater.");
                _Port = value;
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
        public Dictionary<string, string> Headers
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

        #endregion

        #region Private-Members

        private List<string> _Hostnames = new List<string> { "localhost" };
        private List<Uri> _Uris = new List<Uri> { new Uri("http://localhost:8080") };
        private int _Port = 8080;

        private IOSettings _IO = new IOSettings();
        private SslSettings _Ssl = new SslSettings();
        private AccessControlManager _AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
        private DebugSettings _Debug = new DebugSettings();

        private Dictionary<string, string> _Headers = new Dictionary<string, string>
        {
            { "Access-Control-Allow-Origin", "*" },
            { "Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE" },
            { "Access-Control-Allow-Headers", "*" },
            { "Accept", "*/*" },
            { "Accept-Language", "en-US, en" },
            { "Accept-Charset", "ISO-8859-1, utf-8" },
            { "Connection", "close" }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        public WatsonWebserverSettings()
        { 

        }

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        /// <param name="hostname">The hostname on which to listen.</param>
        /// <param name="port">The port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public WatsonWebserverSettings(string hostname, int port, bool ssl = false)
        { 
            _Hostnames = new List<string> { hostname };
            _Port = port;
            _Ssl.Enable = ssl; 
        }

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        /// <param name="hostnames">The hostnames on which to listen.</param>
        /// <param name="port">The port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public WatsonWebserverSettings(List<string> hostnames, int port, bool ssl = false)
        {
            _Hostnames = hostnames;
            _Port = port;
            _Ssl.Enable = ssl;
        }

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        /// <param name="uri">URI on which to listen.</param>
        public WatsonWebserverSettings(Uri uri)
        {
            if (uri == null) uri = new Uri("http://localhost:8080");

            _Uris = new List<Uri> { uri };
        }

        /// <summary>
        /// Watson webserver settings.
        /// </summary>
        /// <param name="uris">URIs on which to listen.</param>
        public WatsonWebserverSettings(List<Uri> uris)
        {
            _Uris = uris;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void BuildUrisFromHostnames()
        {
            _Uris = new List<Uri>();

            string prefix = "http://";
            if (_Ssl.Enable) prefix = "https://";

            foreach (string host in _Hostnames)
            {
                _Uris.Add(new Uri(prefix + host + ":" + _Port));    
            }
        }

        private void BuildHostnamesFromUris()
        {
            _Hostnames = new List<string>();

            foreach (Uri uri in _Uris)
            {
                _Hostnames.Add(uri.Host);
            }
        }

        private void ToggleSsl()
        {
            List<Uri> updated = new List<Uri>();

            foreach (Uri uri in _Uris)
            {
                string tempUri = uri.ToString();
                if (tempUri.StartsWith("http://"))
                {
                    tempUri = "https://" + tempUri.Substring(7);
                    updated.Add(new Uri(tempUri));
                }
                else if (tempUri.StartsWith("https://"))
                {
                    tempUri = "http://" + tempUri.Substring(8);
                    updated.Add(new Uri(tempUri));
                }
            }

            _Uris = new List<Uri>(updated);
        }

        #endregion

        #region Public-Classes

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public class IOSettings
        {
            /// <summary>
            /// Buffer size to use when interacting with streams.
            /// </summary>
            public int StreamBufferSize
            {
                get
                {
                    return _StreamBufferSize;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                    _StreamBufferSize = value;
                }
            }

            /// <summary>
            /// Maximum number of concurrent requests.
            /// </summary>
            public int MaxRequests
            {
                get
                {
                    return _MaxRequests;
                }
                set
                {
                    if (value < 1) throw new ArgumentException("Maximum requests must be greater than zero.");
                    _MaxRequests = value;
                }
            }

            private int _StreamBufferSize = 65536;
            private int _MaxRequests = 1024;

            /// <summary>
            /// Input-output settings.
            /// </summary>
            public IOSettings()
            {

            }
        }

        /// <summary>
        /// SSL settings.
        /// </summary>
        public class SslSettings
        {
            /// <summary>
            /// Enable or disable SSL.
            /// </summary>
            public bool Enable = false;

            /// <summary>
            /// Require mutual authentication.
            /// </summary>
            public bool MutuallyAuthenticate = false;

            /// <summary>
            /// Accept invalid certificates including self-signed and those that are unable to be verified.
            /// </summary>
            public bool AcceptInvalidAcertificates = true;

            /// <summary>
            /// SSL settings.
            /// </summary>
            internal SslSettings()
            {
            } 
        }

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public class HeaderSettings
        {
            /// <summary>
            /// Access-Control-Allow-Origin header.
            /// </summary>
            public string AccessControlAllowOrigin = "*";

            /// <summary>
            /// Access-Control-Allow-Methods header.
            /// </summary>
            public string AccessControlAllowMethods = "OPTIONS, HEAD, GET, PUT, POST, DELETE";

            /// <summary>
            /// Access-Control-Allow-Headers header.
            /// </summary>
            public string AccessControlAllowHeaders = "*";

            /// <summary>
            /// Access-Control-Expose-Headers header.
            /// </summary>
            public string AccessControlExposeHeaders = "";

            /// <summary>
            /// Accept header.
            /// </summary>
            public string Accept = "*/*";

            /// <summary>
            /// Accept-Language header.
            /// </summary>
            public string AcceptLanguage = "en-US, en";

            /// <summary>
            /// Accept-Charset header.
            /// </summary>
            public string AcceptCharset = "ISO-8859-1, utf-8";

            /// <summary>
            /// Connection header.
            /// </summary>
            public string Connection = "close";

            /// <summary>
            /// Host header.
            /// </summary>
            public string Host = null;

            /// <summary>
            /// Headers that will be added to every response unless previously set.
            /// </summary>
            public HeaderSettings()
            {

            }
        }

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public class DebugSettings
        {
            /// <summary>
            /// Enable or disable debug logging of access control.
            /// </summary>
            public bool AccessControl = false;

            /// <summary>
            /// Enable or disable debug logging of routing.
            /// </summary>
            public bool Routing = false;
              
            /// <summary>
            /// Enable or disable debug logging of requests.
            /// </summary>
            public bool Requests = false;

            /// <summary>
            /// Enable or disable debug logging of responses.
            /// </summary>
            public bool Responses = false;

            /// <summary>
            /// Debug logging settings.
            /// Be sure to set Events.Logger in order to receive debug messages.
            /// </summary>
            public DebugSettings()
            {

            }
        }

        #endregion
    }
}
