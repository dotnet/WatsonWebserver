namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Callbacks/actions to use when various events are encountered.
    /// </summary>
    public class WebserverEvents
    {
        #region Public-Members

        /// <summary>
        /// Method to use for sending log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Event to fire when a connection is received.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionReceived;

        /// <summary>
        /// Event to fire when a connection is denied.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionDenied;

        /// <summary>
        /// Event to fire  when a request is received. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived;

        /// <summary>
        /// Event to fire  when a request is denied due to access control. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestDenied;
         
        /// <summary>
        /// Event to fire when a requestor disconnected unexpectedly.
        /// </summary>
#pragma warning disable CS0067
        public event EventHandler<RequestEventArgs> RequestorDisconnected;
#pragma warning restore CS0067

        /// <summary>
        /// Event to fire when a response is sent.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseSent;

        /// <summary>
        /// Event to fire when a response starts.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseStarting;

        /// <summary>
        /// Event to fire when a response completes.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseCompleted;

        /// <summary>
        /// Event to fire when request processing is aborted.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestAborted;

        /// <summary>
        /// Event to fire when an exception is encountered.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExceptionEncountered;

        /// <summary>
        /// Event to fire when the server is started.
        /// </summary>
        public event EventHandler ServerStarted;

        /// <summary>
        /// Event to fire when the server is stopped.
        /// </summary>
        public event EventHandler ServerStopped;

        /// <summary>
        /// Event to fire when the server is being disposed.
        /// </summary>
        public event EventHandler ServerDisposing; 

        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public WebserverEvents()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Handle connection received event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleConnectionReceived(object sender, ConnectionEventArgs args)
        {
            EventHandler<ConnectionEventArgs> handler = ConnectionReceived;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ConnectionReceived");
        }

        /// <summary>
        /// Handle connection denied event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleConnectionDenied(object sender, ConnectionEventArgs args)
        {
            EventHandler<ConnectionEventArgs> handler = ConnectionDenied;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ConnectionDenied");
        }

        /// <summary>
        /// Handle request received event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleRequestReceived(object sender, RequestEventArgs args)
        {
            EventHandler<RequestEventArgs> handler = RequestReceived;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "RequestReceived");
        }

        /// <summary>
        /// Handle request denied event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleRequestDenied(object sender, RequestEventArgs args)
        {
            EventHandler<RequestEventArgs> handler = RequestDenied;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "RequestDenied");
        }

        /// <summary>
        /// Handle response sent event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleResponseSent(object sender, ResponseEventArgs args)
        {
            EventHandler<ResponseEventArgs> handler = ResponseSent;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ResponseSent");
        }

        /// <summary>
        /// Handle response starting event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleResponseStarting(object sender, ResponseEventArgs args)
        {
            EventHandler<ResponseEventArgs> handler = ResponseStarting;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ResponseStarting");
        }

        /// <summary>
        /// Handle response completed event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleResponseCompleted(object sender, ResponseEventArgs args)
        {
            EventHandler<ResponseEventArgs> handler = ResponseCompleted;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ResponseCompleted");
        }

        /// <summary>
        /// Handle request aborted event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleRequestAborted(object sender, RequestEventArgs args)
        {
            EventHandler<RequestEventArgs> handler = RequestAborted;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "RequestAborted");
        }

        /// <summary>
        /// Handle exception encountered event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            EventHandler<ExceptionEventArgs> handler = ExceptionEncountered;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ExceptionEncountered");
        }

        /// <summary>
        /// Handle server started event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerStarted(object sender, EventArgs args)
        {
            EventHandler handler = ServerStarted;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ServerStarted");
        }

        /// <summary>
        /// Handle server stopped event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerStopped(object sender, EventArgs args)
        {
            EventHandler handler = ServerStopped;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ServerStopped");
        }

        /// <summary>
        /// Handle server disposing event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerDisposing(object sender, EventArgs args)
        {
            EventHandler handler = ServerDisposing;
            if (handler == null) return;
            WrappedEventHandler(() => handler(sender, args), "ServerDisposing");
        }

        /// <summary>
        /// Indicates whether any request received handlers are attached.
        /// </summary>
        public bool HasRequestReceivedHandlers
        {
            get
            {
                return RequestReceived != null;
            }
        }

        /// <summary>
        /// Indicates whether any request denied handlers are attached.
        /// </summary>
        public bool HasRequestDeniedHandlers
        {
            get
            {
                return RequestDenied != null;
            }
        }

        /// <summary>
        /// Indicates whether any request aborted handlers are attached.
        /// </summary>
        public bool HasRequestAbortedHandlers
        {
            get
            {
                return RequestAborted != null;
            }
        }

        /// <summary>
        /// Indicates whether any exception handlers are attached.
        /// </summary>
        public bool HasExceptionEncounteredHandlers
        {
            get
            {
                return ExceptionEncountered != null;
            }
        }

        /// <summary>
        /// Indicates whether any response sent handlers are attached.
        /// </summary>
        public bool HasResponseSentHandlers
        {
            get
            {
                return ResponseSent != null;
            }
        }

        /// <summary>
        /// Indicates whether any response starting handlers are attached.
        /// </summary>
        public bool HasResponseStartingHandlers
        {
            get
            {
                return ResponseStarting != null;
            }
        }

        /// <summary>
        /// Indicates whether any response completed handlers are attached.
        /// </summary>
        public bool HasResponseCompletedHandlers
        {
            get
            {
                return ResponseCompleted != null;
            }
        }

        #endregion

        #region Private-Methods

        private void WrappedEventHandler(Action action, string handler)
        {
            if (action == null) return;

            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Logger?.Invoke("Event handler exception in " + handler + ": " + e.Message);
            }
        }

        #endregion
    }
}
