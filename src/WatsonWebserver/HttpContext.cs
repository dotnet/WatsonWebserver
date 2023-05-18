using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Timestamps;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext
    {
        #region Public-Members

        /// <summary>
        /// Time information for start, end, and total runtime.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public HttpRequest Request { get; private set; } = null;

        /// <summary>
        /// Type of route.
        /// </summary>
        [JsonPropertyOrder(0)]
        public RouteTypeEnum? RouteType { get; internal set; } = null;

        /// <summary>
        /// Matched route.
        /// </summary>
        [JsonPropertyOrder(1)]
        public object Route { get; internal set; } = null;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        [JsonPropertyOrder(998)]
        public HttpResponse Response { get; private set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        private readonly ISerializationHelper _Serializer = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        internal HttpContext(
            HttpListenerContext ctx, 
            WatsonWebserverSettings settings, 
            WatsonWebserverEvents events,
            ISerializationHelper serializer)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            
            Request = new HttpRequest(ctx, _Serializer); 
            Response = new HttpResponse(Request, ctx, settings, events, _Serializer); 
        }

        #endregion

        #region Public-Methods
         
        #endregion

        #region Private-Methods

        #endregion
    }
}
