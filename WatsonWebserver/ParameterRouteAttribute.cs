using System;

namespace WatsonWebserver
{
    /// <summary>
    /// Attribute that is used to mark methods as a parameter route.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ParameterRouteAttribute : Attribute
    {
        /// <summary>
        /// The path to match, i.e. /{version}/api/{id}.
        /// If a match is found, the Dictionary found in HttpRequest.Url.Parameters will contain keys for 'version' and 'id'.
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
        /// <param name="path">The path to match, i.e. /{version}/api/{id}.</param>
        public ParameterRouteAttribute(HttpMethod method, string path)
        {
            Path = path;
            Method = method;
        }
    }
}