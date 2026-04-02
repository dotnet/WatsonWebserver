namespace WatsonWebserver.Core.Settings
{
    using System;

    /// <summary>
    /// HTTP/1.1-specific input-output settings.
    /// </summary>
    public class Http1IOSettings
    {
        /// <summary>
        /// Maximum number of retained pooled objects per HTTP/1.1 pooled type.
        /// A value of zero disables retention.
        /// </summary>
        public int PoolMaxRetainedPerType
        {
            get
            {
                return _PoolMaxRetainedPerType;
            }
            set
            {
                _PoolMaxRetainedPerType = Clamp(value, 0, 4096);
            }
        }

        /// <summary>
        /// Maximum number of cached HTTP/1.1 response-header template entries.
        /// A value of zero disables the cache.
        /// </summary>
        public int ResponseHeaderTemplateCacheSize
        {
            get
            {
                return _ResponseHeaderTemplateCacheSize;
            }
            set
            {
                _ResponseHeaderTemplateCacheSize = Clamp(value, 0, 2048);
            }
        }

        /// <summary>
        /// Maximum number of cached HTTP/1.1 status-line entries.
        /// A value of zero disables the cache.
        /// </summary>
        public int StatusLineCacheSize
        {
            get
            {
                return _StatusLineCacheSize;
            }
            set
            {
                _StatusLineCacheSize = Clamp(value, 0, 256);
            }
        }

        private int _PoolMaxRetainedPerType = 256;
        private int _ResponseHeaderTemplateCacheSize = 256;
        private int _StatusLineCacheSize = 64;

        /// <summary>
        /// Instantiate the HTTP/1.1 input-output settings.
        /// </summary>
        public Http1IOSettings()
        {
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
