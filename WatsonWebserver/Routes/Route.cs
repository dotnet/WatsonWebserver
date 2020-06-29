using System;

namespace WatsonWebserver.Routes
{
    /// <summary>
    /// Attribute that is used to mark methods as route-methods
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class Route : Attribute
    {
        public string RouteName { get; }
        public HttpMethod HttpMethod { get; }
        
        /// <summary></summary>
        /// <param name="routeName"></param>
        /// <param name="httpMethod"></param>
        public Route(string routeName, HttpMethod httpMethod = HttpMethod.GET)
        {
            RouteName = routeName;
            HttpMethod = httpMethod;
        }
    }
}