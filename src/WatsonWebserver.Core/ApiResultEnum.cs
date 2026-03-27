namespace WatsonWebserver.Core
{
    /// <summary>
    /// API result enumeration indicating the outcome of an API request.
    /// Each value maps to an HTTP status code.
    /// </summary>
    public enum ApiResultEnum
    {
        /// <summary>
        /// Success (HTTP 200).
        /// </summary>
        Success = 200,

        /// <summary>
        /// Created (HTTP 201).
        /// </summary>
        Created = 201,

        /// <summary>
        /// Bad request (HTTP 400).
        /// </summary>
        BadRequest = 400,

        /// <summary>
        /// Not authorized (HTTP 401).
        /// </summary>
        NotAuthorized = 401,

        /// <summary>
        /// Forbidden (HTTP 403).
        /// </summary>
        Forbidden = 403,

        /// <summary>
        /// Not found (HTTP 404).
        /// </summary>
        NotFound = 404,

        /// <summary>
        /// Request timeout (HTTP 408).
        /// </summary>
        RequestTimeout = 408,

        /// <summary>
        /// Conflict (HTTP 409).
        /// </summary>
        Conflict = 409,

        /// <summary>
        /// Too many requests (HTTP 429).
        /// </summary>
        SlowDown = 429,

        /// <summary>
        /// Internal server error (HTTP 500).
        /// </summary>
        InternalError = 500,

        /// <summary>
        /// Deserialization error (HTTP 400).
        /// </summary>
        DeserializationError = 400
    }
}
