using System;

namespace WatsonWebserver
{
    /// <summary>
    /// Attribute that is used to mark methods as a static route.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class StaticRouteAttribute : Attribute
    {
        /// <summary>
        /// The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.
        /// </summary>
        public string Path = null;

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public HttpMethod Method = HttpMethod.GET;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.</param>
        public StaticRouteAttribute(HttpMethod method, string path)
        {
            Path = path;
            Method = method;
        }
    }
}