namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Request timeout settings for API route handlers.
    /// When enabled, requests that exceed the timeout duration receive a 408 Request Timeout response.
    /// </summary>
    public class TimeoutSettings
    {
        #region Public-Members

        /// <summary>
        /// Default timeout for API route requests.
        /// Set to TimeSpan.Zero (the default) to disable timeouts.
        /// When greater than zero, a cancellation token is provided to route handlers that will be
        /// cancelled when the timeout expires.
        /// </summary>
        public TimeSpan DefaultTimeout
        {
            get
            {
                return _DefaultTimeout;
            }
            set
            {
                if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(DefaultTimeout), "Timeout must be zero or positive.");
                _DefaultTimeout = value;
            }
        }

        #endregion

        #region Private-Members

        private TimeSpan _DefaultTimeout = TimeSpan.Zero;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with timeouts disabled.
        /// </summary>
        public TimeoutSettings()
        {
        }

        /// <summary>
        /// Instantiate with a specified default timeout.
        /// </summary>
        /// <param name="defaultTimeout">The default timeout duration.</param>
        public TimeoutSettings(TimeSpan defaultTimeout)
        {
            DefaultTimeout = defaultTimeout;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
