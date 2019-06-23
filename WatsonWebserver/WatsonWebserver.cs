using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using IpMatcher;

namespace WatsonWebserver
{
    /// <summary>
    /// Watson webserver.
    /// </summary>
    public class Server : IDisposable
    {
        #region Public-Members
        
        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (_HttpListener != null) ? _HttpListener.IsListening : false;
            } 
        }

        /// <summary>
        /// Function to call when an OPTIONS request is received.  Often used to handle CORS.  Leave as 'null' to use the default OPTIONS handler.
        /// </summary>
        public Func<HttpRequest, HttpResponse> OptionsRoute = null;

        /// <summary>
        /// Indicate whether or not Watson should fully read the input stream and populate HttpRequest.Data.
        /// Otherwise, the request body will be available by reading HttpRequest.DataStream.
        /// </summary>
        public bool ReadInputStream = true;

        /// <summary>
        /// Indicate the buffer size to use when reading from a stream to send data to a requestor.
        /// </summary>
        public int StreamReadBufferSize
        {
            get
            {
                return _StreamReadBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentException("StreamReadBufferSize must be greater than zero.");
                _StreamReadBufferSize = value;
            }
        }
         
        /// <summary>
        /// Dynamic routes; i.e. routes with regex matching and any HTTP method.
        /// </summary>
        public DynamicRouteManager DynamicRoutes;

        /// <summary>
        /// Static routes; i.e. routes with explicit matching and any HTTP method.
        /// </summary>
        public StaticRouteManager StaticRoutes;

        /// <summary>
        /// Content routes; i.e. routes to specific files or folders for GET and HEAD requests.
        /// </summary>
        public ContentRouteManager ContentRoutes;

        /// <summary>
        /// Access control manager, i.e. default mode of operation, white list, and black list.
        /// </summary>
        public AccessControlManager AccessControl;

        #endregion

        #region Private-Members

        private readonly EventWaitHandle _Terminator = new EventWaitHandle(false, EventResetMode.ManualReset);

        private HttpListener _HttpListener;
        private List<string> _ListenerHostnames;
        private int _ListenerPort;
        private bool _ListenerSsl;
        private int _StreamReadBufferSize = 65536; 

        private ContentRouteProcessor _ContentRouteProcessor;
        private Func<HttpRequest, HttpResponse> _DefaultRoute;
        
        private CancellationTokenSource _TokenSource;
        private CancellationToken _Token;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new instance of the Watson Webserver.
        /// </summary>
        /// <param name="hostname">Hostname or IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRequestHandler">Method used when a request is received and no routes are defined.  Commonly used as the 404 handler when routes are used.</param>
        public Server(string hostname, int port, bool ssl, Func<HttpRequest, HttpResponse> defaultRequestHandler)
        {
            if (String.IsNullOrEmpty(hostname)) hostname = "*";
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));
            if (defaultRequestHandler == null) throw new ArgumentNullException(nameof(defaultRequestHandler));

            _HttpListener = new HttpListener();

            _ListenerHostnames = new List<string>();
            _ListenerHostnames.Add(hostname); 
            _ListenerPort = port;
            _ListenerSsl = ssl; 
            _DefaultRoute = defaultRequestHandler;

            InitializeRouteManagers();
            AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
            Welcome();

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;
            Task.Run(() => StartServer(_Token), _Token);
        }
         
        /// <summary>
        /// Creates a new instance of the Watson Webserver.
        /// </summary>
        /// <param name="hostnames">Hostnames or IP addresses on which to listen.  Note: multiple listener endpoints is not supported on all platforms.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="defaultRequestHandler">Method used when a request is received and no routes are defined.  Commonly used as the 404 handler when routes are used.</param>
        public Server(List<string> hostnames, int port, bool ssl, Func<HttpRequest, HttpResponse> defaultRequestHandler)
        {
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));
            if (defaultRequestHandler == null) throw new ArgumentNullException(nameof(defaultRequestHandler));

            _HttpListener = new HttpListener();

            _ListenerHostnames = new List<string>();
            if (hostnames == null || hostnames.Count < 1)
            {
                _ListenerHostnames.Add("*");
            }
            else
            {
                foreach (string curr in hostnames)
                {
                    _ListenerHostnames.Add(curr);
                }
            }
            
            _ListenerPort = port;
            _ListenerSsl = ssl;
            _DefaultRoute = defaultRequestHandler;

            InitializeRouteManagers();
            AccessControl = new AccessControlManager(AccessControlMode.DefaultPermit);
            Welcome();

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;
            Task.Run(() => StartServer(_Token), _Token);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
         
        #endregion

        #region Private-Methods

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_HttpListener != null)
                {
                    if (_HttpListener.IsListening) _HttpListener.Stop();
                    _HttpListener.Close();
                }
                
                _TokenSource.Cancel();
            }
        }

        private void InitializeRouteManagers()
        {
            DynamicRoutes = new DynamicRouteManager();
            StaticRoutes = new StaticRouteManager();
            ContentRoutes = new ContentRouteManager();

            _ContentRouteProcessor = new ContentRouteProcessor(ContentRoutes);
            OptionsRoute = null;
        }

        private void Welcome()
        {
            Console.Write("Starting Watson Webserver on: ");
            foreach (string curr in _ListenerHostnames)
            {
                if (_ListenerSsl) Console.Write("https://" + curr + ":" + _ListenerPort + " ");
                else Console.Write("http://" + curr + ":" + _ListenerPort + " ");
            }
            Console.WriteLine("");
        }

        private void StartServer(CancellationToken token)
        {
            Task.Run(() => AcceptConnections(token), token);
            _Terminator.WaitOne();
        }

        private void AcceptConnections(CancellationToken token)
        {
            try
            {
                foreach (string curr in _ListenerHostnames)
                {
                    string prefix = null;
                    if (_ListenerSsl) prefix = "https://" + curr + ":" + _ListenerPort + "/";
                    else prefix = "http://" + curr + ":" + _ListenerPort + "/";
                    _HttpListener.Prefixes.Add(prefix);
                }

                _HttpListener.Start();
                
                while (_HttpListener.IsListening)
                {
                    ThreadPool.QueueUserWorkItem((c) =>
                    { 
                        if (token.IsCancellationRequested) throw new OperationCanceledException();

                        var context = c as HttpListenerContext;
                         
                        try
                        {
                            // Populate HTTP request object
                            HttpRequest req = new HttpRequest(context, ReadInputStream);
                            if (req == null)
                            {
                                HttpResponse resp = new HttpResponse(req, 500, null, "text/plain", "Unable to parse HTTP request");
                                SendResponse(context, req, resp);
                                return;
                            }

                            // Check access control
                            if (!AccessControl.Permit(req.SourceIp))
                            { 
                                context.Response.Close();
                                return;
                            } 

                            // Process OPTIONS request
                            if (req.Method == HttpMethod.OPTIONS
                                && OptionsRoute != null)
                            { 
                                OptionsProcessor(context, req);
                                return;
                            }

                            // Send to handler
                            Task.Run(() =>
                            {
                                HttpResponse resp = null;
                                Func<HttpRequest, HttpResponse> handler = null;

                                // Check content routes
                                if (req.Method == HttpMethod.GET
                                    || req.Method == HttpMethod.HEAD)
                                { 
                                    if (ContentRoutes.Exists(req.RawUrlWithoutQuery))
                                        resp = _ContentRouteProcessor.Process(req);
                                }

                                // Check static routes
                                if (resp == null)
                                {
                                    handler = StaticRoutes.Match(req.Method, req.RawUrlWithoutQuery);
                                    if (handler != null) 
                                        resp = handler(req); 
                                    else
                                    {
                                        // Check dynamic routes
                                        handler = DynamicRoutes.Match(req.Method, req.RawUrlWithoutQuery);
                                        if (handler != null)
                                            resp = handler(req);
                                        else
                                        {
                                            // Use default route
                                            resp = DefaultRouteProcessor(context, req);
                                        }
                                    }
                                }

                                // Return
                                if (resp == null)
                                {
                                    resp = new HttpResponse(req, 500, null, "text/plain", "Unable to generate repsonse");
                                    SendResponse(context, req, resp);
                                    return;
                                }
                                else
                                { 
                                    Dictionary<string, string> headers = new Dictionary<string, string>();
                                    if (!String.IsNullOrEmpty(resp.ContentType)) 
                                        headers.Add("content-type", resp.ContentType); 

                                    if (resp.Headers != null && resp.Headers.Count > 0)
                                    {
                                        foreach (KeyValuePair<string, string> curr in resp.Headers) 
                                            headers = Common.AddToDict(curr.Key, curr.Value, headers); 
                                    }
                                    
                                    SendResponse(context, req, resp);

                                    return;
                                } 
                            }); 
                        }
                        catch (Exception)
                        {

                        }
                        finally
                        {

                        }
                    }, _HttpListener.GetContext());
                }
            } 
            catch (OperationCanceledException)
            {
                // do nothing
            }
            catch (Exception)
            { 
                throw;
            }
            finally
            { 
            }
        }

        private HttpResponse DefaultRouteProcessor(HttpListenerContext context, HttpRequest request)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (request == null) throw new ArgumentNullException(nameof(request));
            HttpResponse ret = _DefaultRoute(request);
            if (ret == null)
            { 
                ret = new HttpResponse(request, 500, null, "application/json", Encoding.UTF8.GetBytes("Unable to generate response"));
                return ret;
            }

            return ret;
        }
         
        private void SendResponse(
            HttpListenerContext context,
            HttpRequest req,
            HttpResponse resp)
        {
            long responseLength = 0;
            HttpListenerResponse response = null;

            try
            { 
                #region Status-Code-and-Description

                response = context.Response;
                response.StatusCode = resp.StatusCode;

                switch (resp.StatusCode)
                {
                    case 200:
                        response.StatusDescription = "OK";
                        break;

                    case 201:
                        response.StatusDescription = "Created";
                        break;

                    case 301:
                        response.StatusDescription = "Moved Permanently";
                        break;

                    case 302:
                        response.StatusDescription = "Moved Temporarily";
                        break;

                    case 304:
                        response.StatusDescription = "Not Modified";
                        break;

                    case 400:
                        response.StatusDescription = "Bad Request";
                        break;

                    case 401:
                        response.StatusDescription = "Unauthorized";
                        break;

                    case 403:
                        response.StatusDescription = "Forbidden";
                        break;

                    case 404:
                        response.StatusDescription = "Not Found";
                        break;

                    case 405:
                        response.StatusDescription = "Method Not Allowed";
                        break;

                    case 429:
                        response.StatusDescription = "Too Many Requests";
                        break;

                    case 500:
                        response.StatusDescription = "Internal Server Error";
                        break;

                    case 501:
                        response.StatusDescription = "Not Implemented";
                        break;

                    case 503:
                        response.StatusDescription = "Service Unavailable";
                        break;

                    default:
                        response.StatusDescription = "Unknown Status";
                        break;
                }

                #endregion

                #region Response-Headers

                response.AddHeader("Access-Control-Allow-Origin", "*");
                response.ContentType = req.ContentType;
                  
                if (resp.Headers != null && resp.Headers.Count > 0)
                {
                    foreach (KeyValuePair<string, string> curr in resp.Headers)
                    {
                        response.AddHeader(curr.Key, curr.Value);
                    }
                }

                #endregion

                #region Handle-HEAD-Request

                if (req.Method == HttpMethod.HEAD)
                {
                    resp.Data = null;
                    resp.DataStream = null; 
                }

                #endregion

                #region Send-Response
                
                Stream output = response.OutputStream;
                response.ContentLength64 = resp.ContentLength;

                try
                {
                    if (resp.Data != null && resp.Data.Length > 0)
                    { 
                        responseLength = resp.Data.Length;
                        response.ContentLength64 = responseLength;
                        output.Write(resp.Data, 0, (int)responseLength);
                    }
                    else if (resp.DataStream != null && resp.ContentLength > 0)
                    {  
                        responseLength = resp.ContentLength; 
                        response.ContentLength64 = resp.ContentLength;  
                         
                        long bytesRemaining = resp.ContentLength;

                        while (bytesRemaining > 0)
                        { 
                            int bytesRead = 0;
                            byte[] buffer = new byte[StreamReadBufferSize];

                            if (bytesRemaining >= StreamReadBufferSize) bytesRead = resp.DataStream.Read(buffer, 0, StreamReadBufferSize); 
                            else bytesRead = resp.DataStream.Read(buffer, 0, (int)bytesRemaining); 

                            output.Write(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }

                        resp.DataStream.Close();
                        resp.DataStream.Dispose();
                    } 
                }
                catch (Exception)
                {
                    // Console.WriteLine("Outer exception");
                    // Console.WriteLine(WatsonCommon.SerializeJson(eInner)); 
                }
                finally
                {
                    output.Flush();
                    output.Close();

                    if (response != null) response.Close(); 
                }

                #endregion

                return;
            } 
            catch (Exception)
            {
                // Console.WriteLine("Outer exception");
                // Console.WriteLine(WatsonCommon.SerializeJson(eOuter));
                return;
            }
            finally
            { 
                if (response != null)
                {
                    response.Close();
                }
            }
        }
        
        private void OptionsProcessor(HttpListenerContext context, HttpRequest req)
        { 
            HttpListenerResponse response = context.Response;
            response.StatusCode = 200;

            string[] requestedHeaders = null;
            if (req.Headers != null)
            {
                foreach (KeyValuePair<string, string> curr in req.Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.IsNullOrEmpty(curr.Value)) continue;
                    if (String.Compare(curr.Key.ToLower(), "access-control-request-headers") == 0)
                    {
                        requestedHeaders = curr.Value.Split(',');
                        break;
                    }
                }
            }

            string headers = "";

            if (requestedHeaders != null)
            {
                int addedCount = 0;
                foreach (string curr in requestedHeaders)
                {
                    if (String.IsNullOrEmpty(curr)) continue;
                    if (addedCount > 0) headers += ", ";
                    headers += ", " + curr;
                    addedCount++;
                }
            }

            string listenerPrefix = null;
            if (_ListenerSsl) listenerPrefix = "https://" + req.DestHostname + ":" + req.DestPort + "/";
            else listenerPrefix = "http://" + req.DestHostname + ":" + req.DestPort + "/";

            response.AddHeader("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            response.AddHeader("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            response.AddHeader("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Accept", "*/*");
            response.AddHeader("Accept-Language", "en-US, en");
            response.AddHeader("Accept-Charset", "ISO-8859-1, utf-8");
            response.AddHeader("Connection", "keep-alive");
            response.AddHeader("Host", listenerPrefix);
            response.ContentLength64 = 0;
            response.Close(); 
            return;
        }

        #endregion
    }
}
