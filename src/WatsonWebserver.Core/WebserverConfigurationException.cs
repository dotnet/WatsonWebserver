namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Exception thrown when the server configuration is incoherent.
    /// </summary>
    public class WebserverConfigurationException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public WebserverConfigurationException(string message) : base(message)
        {
        }
    }
}
