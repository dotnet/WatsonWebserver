namespace WatsonWebserver.Core.Health
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of a health check evaluation.
    /// </summary>
    public class HealthCheckResult
    {
        #region Public-Members

        /// <summary>
        /// The health status.
        /// </summary>
        public HealthStatusEnum Status { get; set; } = HealthStatusEnum.Healthy;

        /// <summary>
        /// Optional description providing context about the health status.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// Optional additional data about the health status.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthCheckResult()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
