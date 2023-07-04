using System.Text.RegularExpressions;

namespace WatsonWebserver.Extensions.HostBuilderExtension
{
    /// <summary>
    /// Host builder interface.
    /// </summary>
    /// <typeparam name="HostBuilder">Host builder.</typeparam>
    /// <typeparam name="InputAction">Input action.</typeparam>
    public interface IHostBuilder<HostBuilder, InputAction>
    {
        /// <summary>
        /// Apply a static route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="routePath">Route path.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapStaticRoute(HttpMethod method, InputAction action, string routePath = "/");

        /// <summary>
        /// Apply a parameter route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="routePath">Route path.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapParameteRoute(HttpMethod method, InputAction action, string routePath = "/");

        /// <summary>
        /// Apply a dynamic route.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="action">Action.</param>
        /// <param name="regex">Regular expression.</param>
        /// <returns>Host builder.</returns>
        HostBuilder MapDynamicRoute(HttpMethod method, InputAction action, Regex regex);
    }
}
