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
            get { return (_HttpListener != null) ? _HttpListener.IsListening : false; }
        }

        /// <summary>
        /// Function to call when an OPTIONS request is received.  Often used to handle CORS.  Leave as 'null' to use the default OPTIONS handler.
        /// </summary>
        public Func<HttpRequest, HttpResponse> OptionsRoute = null;

        /// <summary>
        /// Enable or disable console debugging.
        /// </summary>
        public bool Debug
        {
            get
            {
                return _Debug;
            }
            set
            {
                _Debug = value;
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
        private bool _Debug;
        private LoggingManager _Logging; 

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
            _Debug = false;
            _Logging = new LoggingManager(_Debug); 
            _DefaultRoute = defaultRequestHandler;

            InitializeRouteManagers(_Debug);
            AccessControl = new AccessControlManager(_Logging, _Debug, AccessControlMode.DefaultPermit);
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
            _Debug = false;
            _Logging = new LoggingManager(_Debug); 
            _DefaultRoute = defaultRequestHandler;

            InitializeRouteManagers(_Debug);
            AccessControl = new AccessControlManager(_Logging, _Debug, AccessControlMode.DefaultPermit);
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

        private void InitializeRouteManagers(bool debug)
        {
            DynamicRoutes = new DynamicRouteManager(_Logging, debug);
            StaticRoutes = new StaticRouteManager(_Logging, debug);
            ContentRoutes = new ContentRouteManager(_Logging, debug);

            _ContentRouteProcessor = new ContentRouteProcessor(_Logging, debug, ContentRoutes);
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
                            #region Populate-Http-Request-Object

                            HttpRequest currRequest = new HttpRequest(context);
                            if (currRequest == null)
                            {
                                _Logging.Log("Unable to populate HTTP request object on thread ID " + Thread.CurrentThread.ManagedThreadId + ", returning 400");
                                SendResponse(
                                    context,
                                    currRequest,
                                    BuildErrorResponse(500, "Unable to parse your request.", null),
                                    WatsonCommon.AddToDict("content-type", "application/json", null),
                                    400);
                                return;
                            }

                            #endregion

                            #region Access-Control

                            if (!AccessControl.Permit(currRequest.SourceIp))
                            {
                                _Logging.Log("Thread " + currRequest.ThreadId + " " + currRequest.SourceIp + ":" + currRequest.SourcePort + " " + currRequest.Method + " " + currRequest.RawUrlWithoutQuery + " denied");
                                context.Response.Close();
                                return;
                            }
                            else
                            {
                                _Logging.Log("Thread " + currRequest.ThreadId + " " + currRequest.SourceIp + ":" + currRequest.SourcePort + " " + currRequest.Method + " " + currRequest.RawUrlWithoutQuery);
                            }

                            #endregion

                            #region Process-OPTIONS-Request

                            if (currRequest.Method == HttpMethod.OPTIONS
                                && OptionsRoute != null)
                            {
                                _Logging.Log("Thread " + Thread.CurrentThread.ManagedThreadId + " OPTIONS request received");
                                OptionsProcessor(context, currRequest);
                                return;
                            }

                            #endregion

                            #region Send-to-Handler

                            if (_Debug) _Logging.Log(currRequest.ToString());

                            Task.Run(() =>
                            {
                                HttpResponse currResponse = null;
                                Func<HttpRequest, HttpResponse> handler = null;

                                #region Find-Route
                                
                                if (currRequest.Method == HttpMethod.GET
                                    || currRequest.Method == HttpMethod.HEAD)
                                { 
                                    if (ContentRoutes.Exists(currRequest.RawUrlWithoutQuery))
                                    {
                                        // content route found
                                        currResponse = _ContentRouteProcessor.Process(currRequest);
                                    }
                                }

                                if (currResponse == null)
                                {
                                    handler = StaticRoutes.Match(currRequest.Method, currRequest.RawUrlWithoutQuery);
                                    if (handler != null)
                                    {
                                        // static route found
                                        currResponse = handler(currRequest);
                                    }
                                    else
                                    {
                                        // no static route, check for dynamic route
                                        handler = DynamicRoutes.Match(currRequest.Method, currRequest.RawUrlWithoutQuery);
                                        if (handler != null)
                                        {
                                            // dynamic route found
                                            currResponse = handler(currRequest);
                                        }
                                        else
                                        {
                                            // process using default route
                                            currResponse = DefaultRouteProcessor(context, currRequest);
                                        }
                                    }
                                }

                                #endregion

                                #region Return

                                if (currResponse == null)
                                {
                                    _Logging.Log("Null response from handler for " + currRequest.SourceIp + ":" + currRequest.SourcePort + " " + currRequest.Method + " " + currRequest.RawUrlWithoutQuery);
                                    SendResponse(
                                        context,
                                        currRequest,
                                        BuildErrorResponse(500, "Unable to generate response", null),
                                        WatsonCommon.AddToDict("content-type", "application/json", null),
                                        500);
                                    return;
                                }
                                else
                                {
                                    if (_Debug) _Logging.Log(currResponse.ToString());

                                    Dictionary<string, string> headers = new Dictionary<string, string>();
                                    if (!String.IsNullOrEmpty(currResponse.ContentType))
                                    {
                                        headers.Add("content-type", currResponse.ContentType);
                                    }

                                    if (currResponse.Headers != null && currResponse.Headers.Count > 0)
                                    {
                                        foreach (KeyValuePair<string, string> curr in currResponse.Headers)
                                        {
                                            headers = WatsonCommon.AddToDict(curr.Key, curr.Value, headers);
                                        }
                                    }

                                    if (currResponse.RawResponse)
                                    {
                                        SendResponse(
                                            context,
                                            currRequest,
                                            currResponse.Data,
                                            headers,
                                            currResponse.StatusCode);
                                        return;
                                    }
                                    else
                                    {
                                        SendResponse(
                                            context,
                                            currRequest,
                                            currResponse.ToJsonBytes(),
                                            headers,
                                            currResponse.StatusCode);
                                        return;
                                    }
                                }

                                #endregion
                            });

                            #endregion
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                        finally
                        {

                        }
                    }, _HttpListener.GetContext());
                }
            } 
            catch (Exception eOuter)
            {
                _Logging.LogException("AcceptConnections", eOuter);
                throw;
            }
            finally
            {
                _Logging.Log("Exiting");
            }
        }

        private HttpResponse DefaultRouteProcessor(HttpListenerContext context, HttpRequest request)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (request == null) throw new ArgumentNullException(nameof(request));
            HttpResponse ret = _DefaultRoute(request);
            if (ret == null)
            {
                _Logging.Log("Null HttpResponse received from call to RequestReceived, sending 500");
                ret = new HttpResponse(request, false, 500, null, "application/json", "Unable to generate response", false);
                return ret;
            }

            return ret;
        }

        private byte[] BuildErrorResponse(
            int status,
            string text,
            byte[] data)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            ret.Add("data", data);
            ret.Add("success", false);
            ret.Add("http_status", status);

            switch (status)
            {
                case 200:
                    ret.Add("http_text", "OK");
                    break;

                case 201:
                    ret.Add("http_text", "Created");
                    break;

                case 301:
                    ret.Add("http_text", "Moved Permanently");
                    break;

                case 302:
                    ret.Add("http_text", "Moved Temporarily");
                    break;

                case 304:
                    ret.Add("http_text", "Not Modified");
                    break;

                case 400:
                    ret.Add("http_text", "Bad Request");
                    break;

                case 401:
                    ret.Add("http_text", "Unauthorized");
                    break;

                case 403:
                    ret.Add("http_text", "Forbidden");
                    break;

                case 404:
                    ret.Add("http_text", "Not Found");
                    break;

                case 405:
                    ret.Add("http_text", "Method Not Allowed");
                    break;

                case 429:
                    ret.Add("http_text", "Too Many Requests");
                    break;

                case 500:
                    ret.Add("http_text", "Internal Server Error");
                    break;

                case 501:
                    ret.Add("http_text", "Not Implemented");
                    break;

                case 503:
                    ret.Add("http_text", "Service Unavailable");
                    break;

                default:
                    ret.Add("http_text", "Unknown Status");
                    break;
            }

            ret.Add("text", text);
            string json = WatsonCommon.SerializeJson(ret);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] BuildSuccessResponse(
            int status,
            object data)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            ret.Add("data", data);
            ret.Add("success", true);
            ret.Add("http_status", status);

            switch (status)
            {
                // see https://en.wikipedia.org/wiki/List_of_HTTP_status_codes

                #region Official

                case 100:
                    ret.Add("http_text", "Continue");
                    break;

                case 101:
                    ret.Add("http_text", "Switching Protocols");
                    break;

                case 102:
                    ret.Add("http_text", "Processing");
                    break;

                case 200:
                    ret.Add("http_text", "OK");
                    break;

                case 201:
                    ret.Add("http_text", "Created");
                    break;

                case 202:
                    ret.Add("http_text", "Accepted");
                    break;

                case 203:
                    ret.Add("http_text", "Non-Authoritative Information");
                    break;

                case 204:
                    ret.Add("http_text", "No Content");
                    break;

                case 205:
                    ret.Add("http_text", "Reset Content");
                    break;

                case 206:
                    ret.Add("http_text", "Partial Content");
                    break;

                case 207:
                    ret.Add("http_text", "Multi-Status");
                    break;

                case 208:
                    ret.Add("http_text", "Already Reported");
                    break;

                case 226:
                    ret.Add("http_text", "IM Used");
                    break;

                case 300:
                    ret.Add("http_text", "Multiple Choices");
                    break;

                case 301:
                    ret.Add("http_text", "Moved Permanently");
                    break;

                case 302:
                    ret.Add("http_text", "Moved Temporarily");
                    break;

                case 303:
                    ret.Add("http_text", "See Other");
                    break;

                case 304:
                    ret.Add("http_text", "Not Modified");
                    break;

                case 305:
                    ret.Add("http_text", "Use Proxy");
                    break;

                case 306:
                    ret.Add("http_text", "Switch Proxy");
                    break;

                case 307:
                    ret.Add("http_text", "Temporary Redirect");
                    break;

                case 308:
                    ret.Add("http_text", "Permanent Redirect");
                    break;

                case 400:
                    ret.Add("http_text", "Bad Request");
                    break;

                case 401:
                    ret.Add("http_text", "Unauthorized");
                    break;

                case 402:
                    ret.Add("http_text", "Payment Required");
                    break;

                case 403:
                    ret.Add("http_text", "Forbidden");
                    break;

                case 404:
                    ret.Add("http_text", "Not Found");
                    break;

                case 405:
                    ret.Add("http_text", "Method Not Allowed");
                    break;

                case 406:
                    ret.Add("http_text", "Not Acceptable");
                    break;

                case 407:
                    ret.Add("http_text", "Proxy Authentication Required");
                    break;

                case 408:
                    ret.Add("http_text", "Request Timeout");
                    break;

                case 409:
                    ret.Add("http_text", "Conflict");
                    break;

                case 410:
                    ret.Add("http_text", "Gone");
                    break;

                case 411:
                    ret.Add("http_text", "Length Required");
                    break;

                case 412:
                    ret.Add("http_text", "Precondition Failed");
                    break;

                case 413:
                    ret.Add("http_text", "Payload Too Large");
                    break;

                case 414:
                    ret.Add("http_text", "URI Too Long");
                    break;

                case 415:
                    ret.Add("http_text", "Unsupported Media Type");
                    break;

                case 416:
                    ret.Add("http_text", "Range Not Satisfiable");
                    break;

                case 417:
                    ret.Add("http_text", "Expectation Failed");
                    break;

                case 418:
                    ret.Add("http_text", "I'm a Teapot :)");
                    break;

                case 421:
                    ret.Add("http_text", "Misdirected Request");
                    break;

                case 422:
                    ret.Add("http_text", "Unprocessable Entity");
                    break;

                case 423:
                    ret.Add("http_text", "Locked");
                    break;

                case 424:
                    ret.Add("http_text", "Failed Dependency");
                    break;

                case 426:
                    ret.Add("http_text", "Upgrade Required");
                    break;

                case 428:
                    ret.Add("http_text", "Precondition Required");
                    break;

                case 429:
                    ret.Add("http_text", "Too Many Requests");
                    break;

                case 431:
                    ret.Add("http_text", "Request Header Fields Too Large");
                    break;

                case 451:
                    ret.Add("http_text", "Unavailable for Legal Reasons");
                    break;

                case 500:
                    ret.Add("http_text", "Internal Server Error");
                    break;

                case 501:
                    ret.Add("http_text", "Not Implemented");
                    break;

                case 502:
                    ret.Add("http_text", "Bad Gateway");
                    break;

                case 503:
                    ret.Add("http_text", "Service Unavailable");
                    break;

                case 504:
                    ret.Add("http_text", "Gateway Timeout");
                    break;

                case 505:
                    ret.Add("http_text", "HTTP Version Not Supported");
                    break;

                case 506:
                    ret.Add("http_text", "Variant Also Negotiates");
                    break;

                case 507:
                    ret.Add("http_text", "Insufficient Storage");
                    break;

                case 508:
                    ret.Add("http_text", "Loop Detected");
                    break;

                case 510:
                    ret.Add("http_text", "Not Extended");
                    break;

                case 511:
                    ret.Add("http_text", "Network Authentication Required");
                    break;

                #endregion

                #region Unofficial

                case 103:
                    ret.Add("http_text", "Checkpoint or Early Hints");
                    break;

                case 420:
                    ret.Add("http_text", "Method Failure or Enhance Your Calm");
                    break;

                case 450:
                    ret.Add("http_text", "Blocked By Parental Controls");
                    break;

                case 498:
                    ret.Add("http_text", "Invalid Token");
                    break;

                case 499:
                    ret.Add("http_text", "Token Required or Client Closed Request");
                    break;

                case 509:
                    ret.Add("http_text", "Bandwidth Limit Exceeded");
                    break;

                case 530:
                    ret.Add("http_text", "Site Is Frozen");
                    break;

                case 598:
                    ret.Add("http_text", "Network Read Timeout Error");
                    break;

                case 599:
                    ret.Add("http_text", "Network Connect Timeout Error");
                    break;

                #endregion

                #region IIS

                case 440:
                    ret.Add("http_text", "Login Timeout");
                    break;

                case 449:
                    ret.Add("http_text", "Retry With");
                    break;

                #endregion

                #region nginx

                case 444:
                    ret.Add("http_text", "No Response");
                    break;

                case 495:
                    ret.Add("http_text", "SSL Certificate Error");
                    break;

                case 496:
                    ret.Add("http_text", "SSL Certificate Required");
                    break;

                case 497:
                    ret.Add("http_text", "HTTP Request Sent To HTTPS Port");
                    break;

                #endregion

                #region Cloudflare

                case 520:
                    ret.Add("http_text", "Unknown Error");
                    break;

                case 521:
                    ret.Add("http_text", "Web Server Is Down");
                    break;

                case 522:
                    ret.Add("http_text", "Connection Timed Out");
                    break;

                case 523:
                    ret.Add("http_text", "Origin Is Unreachable");
                    break;

                case 524:
                    ret.Add("http_text", "A Timeout Occurred");
                    break;

                case 525:
                    ret.Add("http_text", "SSL Handshake Failed");
                    break;

                case 526:
                    ret.Add("http_text", "Invalid SSL Certificate");
                    break;

                case 527:
                    ret.Add("http_text", "Railgun Error");
                    break;

                #endregion

                default:
                    ret.Add("http_text", "Unknown Status");
                    break;
            }
            
            string json = WatsonCommon.SerializeJson(ret);
            return Encoding.UTF8.GetBytes(json);
        }

        private void SendResponse(
            HttpListenerContext context,
            HttpRequest req,
            object data,
            Dictionary<string, string> headers,
            int status)
        {
            int responseLen = 0;
            HttpListenerResponse response = null;

            try
            {
                #region Set-Variables

                if (data != null)
                {
                    if (data is string)
                    {
                        if (!String.IsNullOrEmpty(data.ToString()))
                        {
                            responseLen = data.ToString().Length;
                        }
                    }
                    else if (data is byte[])
                    {
                        if ((byte[])data != null)
                        {
                            if (((byte[])data).Length > 0)
                            {
                                responseLen = ((byte[])data).Length;
                            }
                        }
                    }
                    else
                    {
                        responseLen = (WatsonCommon.SerializeJson(data)).Length;
                    }
                }
                
                #endregion

                #region Status-Code-and-Description

                response = context.Response;
                response.StatusCode = status;

                switch (status)
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

                int headerCount = 0;

                if (headers != null)
                {
                    if (headers.Count > 0)
                    {
                        headerCount = headers.Count;
                    }
                }

                if (headerCount > 0)
                {
                    foreach (KeyValuePair<string, string> curr in headers)
                    {
                        response.AddHeader(curr.Key, curr.Value);
                    }
                }

                #endregion

                #region Handle-HEAD-Request

                if (req.Method == HttpMethod.HEAD)
                {
                    data = null;
                }

                #endregion

                #region Send-Response
                
                Stream output = response.OutputStream;

                try
                {
                    if (data != null)
                    {
                        #region Response-Body-Attached

                        if (data is string)
                        {
                            #region string

                            if (!String.IsNullOrEmpty(data.ToString()))
                            {
                                if (data.ToString().Length > 0)
                                {
                                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(data.ToString());
                                    response.ContentLength64 = buffer.Length;
                                    output.Write(buffer, 0, buffer.Length);
                                    output.Close();
                                }
                            }

                            #endregion
                        }
                        else if (data is byte[])
                        {
                            #region byte-array

                            response.ContentLength64 = responseLen;
                            output.Write((byte[])data, 0, responseLen);
                            output.Close();

                            #endregion
                        }
                        else
                        {
                            #region other

                            response.ContentLength64 = responseLen;
                            output.Write(Encoding.UTF8.GetBytes(WatsonCommon.SerializeJson(data)), 0, responseLen); 
                            output.Close();

                            #endregion
                        }

                        #endregion
                    }
                    else
                    {
                        #region No-Response-Body

                        response.ContentLength64 = 0;
                        output.Flush();
                        output.Close();

                        #endregion
                    }
                }
                catch (HttpListenerException)
                {
                    _Logging.Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have disconnected");
                }
                finally
                {
                    if (response != null) response.Close();
                }

                #endregion

                return;
            }
            catch (IOException)
            {
                _Logging.Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have terminated connection prematurely (outer IOException)");
                return;
            }
            catch (HttpListenerException)
            {
                _Logging.Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have terminated connection prematurely (outer HttpListenerException)");
                return;
            }
            catch (Exception e)
            {
                _Logging.LogException("SendResponse", e);
                return;
            }
            finally
            {
                if (req != null)
                {
                    if (req.TimestampUtc != null)
                    {
                        _Logging.Log("Thread " + req.ThreadId + " sending " + responseLen + "B status " + status + " " + req.SourceIp + ":" + req.SourcePort + " for " + req.Method + " " + req.RawUrlWithoutQuery + " (" + WatsonCommon.TotalMsFrom(req.TimestampUtc) + "ms)");
                    }
                }

                if (response != null)
                {
                    response.Close();
                }
            }
        }
        
        private void OptionsProcessor(HttpListenerContext context, HttpRequest req)
        {
            _Logging.Log("Thread " + Thread.CurrentThread.ManagedThreadId + " processing OPTIONS request");

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

            _Logging.Log("Thread " + Thread.CurrentThread.ManagedThreadId + " sent OPTIONS response");
            return;
        }

        #endregion
    }
}
