namespace WatsonWebserver.Core.OpenApi
{
    using System;

    /// <summary>
    /// Thrown when an OpenAPI document cannot be generated because the requested settings are
    /// inconsistent with one another or with the selected <see cref="OpenApiVersionEnum"/>.
    /// Examples include requesting a 3.2-only construct while targeting 3.0, supplying both a
    /// license URL and an SPDX identifier, or declaring an OAuth2 scheme without any flows.
    /// </summary>
    public class OpenApiValidationException : Exception
    {
        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        public OpenApiValidationException()
        {
        }

        /// <summary>
        /// Instantiate the exception with a message describing the validation failure.
        /// </summary>
        /// <param name="message">A message describing which setting is invalid and why.</param>
        public OpenApiValidationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Instantiate the exception with a message and an inner exception.
        /// </summary>
        /// <param name="message">A message describing which setting is invalid and why.</param>
        /// <param name="innerException">The underlying exception, if any.</param>
        public OpenApiValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        #endregion
    }
}
