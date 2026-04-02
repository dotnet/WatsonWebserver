namespace WatsonWebserver.Core
{
    using System;
    using System.IO;

    /// <summary>
    /// Exception thrown when an HTTP/1.1 request is malformed.
    /// </summary>
    public class MalformedHttpRequestException : IOException
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        public MalformedHttpRequestException()
        {
        }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        public MalformedHttpRequestException(string message) : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public MalformedHttpRequestException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
