
using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WatsonWebserver
{
    /// <summary>
    /// Helper methods for Auto route.
    /// </summary>
    public static class AutoRoute
    {

        #region AutoRoute
        /// <summary>
        /// Register the assemblies for Auto Route.
        /// The public methods in public classes will be auto routed.
        /// Support the Route attribute and DRoute attribute.
        /// </summary>
        /// <typeparam name="T">Server.</typeparam>
        /// <param name="server">Server.</param>
        /// <param name="assemblies">Specific assemblies register to server.</param>
        /// <returns>Server.</returns>
        public static T Register<T>(this T server, params Assembly[] assemblies) where T : Server
        {
            if (assemblies.Length == 0)
                assemblies = new Assembly[] { Assembly.GetCallingAssembly() };

            foreach (var asm in assemblies)
            {
                Register(asm, server);
            }
            return server;
        }
        /// <summary>
        /// Register the assemblies for Auto Route.
        /// </summary>
        private static void Register(Assembly assembly, Server srv)
        {
            var classes = assembly
                .GetTypes()
                .Where(w => w.IsPublic)                                         // Get all public classes from assembly
                .Where(w => w.GetCustomAttributes().OfType<RouteAttribute>().Any());

            foreach (var clas in classes)
            {
                var methods = clas.GetMethods()
                    .Where(w => w.IsPublic);                                    // Get all public methods from assembly
                var staticRoutes = methods.Where(IsValidStaticRoute);           // Only select methods that are valid routes
                var dynamicRoutes = methods.Where(IsValidDynamicRouteD);               // Only select methods that are valid Droutes
                var parentRoute = clas.                                         // Get the RouteAttribute from class
                    GetCustomAttributes().OfType<RouteAttribute>().
                    FirstOrDefault();

                //Process static route
                foreach (var route in staticRoutes)
                {
                    var withMethodName = false;
                    var attribute = route.GetCustomAttributes().OfType<RouteAttribute>().FirstOrDefault();
                    if (attribute == null)
                    {
                        attribute = new RouteAttribute(route.Name);
                        withMethodName = true;
                    }
                    var path = $"{parentRoute.Path}{attribute.Path}";
                    srv.StaticRoutes.Add(
                        attribute.Method,
                        path,
                        route.ToRouteMethod());
                    Debug.WriteLine($"Auto static routed: {attribute.Method}  {path}  by {(withMethodName == true ? "Method Name" : "Route Arrtibute")}");
                }

                //Process dynamic route
                foreach (var route in dynamicRoutes)
                {
                    var attribute = route.GetCustomAttributes().OfType<DRouteAttribute>().First();
                    var regx = $"^{parentRoute.Path}{attribute.Path.TrimStart('^')}";
                    if (regx.Contains("//"))
                    {
                        var warning = $"AutoRoute Warning: the final regex pattern:  {regx}   may not correct.";
                        Debug.WriteLine(warning);
                        Console.WriteLine(warning);
                    }
                    srv.DynamicRoutes.Add(
                        attribute.Method,
                        new Regex(regx),
                        route.ToRouteMethod());
                    Debug.WriteLine($"Auto dynamic routed: {attribute.Method}  {regx}  by DRoute Arrtibute");
                }
            }


        }
        #endregion

        private static bool IsValidStaticRoute(MethodInfo method)
            => (method.GetCustomAttributes().OfType<RouteAttribute>().Any()
            || (method.IsPublic && !method.GetCustomAttributes().OfType<DRouteAttribute>().Any())) // Have Route attribute or is public and not dynamic
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);


        private static bool IsValidDynamicRouteD(MethodInfo method)
            => method.GetCustomAttributes().OfType<DRouteAttribute>().Any() // Must have the Route attribute
               && method.ReturnType == typeof(Task)
               && method.GetParameters().Length == 1
               && method.GetParameters().First().ParameterType == typeof(HttpContext);

        private static Func<HttpContext, Task> ToRouteMethod(this MethodInfo method)
        {
            if (method.IsStatic)
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), method);
            else
            {
                if (method.DeclaringType == null) throw new Exception("Declaring class is null");
                var constructors = method.DeclaringType.GetConstructors();
                foreach (var item in constructors)
                {
                    var paramsInfos = item.GetParameters();
                    if (paramsInfos.Length > 0)
                    {
                        throw new Exception("API class can't have constructors with params.");
                    }
                }

                object classInstance = Activator.CreateInstance(method.DeclaringType);
                return (Func<HttpContext, Task>)Delegate.CreateDelegate(typeof(Func<HttpContext, Task>), classInstance,
                    method);
            }
        }
    }
}

