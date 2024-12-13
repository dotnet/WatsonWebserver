namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Webserver constants.
    /// </summary>
    public static class WebserverConstants
    {
        #region Public-Members

        /// <summary>
        /// Content type text.
        /// </summary>
        public static string ContentTypeText { get; set; } = "text/plain";

        /// <summary>
        /// Content type HTML.
        /// </summary>
        public static string ContentTypeHtml { get; set; } = "text/html";

        /// <summary>
        /// Content type JSON.
        /// </summary>
        public static string ContentTypeJson { get; set; } = "application/json";

        /// <summary>
        /// Content type XML.
        /// </summary>
        public static string ContentTypeXml { get; set; } = "application/xml";

        /// <summary>
        /// HTML content for a 400 response.
        /// </summary>
        public static string PageContent400 { get; set; } =
            "<html>" + Environment.NewLine +
            "  <head>" + Environment.NewLine +
            "    <title>We don't understand what you're saying</title>" + Environment.NewLine +
            "  </head>" + Environment.NewLine +
            "  <body>" + Environment.NewLine +
            "    <h2>Bad request</h2>" + Environment.NewLine +
            "    <p>We don't know how to process the request you sent us." + Environment.NewLine +
            "  </body>" + Environment.NewLine +
            "<html>" + Environment.NewLine;

        /// <summary>
        /// HTML content for a 404 response.
        /// </summary>
        public static string PageContent404 { get; set; } =
            "<html>" + Environment.NewLine +
            "  <head>" + Environment.NewLine +
            "    <title>Not found</title>" + Environment.NewLine +
            "  </head>" + Environment.NewLine +
            "  <body>" + Environment.NewLine +
            "    <h2>Not found</h2>" + Environment.NewLine +
            "    <p>We're sorry, though you may seek, you shall not find here.</p>" + Environment.NewLine +
            "  </body>" + Environment.NewLine +
            "<html>" + Environment.NewLine;

        /// <summary>
        /// HTML content for a 500 response.
        /// </summary>
        public static string PageContent500 { get; set; } =
            "<html>" + Environment.NewLine +
            "  <head>" + Environment.NewLine +
            "    <title>It's me, not you</title>" + Environment.NewLine +
            "  </head>" + Environment.NewLine +
            "  <body>" + Environment.NewLine +
            "    <h2>Internal server error</h2>" + Environment.NewLine +
            "    <p>There's a problem here, but it's on me, not you.</p>" + Environment.NewLine +
            "  </body>" + Environment.NewLine +
            "<html>" + Environment.NewLine;

        /// <summary>
        /// Header for access-control-allow-origin.
        /// </summary>
        public static string HeaderAccessControlAllowOrigin { get; set; } = "Access-Control-Allow-Origin";

        /// <summary>
        /// Header for access-control-allow-methods.
        /// </summary>
        public static string HeaderAccessControlAllowMethods { get; set; } = "Access-Control-Allow-Methods";

        /// <summary>
        /// Header for access-control-allow-headers.
        /// </summary>
        public static string HeaderAccessControlAllowHeaders { get; set; } = "Access-Control-Allow-Headers";

        /// <summary>
        /// Header for access-control-expose-headers.
        /// </summary>
        public static string HeaderAccessControlExposeHeaders { get; set; } = "Access-Control-Expose-Headers";

        /// <summary>
        /// Header for accept.
        /// </summary>
        public static string HeaderAccept { get; set; } = "Accept";

        /// <summary>
        /// Header for accept-language.
        /// </summary>
        public static string HeaderAcceptLanguage { get; set; } = "Accept-Language";

        /// <summary>
        /// Header for accept-charset.
        /// </summary>
        public static string HeaderAcceptCharset { get; set; } = "Accept-Charset";

        /// <summary>
        /// Header for cache control.
        /// </summary>
        public static string HeaderCacheControl { get; set; } = "Cache-Control";

        /// <summary>
        /// Header for connection.
        /// </summary>
        public static string HeaderConnection { get; set; } = "Connection";

        /// <summary>
        /// Header for content length.
        /// </summary>
        public static string HeaderContentLength { get; set; } = "Content-Length";

        /// <summary>
        /// Header for content type.
        /// </summary>
        public static string HeaderContentType { get; set; } = "Content-Type";

        /// <summary>
        /// Header for date.
        /// </summary>
        public static string HeaderDate { get; set; } = "Date";

        /// <summary>
        /// DateTime format for date header.
        /// </summary>
        public static string HeaderDateValueFormat { get; set; } = "ddd, dd MMM yyy HH:mm:ss 'GMT'";

        /// <summary>
        /// Header for host.
        /// </summary>
        public static string HeaderHost { get; set; } = "Host";

        /// <summary>
        /// Header for transfer encoding.
        /// </summary>
        public static string HeaderTransferEncoding { get; set; } = "Transfer-Encoding";

        #endregion

        #region Private-Members

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
