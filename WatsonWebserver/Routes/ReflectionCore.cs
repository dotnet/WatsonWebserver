using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace WatsonWebserver.Routes
{
    public static class ReflectionCore
    {
        /// <summary>
        /// Load routes from assembly
        /// </summary>
        /// <param name="server"></param>
        /// <param name="assembly"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T LoadRoutes<T>(this T server, Assembly assembly = null) where T : Server
        {
            var routes = (assembly ?? Assembly.GetCallingAssembly())
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsValidRoute); // Only select methods that are valid routes

            foreach (var route in routes)
            {
                var attribute = route.GetCustomAttributes().OfType<Route>().First();
                server.StaticRoutes.Add(attribute.HttpMethod, attribute.RouteName, route.ToRouteMethod());
            }

            return server;
        }

        /// <summary>
        /// Determines whether method is a valid route-method
        /// </summary>
        /// <param name="method"></param>
        /// <returns>true when method is valid</returns>
        private static bool IsValidRoute(MethodInfo method)
            => method.GetCustomAttributes().OfType<Route>().Any() // Must have the Route attribute
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext); 

        /// <summary>
        /// Create delegate method from methodInfo
        /// </summary>
        /// <param name="method"></param>
        /// <returns>static route method</returns>
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