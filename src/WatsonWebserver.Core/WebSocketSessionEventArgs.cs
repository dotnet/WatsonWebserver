namespace WatsonWebserver.Core
{
    using System;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Event arguments for WebSocket session lifecycle events.
    /// </summary>
    public class WebSocketSessionEventArgs : EventArgs
    {
        /// <summary>
        /// HTTP context associated with the session when available.
        /// </summary>
        public HttpContextBase Context { get; }

        /// <summary>
        /// WebSocket session.
        /// </summary>
        public WebSocketSession Session { get; }

        /// <summary>
        /// Instantiate the event arguments.
        /// </summary>
        public WebSocketSessionEventArgs(HttpContextBase context, WebSocketSession session)
        {
            Context = context;
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }
    }
}
