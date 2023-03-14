using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Request event arguments.
    /// </summary>
    public class RequestEventArgs : EventArgs
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
        /// Query found in the URL.
        /// </summary>
        public NameValueCollection Query { get; private set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection Headers { get; private set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Content length.
        /// </summary>
        public long ContentLength { get; private set; } = 0;

        internal RequestEventArgs(HttpContext ctx)
        {
            Ip = ctx.Request.Source.IpAddress;
            Port = ctx.Request.Source.Port;
            Method = ctx.Request.Method;
            Url = ctx.Request.Url.Full;
            Query = ctx.Request.Query.Elements;
            Headers = ctx.Request.Headers;
            ContentLength = ctx.Request.ContentLength;
        }
    }
}
