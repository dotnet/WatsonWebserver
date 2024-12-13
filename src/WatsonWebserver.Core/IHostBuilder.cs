namespace WatsonWebserver.Core
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// Host builder interface.
    /// </summary>
    /// <typeparam name="HostBuilder">Host builder.</typeparam>
    /// <typeparam name="InputAction">Input action.</typeparam>
    public interface IHostBuilder<HostBuilder, InputAction>
    { 
        /// <summary>
        /// Map the pre-flight route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapPreflightRoute(InputAction handler);

        /// <summary>
        /// Map the pre-routing route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapPreRoutingRoute(InputAction handler);

        /// <summary>
        /// Map an authentication route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapAuthenticationRoute(InputAction handler);

        /// <summary>
        /// Map the default route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapDefaultRoute(InputAction handler);

        /// <summary>
        /// Map the post-routing route.
        /// </summary>
        /// <param name="handler">Handler.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapPostRoutingRoute(InputAction handler);

        /// <summary>
        /// Map a content route.
        /// </summary>
        /// <param name="path">Route path.</param>
        /// <param name="isDirectory">Flag to indicate if the path is a directory.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <param name="exceptionHandler">Method to invoke when handling exceptions.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapContentRoute(
            string path, 
            bool isDirectory, 
            bool requiresAuthentication = false,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null);

        /// <summary>
        /// Apply a static route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Route path.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="exceptionHandler">Method to invoke when handling exceptions.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapStaticRoute(
            HttpMethod method, 
            string path, 
            InputAction handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            bool requiresAuthentication = false);

        /// <summary>
        /// Apply a parameter route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Route path.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="exceptionHandler">Method to invoke when handling exceptions.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        [Obsolete("Use MapParameterRoute instead.")]
        HostBuilder MapParameteRoute(
            HttpMethod method, 
            string path, 
            InputAction handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            bool requiresAuthentication = false);

        /// <summary>
        /// Apply a parameter route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="path">Route path.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="exceptionHandler">Method to invoke when handling exceptions.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapParameterRoute(
            HttpMethod method, 
            string path, 
            InputAction handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            bool requiresAuthentication = false);

        /// <summary>
        /// Apply a dynamic route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="regex">Regular expression.</param>
        /// <param name="handler">Route handler.</param>
        /// <param name="exceptionHandler">Method to invoke when handling exceptions.</param>
        /// <param name="requiresAuthentication">Flag to indicate whether or not the route requires authentication.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapDynamicRoute(
            HttpMethod method, 
            Regex regex, 
            InputAction handler,
            Func<HttpContextBase, Exception, Task> exceptionHandler = null,
            bool requiresAuthentication = false);
    }
}
