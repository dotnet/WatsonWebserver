using System;
using System.Net;
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext
    {
        #region Public-Members

        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        [JsonProperty(Order = -1)]
        public HttpRequest Request { get; private set; } = null;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        public HttpResponse Response { get; private set; } = null;

        #endregion

        #region Private-Members

        private HttpListenerContext _Context = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(HttpListenerContext ctx, WatsonWebserverSettings settings, WatsonWebserverEvents events)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (events == null) throw new ArgumentNullException(nameof(events));
            _Context = ctx;
            Request = new HttpRequest(ctx); 
            Response = new HttpResponse(Request, ctx, settings, events); 
        }

        #endregion

        #region Public-Methods
         
        #endregion

        #region Private-Methods

        #endregion
    }
}
