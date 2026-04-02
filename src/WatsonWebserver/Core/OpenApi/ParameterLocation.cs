namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// The location of a parameter in an HTTP request.
    /// </summary>
    public enum ParameterLocation
    {
        /// <summary>
        /// Parameter is passed in the query string (e.g., ?foo=bar).
        /// </summary>
        Query,

        /// <summary>
        /// Parameter is passed in an HTTP header.
        /// </summary>
        Header,

        /// <summary>
        /// Parameter is part of the URL path (e.g., /users/{id}).
        /// </summary>
        Path,

        /// <summary>
        /// Parameter is passed as a cookie.
        /// </summary>
        Cookie
    }
}
