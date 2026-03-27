namespace WatsonWebserver.Core.Health
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Extension methods for <see cref="WebserverBase"/> to enable health check endpoints.
    /// </summary>
    public static class WebserverHealthExtensions
    {
        /// <summary>
        /// Enable a health check endpoint on the server.
        /// The endpoint returns HTTP 200 for Healthy/Degraded and HTTP 503 for Unhealthy.
        /// </summary>
        /// <param name="server">The webserver instance.</param>
        /// <param name="configure">Optional action to configure the health check settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when server is null.</exception>
        public static void UseHealthCheck(this WebserverBase server, Action<HealthCheckSettings> configure = null)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            HealthCheckSettings settings = new HealthCheckSettings();
            configure?.Invoke(settings);

            Func<ApiRequest, Task<object>> handler = async (ApiRequest req) =>
            {
                HealthCheckResult result;

                if (settings.CustomCheck != null)
                {
                    result = await settings.CustomCheck(req.CancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = new HealthCheckResult { Status = HealthStatusEnum.Healthy };
                }

                if (result.Status == HealthStatusEnum.Unhealthy)
                {
                    req.Http.Response.StatusCode = 503;
                }
                else
                {
                    req.Http.Response.StatusCode = 200;
                }

                return result;
            };

            server.Get(settings.Path, handler, auth: settings.RequireAuthentication);
        }
    }
}
