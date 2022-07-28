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
        /// Globally-unique identifier.
        /// </summary>
        public string GUID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The regular expression pattern against which the raw URL should be matched.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public DynamicRouteAttribute(HttpMethod method, string path, string guid = null, object metadata = null)
        {
            Path = new Regex(path);
            Method = method;
            
            if (!String.IsNullOrEmpty(guid)) GUID = guid;
            if (metadata != null) Metadata = metadata;
        }
    }
}