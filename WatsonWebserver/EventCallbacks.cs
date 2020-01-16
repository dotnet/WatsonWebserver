using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Callbacks/actions to use when various events are encountered.
    /// </summary>
    public class EventCallbacks
    {
        #region Public-Members

        /// <summary>
        /// Callback/action to call when a connection is received.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// </summary>
        public Func<string, int, bool> ConnectionReceived
        {
            get
            {
                return _ConnectionReceived;
            }
            set
            {
                _ConnectionReceived = value ?? throw new ArgumentNullException(nameof(ConnectionReceived));
            }
        }

        /// <summary>
        /// Callback/action to call when a request is received.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// </summary>
        public Func<string, int, string, string, bool> RequestReceived
        {
            get
            {
                return _RequestReceived;
            }
            set
            {
                _RequestReceived = value ?? throw new ArgumentNullException(nameof(RequestReceived));
            }
        }

        /// <summary>
        /// Callback/action to call when a request is denied due to access control.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// </summary>
        public Func<string, int, string, string, bool> AccessControlDenied
        {
            get
            {
                return _AccessControlDenied;
            }
            set
            {
                _AccessControlDenied = value ?? throw new ArgumentNullException(nameof(AccessControlDenied));
            }
        }

        /// <summary>
        /// Callback/action to call when a response is sent.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string: Full URL.
        /// int: Response status code.
        /// double: Number of milliseconds.
        /// </summary>
        public Func<string, int, string, string, int, double, bool> ResponseSent
        {
            get
            {
                return _ResponseSent;
            }
            set
            {
                _ResponseSent = value ?? throw new ArgumentNullException(nameof(ResponseSent));
            }
        }

        /// <summary>
        /// Callback/action to call when an exception is encountered.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// Exception: Exception encountered.
        /// </summary>
        public Func<string, int, Exception, bool> ExceptionEncountered
        {
            get
            {
                return _ExceptionEncountered;
            }
            set
            {
                _ExceptionEncountered = value ?? throw new ArgumentNullException(nameof(ExceptionEncountered));
            }
        }
         
        /// <summary>
        /// Callback/action to call when the server is stopped.
        /// </summary>
        public Func<bool> ServerStopped
        {
            get
            {
                return _ServerStopped;
            }
            set
            {
                _ServerStopped = value ?? throw new ArgumentNullException(nameof(ServerStopped));
            }
        }

        /// <summary>
        /// Callback/action to call when the server is disposed.
        /// </summary>
        public Func<bool> ServerDisposed
        {
            get
            {
                return _ServerDisposed;
            }
            set
            {
                _ServerDisposed = value ?? throw new ArgumentNullException(nameof(ServerDisposed));
            }
        }

        #endregion

        #region Private-Members

        private Func<string, int, bool> _ConnectionReceived = null;
        private Func<string, int, string, string, bool> _RequestReceived = null;
        private Func<string, int, string, string, bool> _AccessControlDenied = null;
        private Func<string, int, string, string, int, double, bool> _ResponseSent = null;
        private Func<string, int, Exception, bool> _ExceptionEncountered = null;
        private Func<bool> _ServerStopped = null;
        private Func<bool> _ServerDisposed = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EventCallbacks()
        {
            _ConnectionReceived = ConnectionReceivedInternal;
            _RequestReceived = RequestReceivedInternal;
            _AccessControlDenied = AccessControlDeniedInternal;
            _ResponseSent = ResponseSentInternal;
            _ExceptionEncountered = ExceptionEncounteredInternal; 
            _ServerStopped = ServerStoppedInternal;
            _ServerDisposed = ServerDisposedInternal;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private bool ConnectionReceivedInternal(string ip, int port)
        {
            return true;
        }

        private bool RequestReceivedInternal(string ip, int port, string method, string url)
        {
            return true;
        }

        private bool AccessControlDeniedInternal(string ip, int port, string method, string url)
        {
            return true;
        }

        private bool ResponseSentInternal(string ip, int port, string method, string url, int status, double totalTimeMs)
        {
            return true;
        }

        private bool ExceptionEncounteredInternal(string ip, int port, Exception e)
        {
            return true;
        }
         
        private bool ServerStoppedInternal()
        {
            return true;
        }

        private bool ServerDisposedInternal()
        {
            return true;
        }

        #endregion
    }
}
