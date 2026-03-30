namespace WatsonWebserver.Core
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Structured API error response returned to clients when an error occurs.
    /// </summary>
    public class ApiErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// The API result indicating the type of error.
        /// </summary>
        [JsonPropertyOrder(0)]
        public ApiResultEnum Error { get; set; } = ApiResultEnum.InternalError;

        /// <summary>
        /// The HTTP status code derived from the error type.
        /// </summary>
        [JsonPropertyOrder(1)]
        public int StatusCode
        {
            get { return (int)Error; }
        }

        /// <summary>
        /// Human-readable description of the error type.
        /// </summary>
        [JsonPropertyOrder(2)]
        public string Description
        {
            get { return GetDescription(Error); }
        }

        /// <summary>
        /// Optional custom error message providing additional context.
        /// </summary>
        [JsonPropertyOrder(3)]
        public string Message { get; set; } = null;

        /// <summary>
        /// Optional additional data associated with the error.
        /// </summary>
        [JsonPropertyOrder(4)]
        public object Data { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiErrorResponse()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private static string GetDescription(ApiResultEnum result)
        {
            switch (result)
            {
                case ApiResultEnum.Success:
                    return "The request was successful.";
                case ApiResultEnum.Created:
                    return "The resource was created.";
                case ApiResultEnum.BadRequest:
                    return "The request was invalid or malformed.";
                case ApiResultEnum.NotAuthorized:
                    return "Authentication is required or has failed.";
                case ApiResultEnum.Forbidden:
                    return "Access to this resource is forbidden.";
                case ApiResultEnum.NotFound:
                    return "The requested resource was not found.";
                case ApiResultEnum.RequestTimeout:
                    return "The request timed out.";
                case ApiResultEnum.Conflict:
                    return "The request conflicts with the current state of the resource.";
                case ApiResultEnum.SlowDown:
                    return "Too many requests. Please slow down.";
                case ApiResultEnum.InternalError:
                    return "An internal server error occurred.";
                default:
                    return "An unknown error occurred.";
            }
        }

        #endregion
    }
}
