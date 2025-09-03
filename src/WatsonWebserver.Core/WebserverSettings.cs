namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

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
        private IOSettings _IO = new IOSettings();
        private SslSettings _Ssl = new SslSettings();
        private AccessControlManager _AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
        private DebugSettings _Debug = new DebugSettings();
        private HeaderSettings _Headers = new HeaderSettings();
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

            /// <summary>
            /// Read timeout, in milliseconds.
            /// This property is only used by WatsonWebserver.Lite.
            /// </summary>
            public int ReadTimeoutMs
            {
                get
                {
                    return _ReadTimeoutMs;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMs));
                    _ReadTimeoutMs = value;
                }
            }

            /// <summary>
            /// Maximum incoming header size, in bytes.
            /// This property is only used by WatsonWebserver.Lite.
            /// </summary>
            public int MaxIncomingHeadersSize
            {
                get
                {
                    return _MaxIncomingHeadersSize;
                }
                set
                {
                    if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxIncomingHeadersSize));
                    _MaxIncomingHeadersSize = value;
                }
            }

            /// <summary>
            /// Flag indicating whether or not the server requests a persistent connection.
            /// </summary>
            public bool EnableKeepAlive { get; set; } = false;

            private int _StreamBufferSize = 65536;
            private int _MaxRequests = 1024;
            private int _ReadTimeoutMs = 10000;
            private int _MaxIncomingHeadersSize = 65536;

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
            public bool Enable { get; set; } = false;

            /// <summary>
            /// Certifcate for SSL.
            /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
            /// </summary>
            public X509Certificate2 SslCertificate
            {
                get
                {
                    if (_SslCertificate == null)
                    {
                        if (!String.IsNullOrEmpty(PfxCertificateFile))
                        {
                            if (!String.IsNullOrEmpty(PfxCertificatePassword))
                            {
                                _SslCertificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile), PfxCertificatePassword);
                            }
                            else
                            {
                                _SslCertificate = new X509Certificate2(File.ReadAllBytes(PfxCertificateFile));
                            }
                        }
                    }

                    return _SslCertificate;
                }
                set
                {
                    _SslCertificate = value;
                }
            }

            /// <summary>
            /// PFX certificate filename.
            /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
            /// </summary>
            public string PfxCertificateFile { get; set; } = null;

            /// <summary>
            /// PFX certificate password.
            /// For WatsonWebserver, install the certificate in your operating system.  This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
            /// </summary>
            public string PfxCertificatePassword { get; set; } = null;

            /// <summary>
            /// Require mutual authentication.
            /// This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
            /// </summary>
            public bool MutuallyAuthenticate { get; set; } = false;

            /// <summary>
            /// Accept invalid certificates including self-signed and those that are unable to be verified.
            /// This property is not used by WatsonWebserver, only WatsonWebserver.Lite.
            /// </summary>
            public bool AcceptInvalidAcertificates { get; set; } = true;

            private X509Certificate2 _SslCertificate = null;

            /// <summary>
            /// SSL settings.
            /// </summary>
            public SslSettings()
            {
            }
        }

        /// <summary>
        /// Header settings.
        /// </summary>
        public class HeaderSettings
        {
            /// <summary>
            /// Automatically set content length if not already set.
            /// </summary>
            public bool IncludeContentLength { get; set; } = true;

            /// <summary>
            /// Headers to add to each request.
            /// </summary>
            public Dictionary<string, string> DefaultHeaders
            {
                get
                {
                    return _DefaultHeaders;
                }
                set
                {
                    if (value == null) _DefaultHeaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    else _DefaultHeaders = value;
                }
            }

            private Dictionary<string, string> _DefaultHeaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { WebserverConstants.HeaderAccessControlAllowOrigin, "*" },
                { WebserverConstants.HeaderAccessControlAllowMethods, "OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH" },
                { WebserverConstants.HeaderAccessControlAllowHeaders, "*" },
                { WebserverConstants.HeaderAccessControlExposeHeaders, "" },
                { WebserverConstants.HeaderAccept, "*/*" },
                { WebserverConstants.HeaderAcceptLanguage, "en-US, en" },
                { WebserverConstants.HeaderAcceptCharset, "ISO-8859-1, utf-8" },
                { WebserverConstants.HeaderCacheControl, "no-cache" },
                { WebserverConstants.HeaderConnection, "close" },
                { WebserverConstants.HeaderHost, "localhost:8000" }
            };

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
            public bool AccessControl { get; set; } = false;

            /// <summary>
            /// Enable or disable debug logging of routing.
            /// </summary>
            public bool Routing { get; set; } = false;

            /// <summary>
            /// Enable or disable debug logging of requests.
            /// </summary>
            public bool Requests { get; set; } = false;

            /// <summary>
            /// Enable or disable debug logging of responses.
            /// </summary>
            public bool Responses { get; set; } = false;

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
