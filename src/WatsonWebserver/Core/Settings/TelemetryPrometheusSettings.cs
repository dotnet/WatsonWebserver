namespace WatsonWebserver.Core.Settings
{
    using System;

    /// <summary>
    /// Settings for the optional in-process Prometheus scrape endpoint.
    /// The endpoint is served on the existing Watson listener as a route at a configurable path, so it
    /// never opens a second TCP port and cannot create a listener port conflict. It is disabled by
    /// default; enable it only when a Prometheus scraper needs an in-process endpoint and no external
    /// collector is present.
    /// </summary>
    public class TelemetryPrometheusSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable the in-process Prometheus scrape endpoint.
        /// Default is false. When enabled, a GET request to <see cref="Path"/> returns the current
        /// metrics in Prometheus exposition format, served on the main listener (no additional port).
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// Path at which the scrape endpoint is served. Default is "/metrics".
        /// Always begins with '/'. Choose a value that does not collide with a registered route.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Path));
                if (!value.StartsWith("/")) value = "/" + value;
                _Path = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Path = "/metrics";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with the endpoint disabled.
        /// </summary>
        public TelemetryPrometheusSettings()
        {
        }

        #endregion
    }
}
