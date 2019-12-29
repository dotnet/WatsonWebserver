using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext 
    {
        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        public HttpRequest Request;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        public HttpResponse Response;
         
        private HttpListenerContext _Context;
        private EventCallbacks _Events;

        private HttpContext()
        {
        }

        internal HttpContext(HttpListenerContext ctx, EventCallbacks events)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (events == null) throw new ArgumentNullException(nameof(events));

            _Context = ctx;
            _Events = events;

            Request = new HttpRequest(ctx);
            Response = new HttpResponse(Request, _Context, _Events);
        } 
    }
}
