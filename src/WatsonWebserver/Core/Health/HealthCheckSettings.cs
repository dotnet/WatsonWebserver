namespace WatsonWebserver.Core.Health
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Configuration for the health check endpoint.
    /// </summary>
    public class HealthCheckSettings
    {
        #region Public-Members

        /// <summary>
        /// URL path for the health check endpoint.
        /// Default is /health.
        /// </summary>
        public string Path { get; set; } = "/health";

        /// <summary>
        /// When true, the health check endpoint requires authentication.
        /// Default is false.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;

        /// <summary>
        /// Optional custom health check delegate.
        /// When set, this delegate is called to evaluate the health status.
        /// When null, the endpoint always returns Healthy.
        /// </summary>
        public Func<CancellationToken, Task<HealthCheckResult>> CustomCheck { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthCheckSettings()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
