namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Text;

    /// <summary>
    /// Response event arguments.
    /// </summary>
    public class ResponseEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// IP address.
        /// </summary>
        public string Ip { get; set; } = null;

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Request query.
        /// </summary>
        public NameValueCollection Query { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection RequestHeaders { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Content length.
        /// </summary>
        public long RequestContentLength { get; set; } = 0;

        /// <summary>
        /// Response status.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Response headers.
        /// </summary>
        public NameValueCollection ResponseHeaders { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response content length.
        /// </summary>
        public long? ResponseContentLength { get; set; } = 0;

        /// <summary>
        /// Total time in processing the request and sending the response, in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ctx">Context.</param>
        /// <param name="totalMs">Total milliseconds.</param>
        public ResponseEventArgs(HttpContextBase ctx, double totalMs)
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
            TotalMs = totalMs;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
