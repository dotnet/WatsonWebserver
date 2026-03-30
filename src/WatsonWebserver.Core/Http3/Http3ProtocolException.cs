namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.IO;

    /// <summary>
    /// HTTP/3 protocol failure.
    /// </summary>
    public class Http3ProtocolException : IOException
    {
        /// <summary>
        /// Application error code associated with the failure.
        /// </summary>
        public Http3ErrorCode ErrorCode { get; private set; } = Http3ErrorCode.GeneralProtocolError;

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Failure message.</param>
        public Http3ProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="errorCode">HTTP/3 error code.</param>
        /// <param name="message">Failure message.</param>
        public Http3ProtocolException(Http3ErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
