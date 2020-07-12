using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace WatsonWebserver
{ 
    /// <summary>
    /// Helper methods for reflection.
    /// </summary>
    public static class ReflectionCore
    {
        /// <summary>
        /// Load routes for the server.
        /// </summary>
        /// <typeparam name="T">Server.</typeparam>
        /// <param name="server">Server.</param>
        /// <param name="assembly">Assembly.</param>
        /// <returns>Server.</returns>
        public static T LoadRoutes<T>(this T server, Assembly assembly = null) where T : Server
        {
            var routes = (assembly ?? Assembly.GetCallingAssembly())
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsValidRoute); // Only select methods that are valid routes

            foreach (var route in routes)
            {
                var attribute = route.GetCustomAttributes().OfType<RouteAttribute>().First();
                server.StaticRoutes.Add(attribute.Method, attribute.Path, route.ToRouteMethod());
            }

            return server;
        }
         
        private static bool IsValidRoute(MethodInfo method)
            => method.GetCustomAttributes().OfType<RouteAttribute>().Any() // Must have the Route attribute
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext); 
         
        private static Func<HttpContext, Task> ToRouteMethod(this MethodInfo method)
        {
            if (method.IsStatic)
                return (Func<HttpContext, Task>) Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), method);
            else
            {
                object classInstance =
                    Activator.CreateInstance(method.DeclaringType ?? throw new Exception("Declaring class is null"));
                return (Func<HttpContext, Task>) Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), classInstance,
                    method);
            }
        }
    }
}