using System;
using System.Text.RegularExpressions;

namespace WatsonWebserver
{
    /// <summary>
    /// Attribute that is used to mark methods as a dynamic route.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DynamicRouteAttribute : Attribute
    {
        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        public HttpMethod Method = HttpMethod.GET;

        /// <summary>
        /// The pattern against which the raw URL should be matched. Must be convertible to a regular expression. 
        /// </summary>
        public Regex Path = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The regular expression pattern against which the raw URL should be matched.</param>
        public DynamicRouteAttribute(HttpMethod method, string path)
        {
            Path = new Regex(path);
            Method = method;
        }
    }
}