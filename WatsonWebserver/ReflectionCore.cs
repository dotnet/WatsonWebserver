using System;
using System.Collections;
using System.Collections.Generic;
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
        public static Server LoadRoutes<T>(this T server, Assembly assembly = null) where T : Server
        { 
            var staticRoutes = (assembly ?? Assembly.GetCallingAssembly())
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsStaticRoute); // Only select methods that are valid routes

            var dynamicRoutes = (assembly ?? Assembly.GetCallingAssembly())
                .GetTypes() // Get all classes from assembly
                .SelectMany(x => x.GetMethods()) // Get all methods from assembly
                .Where(IsDynamicRoute); // Only select methods that are valid routes
             
            foreach (var staticRoute in staticRoutes)
            {
                var attribute = staticRoute.GetCustomAttributes().OfType<StaticRouteAttribute>().First();
                server.StaticRoutes.Add(attribute.Method, attribute.Path, staticRoute.ToRouteMethod());
            }

            foreach (var dynamicRoute in dynamicRoutes)
            {
                var attribute = dynamicRoute.GetCustomAttributes().OfType<DynamicRouteAttribute>().First();
                server.DynamicRoutes.Add(attribute.Method, attribute.Path, dynamicRoute.ToRouteMethod());
            }

            return server;
        }
         
        private static bool IsStaticRoute(MethodInfo method)
            => method.GetCustomAttributes().OfType<StaticRouteAttribute>().Any() 
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);

        private static bool IsDynamicRoute(MethodInfo method)
            => method.GetCustomAttributes().OfType<DynamicRouteAttribute>().Any() 
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);
         
        private static Func<HttpContext, Task> ToRouteMethod(this MethodInfo method)
        {
            if (method.IsStatic)
            {
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), method);
            }
            else
            {
                object instance = Activator.CreateInstance(method.DeclaringType ?? throw new Exception("Declaring class is null"));
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), instance, method);
            }
        }
    }
}