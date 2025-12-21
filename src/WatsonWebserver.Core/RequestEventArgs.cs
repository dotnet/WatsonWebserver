namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Text;

    /// <summary>
    /// Request event arguments.
    /// </summary>
    public class RequestEventArgs : EventArgs
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
        /// Query found in the URL.
        /// </summary>
        public NameValueCollection Query { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection Headers { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Content length.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ctx"></param>
        public RequestEventArgs(HttpContextBase ctx)
        {
            Ip = ctx.Request.Source.IpAddress;
            Port = ctx.Request.Source.Port;
            Method = ctx.Request.Method;
            Url = ctx.Request.Url.Full;
            Query = ctx.Request.Query.Elements;
            Headers = ctx.Request.Headers;
            ContentLength = ctx.Request.ContentLength;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
