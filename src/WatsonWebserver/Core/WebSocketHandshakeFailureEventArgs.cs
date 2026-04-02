namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Event arguments for failed WebSocket handshakes.
    /// </summary>
    public class WebSocketHandshakeFailureEventArgs : EventArgs
    {
        /// <summary>
        /// HTTP context when available.
        /// </summary>
        public HttpContextBase Context { get; }

        /// <summary>
        /// Failure reason.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Exception, when one exists.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Instantiate the event arguments.
        /// </summary>
        public WebSocketHandshakeFailureEventArgs(HttpContextBase context, string reason, Exception exception = null)
        {
            Context = context;
            Reason = reason;
            Exception = exception;
        }
    }
}
