using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CavemanTcp;
using Timestamps;
using WatsonWebserver.Core;

namespace WatsonWebserver.Lite
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext : HttpContextBase
    {
        #region Public-Members

        #endregion

        #region Private-Members
         
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(
            string ipPort, 
            Stream stream, 
            string requestHeader, 
            WebserverEvents events, 
            WebserverSettings.HeaderSettings headers,
            int streamBufferSize)
        { 
            if (String.IsNullOrEmpty(requestHeader)) throw new ArgumentNullException(nameof(requestHeader));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (streamBufferSize < 1) throw new ArgumentOutOfRangeException(nameof(streamBufferSize));

            Request = new HttpRequest(ipPort, stream, requestHeader);
            Response = new HttpResponse(ipPort, headers, stream, Request, events, streamBufferSize);
        }

        #endregion

        #region Public-Methods

        #endregion
    }
}