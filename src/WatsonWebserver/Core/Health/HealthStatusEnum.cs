namespace WatsonWebserver.Core.Health
{
    /// <summary>
    /// Health check status enumeration.
    /// </summary>
    public enum HealthStatusEnum
    {
        /// <summary>
        /// The application is healthy and fully operational. Maps to HTTP 200.
        /// </summary>
        Healthy,

        /// <summary>
        /// The application is operational but experiencing degraded performance or partial issues. Maps to HTTP 200.
        /// </summary>
        Degraded,

        /// <summary>
        /// The application is unhealthy and unable to serve requests properly. Maps to HTTP 503.
        /// </summary>
        Unhealthy
    }
}
