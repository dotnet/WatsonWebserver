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
    public class HttpContext 
    {
        public HttpRequest Request;
        public HttpResponse Response;

        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamBufferSize must be greater than zero bytes.");
                _StreamBufferSize = value;
            }
        }
         
        private int _StreamBufferSize = 65536;
        private HttpListenerContext _Context;
        private EventCallbacks _Events;

        private HttpContext()
        {
        }

        public HttpContext(HttpListenerContext ctx, EventCallbacks events)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (events == null) throw new ArgumentNullException(nameof(events));

            _Context = ctx;
            _Events = events;

            Request = new HttpRequest(ctx);
            Response = new HttpResponse(Request, _Context, _Events, _StreamBufferSize);
        } 
    }
}
