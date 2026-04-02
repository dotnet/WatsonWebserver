namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// Exception indicating an HTTP/2 protocol violation.
    /// </summary>
    public class Http2ProtocolException : Exception
    {
        /// <summary>
        /// HTTP/2 error code associated with the failure.
        /// </summary>
        public Http2ErrorCode ErrorCode { get; private set; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="errorCode">HTTP/2 error code.</param>
        /// <param name="message">Failure message.</param>
        public Http2ProtocolException(Http2ErrorCode errorCode, string message)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="errorCode">HTTP/2 error code.</param>
        /// <param name="message">Failure message.</param>
        /// <param name="innerException">Inner exception.</param>
        public Http2ProtocolException(Http2ErrorCode errorCode, string message, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
