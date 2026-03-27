namespace WatsonWebserver.Core.Settings
{
    using System.Collections.Generic;

    /// <summary>
    /// Header settings.
    /// </summary>
    public class HeaderSettings
    {
        /// <summary>
        /// Automatically set content length if not already set.
        /// </summary>
        public bool IncludeContentLength { get; set; } = true;

        /// <summary>
        /// Headers to add to each request.
        /// </summary>
        public Dictionary<string, string> DefaultHeaders
        {
            get
            {
                return _DefaultHeaders;
            }
            set
            {
                if (value == null) _DefaultHeaders = CreateDefaultHeaders();
                else _DefaultHeaders = value;
            }
        }

        private Dictionary<string, string> _DefaultHeaders = CreateDefaultHeaders();

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public HeaderSettings()
        {
        }

        private static Dictionary<string, string> CreateDefaultHeaders()
        {
            return new Dictionary<string, string>(System.StringComparer.InvariantCultureIgnoreCase)
            {
                { WebserverConstants.HeaderAccessControlAllowOrigin, "*" },
                { WebserverConstants.HeaderAccessControlAllowMethods, "OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH" },
                { WebserverConstants.HeaderAccessControlAllowHeaders, "*" },
                { WebserverConstants.HeaderAccessControlExposeHeaders, "" },
                { WebserverConstants.HeaderAccept, "*/*" },
                { WebserverConstants.HeaderAcceptLanguage, "en-US, en" },
                { WebserverConstants.HeaderAcceptCharset, "ISO-8859-1, utf-8" },
                { WebserverConstants.HeaderCacheControl, "no-cache" },
                { WebserverConstants.HeaderConnection, "close" },
                { WebserverConstants.HeaderHost, "localhost:8000" }
            };
        }
    }
}
