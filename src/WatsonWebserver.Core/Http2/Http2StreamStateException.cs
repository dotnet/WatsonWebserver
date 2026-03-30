namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// Exception indicating an invalid HTTP/2 stream state transition.
    /// </summary>
    public class Http2StreamStateException : Exception
    {
        /// <summary>
        /// Current stream state.
        /// </summary>
        public Http2StreamState CurrentState { get; private set; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="currentState">Current stream state.</param>
        /// <param name="message">Failure message.</param>
        public Http2StreamStateException(Http2StreamState currentState, string message)
            : base(message)
        {
            CurrentState = currentState;
        }
    }
}
