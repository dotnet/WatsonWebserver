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
        public Action<string, int> ConnectionReceived
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
        public Action<string, int, string, string> RequestReceived
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
        public Action<string, int, string, string> AccessControlDenied
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
        /// Callback/action to call when a requestor disconnected unexpectedly.
        /// string: IP address of the client.
        /// int: Source TCP port of the client.
        /// string: HTTP method.
        /// string Full URL.
        /// </summary>
        public Action<string, int, string, string> RequestorDisconnected
        {
            get
            {
                return _RequestorDisconnected;
            }
            set
            {
                _RequestorDisconnected = value ?? throw new ArgumentNullException(nameof(RequestorDisconnected));
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
        public Action<string, int, string, string, int, double> ResponseSent
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
        public Action<string, int, Exception> ExceptionEncountered
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
        public Action ServerStopped
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
        public Action ServerDisposed
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

        private Action<string, int> _ConnectionReceived = null;
        private Action<string, int, string, string> _RequestReceived = null;
        private Action<string, int, string, string> _AccessControlDenied = null;
        private Action<string, int, string, string> _RequestorDisconnected = null;
        private Action<string, int, string, string, int, double> _ResponseSent = null;
        private Action<string, int, Exception> _ExceptionEncountered = null;
        private Action _ServerStopped = null;
        private Action _ServerDisposed = null;

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

        private void ConnectionReceivedInternal(string ip, int port)
        {
        }

        private void RequestReceivedInternal(string ip, int port, string method, string url)
        {
        }

        private void AccessControlDeniedInternal(string ip, int port, string method, string url)
        {
        }

        private void ResponseSentInternal(string ip, int port, string method, string url, int status, double totalTimeMs)
        {
        }

        private void ExceptionEncounteredInternal(string ip, int port, Exception e)
        {
        }
         
        private void ServerStoppedInternal()
        {
        }

        private void ServerDisposedInternal()
        {
        }

        #endregion
    }
}
