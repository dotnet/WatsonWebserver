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
        /// Prefixes on which to listen.
        /// </summary>
        public List<string> Prefixes
        {
            get
            {
                return _Prefixes; 
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Prefixes));
                if (value.Count < 1) throw new ArgumentException("At least one prefix must be specified.");
                _Prefixes = value; 
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

        private List<string> _Prefixes = new List<string>(); 
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
            if (String.IsNullOrEmpty(hostname)) hostname = "localhost";
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            string prefix = "http";
            if (ssl) prefix += "s://" + hostname + ":" + port + "/";
            else prefix += "://" + hostname + ":" + port + "/";
            _Prefixes.Add(prefix);
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
            if (hostnames == null) hostnames = new List<string> { "localhost" };
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            foreach (string hostname in hostnames)
            {
                string prefix = "http";
                if (ssl) prefix += "s://" + hostname + ":" + port + "/";
                else prefix += "://" + hostname + ":" + port + "/";
                _Prefixes.Add(prefix);
            }

            _Ssl.Enable = ssl;
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
