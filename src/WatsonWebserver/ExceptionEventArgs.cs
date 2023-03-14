using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Exception event arguments.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// IP address.
        /// </summary>
        public string Ip { get; private set; } = null;

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; private set; } = 0;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; private set; } = HttpMethod.GET;

        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; private set; } = null;

        /// <summary>
        /// Request query.
        /// </summary>
        public NameValueCollection Query { get; private set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection RequestHeaders { get; private set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Content length.
        /// </summary>
        public long RequestContentLength { get; private set; } = 0;

        /// <summary>
        /// Response status.
        /// </summary>
        public int StatusCode { get; private set; } = 0;

        /// <summary>
        /// Response headers.
        /// </summary>
        public NameValueCollection ResponseHeaders { get; private set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response content length.
        /// </summary>
        public long? ResponseContentLength { get; private set; } = 0;

        /// <summary>
        /// Exception.
        /// </summary>
        public Exception Exception { get; private set; } = null;

        internal ExceptionEventArgs(HttpContext ctx, Exception e)
        {
            if (ctx != null)
            {
                Ip = ctx.Request.Source.IpAddress;
                Port = ctx.Request.Source.Port;
                Method = ctx.Request.Method;
                Url = ctx.Request.Url.Full;
                Query = ctx.Request.Query.Elements;
                RequestHeaders = ctx.Request.Headers;
                RequestContentLength = ctx.Request.ContentLength;
                StatusCode = ctx.Response.StatusCode;
                ResponseContentLength = ctx.Response.ContentLength;
            }

            Exception = e;
        }
    }
}
