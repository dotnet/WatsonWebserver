namespace WatsonWebserver.Lite
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using CavemanTcp;
    using Timestamps;
    using WatsonWebserver.Core;

    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext : HttpContextBase
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private WebserverSettings _Settings = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(
            WebserverSettings settings,
            WebserverEvents events,
            string sourceIpPort, 
            string destIpPort,
            Stream stream, 
            string requestHeader, 
            int streamBufferSize)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrEmpty(requestHeader)) throw new ArgumentNullException(nameof(requestHeader));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (streamBufferSize < 1) throw new ArgumentOutOfRangeException(nameof(streamBufferSize));

            _Settings = settings;

            Request = new HttpRequest(_Settings, sourceIpPort, destIpPort, stream, requestHeader);
            Response = new HttpResponse(sourceIpPort, _Settings.Headers, stream, Request, events, streamBufferSize);
        }

        #endregion

        #region Public-Methods

        #endregion
    }
}