using System;

namespace WatsonWebserver
{
    /// <summary>
    /// Attribute that is used to mark methods as route methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RouteAttribute : Attribute
    {
        /// <summary>
        /// The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>Instantiate the object.</summary>
        /// <param name="path">The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.</param>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        public RouteAttribute(string path, HttpMethod method = HttpMethod.GET)
        {
            Path = path;
            Method = method;
        }
    }
}