using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver.Core
{
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
        public event EventHandler<ConnectionEventArgs> ConnectionReceived = delegate { };

        /// <summary>
        /// Event to fire when a connection is denied.
        /// This event is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        public event EventHandler<ConnectionEventArgs> ConnectionDenied = delegate { };

        /// <summary>
        /// Event to fire  when a request is received. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate { };

        /// <summary>
        /// Event to fire  when a request is denied due to access control. 
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestDenied = delegate { };
         
        /// <summary>
        /// Event to fire when a requestor disconnected unexpectedly.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestorDisconnected = delegate { };

        /// <summary>
        /// Event to fire when a response is sent.
        /// </summary>
        public event EventHandler<ResponseEventArgs> ResponseSent = delegate { };

        /// <summary>
        /// Event to fire when an exception is encountered.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> ExceptionEncountered = delegate { };

        /// <summary>
        /// Event to fire when the server is started.
        /// </summary>
        public event EventHandler ServerStarted = delegate { };

        /// <summary>
        /// Event to fire when the server is stopped.
        /// </summary>
        public event EventHandler ServerStopped = delegate { };

        /// <summary>
        /// Event to fire when the server is being disposed.
        /// </summary>
        public event EventHandler ServerDisposing = delegate { }; 

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
            WrappedEventHandler(() => ConnectionReceived?.Invoke(sender, args), "ConnectionReceived", sender);
        }

        /// <summary>
        /// Handle connection denied event.
        /// This event is not used by WatsonWebserver, only WatsonWebserver.Lite.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleConnectionDenied(object sender, ConnectionEventArgs args)
        {
            WrappedEventHandler(() => ConnectionDenied?.Invoke(sender, args), "ConnectionDenied", sender);
        }

        /// <summary>
        /// Handle request received event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleRequestReceived(object sender, RequestEventArgs args)
        {
            WrappedEventHandler(() => RequestReceived?.Invoke(sender, args), "RequestReceived", sender);
        }

        /// <summary>
        /// Handle request denied event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleRequestDenied(object sender, RequestEventArgs args)
        {
            WrappedEventHandler(() => RequestDenied?.Invoke(sender, args), "RequestDenied", sender);
        }

        /// <summary>
        /// Handle response sent event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleResponseSent(object sender, ResponseEventArgs args)
        {
            WrappedEventHandler(() => ResponseSent?.Invoke(sender, args), "ResponseSent", sender);
        }

        /// <summary>
        /// Handle exception encountered event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            WrappedEventHandler(() => ExceptionEncountered?.Invoke(sender, args), "ExceptionEncountered", sender);
        }

        /// <summary>
        /// Handle server started event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerStarted(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerStarted?.Invoke(sender, args), "ServerStarted", sender);
        }

        /// <summary>
        /// Handle server stopped event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerStopped(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerStopped?.Invoke(sender, args), "ServerStopped", sender);
        }

        /// <summary>
        /// Handle server disposing event.
        /// </summary>
        /// <param name="sender">Sender.</param>
        /// <param name="args">Args.</param>
        public void HandleServerDisposing(object sender, EventArgs args)
        {
            WrappedEventHandler(() => ServerDisposing?.Invoke(sender, args), "ServerDisposing", sender);
        }

        #endregion

        #region Private-Methods

        private void WrappedEventHandler(Action action, string handler, object sender)
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
