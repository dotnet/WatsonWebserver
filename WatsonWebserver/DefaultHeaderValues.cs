using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Values for commonly-used headers.
    /// </summary>
    public class DefaultHeaderValues
    {
        /// <summary>
        /// Access-Control-Allow-Origin header.
        /// </summary>
        public string AccessControlAllowOrigin = "*";

        /// <summary>
        /// Access-Control-Allow-Methods header.
        /// </summary>
        public string AccessControlAllowMethods = "OPTIONS, HEAD, GET, PUT, POST, DELETE";

        /// <summary>
        /// Access-Control-Allow-Headers header.
        /// </summary>
        public string AccessControlAllowHeaders = "*";

        /// <summary>
        /// Access-Control-Expose-Headers header.
        /// </summary>
        public string AccessControlExposeHeaders = "";

        /// <summary>
        /// Accept header.
        /// </summary>
        public string Accept = "*/*";

        /// <summary>
        /// Accept-Language header.
        /// </summary>
        public string AcceptLanguage = "en-US, en";

        /// <summary>
        /// Accept-Charset header.
        /// </summary>
        public string AcceptCharset = "ISO-8859-1, utf-8";

        /// <summary>
        /// Connection header.
        /// </summary>
        public string Connection = "close";

        /// <summary>
        /// Host header.
        /// </summary>
        public string Host = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public DefaultHeaderValues()
        {

        }
    }
}