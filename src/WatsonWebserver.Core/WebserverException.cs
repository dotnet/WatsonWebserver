namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Exception type for API route handlers that automatically maps to structured HTTP error responses.
    /// Throw this from within an API route handler to return a specific HTTP status code and error body.
    /// </summary>
    public class WebserverException : Exception
    {
        #region Public-Members

        /// <summary>
        /// The API result indicating the type of error.
        /// </summary>
        public ApiResultEnum Result { get; set; } = ApiResultEnum.InternalError;

        /// <summary>
        /// The HTTP status code derived from the result.
        /// </summary>
        public int StatusCode
        {
            get { return (int)Result; }
        }

        /// <summary>
        /// Optional additional data to include in the error response.
        /// </summary>
        public new object Data { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with an API result.
        /// </summary>
        /// <param name="result">The API result type.</param>
        public WebserverException(ApiResultEnum result)
            : base(GetDefaultMessage(result))
        {
            Result = result;
        }

        /// <summary>
        /// Instantiate with an API result and custom message.
        /// </summary>
        /// <param name="result">The API result type.</param>
        /// <param name="message">Custom error message.</param>
        public WebserverException(ApiResultEnum result, string message)
            : base(message)
        {
            Result = result;
        }

        /// <summary>
        /// Instantiate with an API result, custom message, and inner exception.
        /// </summary>
        /// <param name="result">The API result type.</param>
        /// <param name="message">Custom error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public WebserverException(ApiResultEnum result, string message, Exception innerException)
            : base(message, innerException)
        {
            Result = result;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private static string GetDefaultMessage(ApiResultEnum result)
        {
            switch (result)
            {
                case ApiResultEnum.BadRequest: return "Bad request.";
                case ApiResultEnum.NotAuthorized: return "Not authorized.";
                case ApiResultEnum.Forbidden: return "Forbidden.";
                case ApiResultEnum.NotFound: return "Not found.";
                case ApiResultEnum.RequestTimeout: return "Request timeout.";
                case ApiResultEnum.Conflict: return "Conflict.";
                case ApiResultEnum.SlowDown: return "Too many requests.";
                case ApiResultEnum.InternalError: return "Internal server error.";
                default: return "An error occurred.";
            }
        }

        #endregion
    }
}
