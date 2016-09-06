using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    public class Server
    {
        #region Public-Members

        /// <summary>
        /// Specify whether or not Watson should log to the console at all.
        /// </summary>
        public bool ConsoleLogging;

        /// <summary>
        /// Specify whether or not incoming REST requests should be displayed on the console (requires ConsoleLogging == true).
        /// </summary>
        public bool DebugRestRequests;

        /// <summary>
        /// Specify whether or not outgoing REST responses should be displayed on the console (requires ConsoleLogging == true).
        /// </summary>
        public bool DebugRestResponses;

        #endregion

        #region Private-Members

        private readonly EventWaitHandle Terminator = new EventWaitHandle(false, EventResetMode.ManualReset, "UserIntervention");

        private string ListenerIp;
        private int ListenerPort;
        private bool ListenerSsl;
        private string ListenerPrefix;

        private Func<HttpRequest, HttpResponse> RequestReceived;

        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new instance of the Watson Webserver.
        /// </summary>
        /// <param name="ip">IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="requestReceived">Callback function used when a request is received.</param>
        public Server(string ip, int port, bool ssl, Func<HttpRequest, HttpResponse> requestReceived)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));
            if (requestReceived == null) throw new ArgumentNullException(nameof(requestReceived));

            ListenerIp = ip;
            ListenerPort = port;
            ListenerSsl = ssl;
            RequestReceived = requestReceived;
            ConsoleLogging = true;
            DebugRestRequests = true;
            DebugRestResponses = true;

            DisplaySmallLogo();
            Console.WriteLine("Watson Webserver :: v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            Task.Run(() => StartServer());
        }

        /// <summary>
        /// Creates a new instance of the Watson Webserver.
        /// </summary>
        /// <param name="ip">IP address on which to listen.</param>
        /// <param name="port">TCP port on which to listen.</param>
        /// <param name="ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="requestReceived">Callback function used when a request is received.</param>
        /// <param name="skipLogo">Set to true to not display Watson ASCII art.</param>
        public Server(string ip, int port, bool ssl, Func<HttpRequest, HttpResponse> requestReceived, bool skipLogo)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 1) throw new ArgumentOutOfRangeException(nameof(port));
            if (requestReceived == null) throw new ArgumentNullException(nameof(requestReceived));

            ListenerIp = ip;
            ListenerPort = port;
            ListenerSsl = ssl;
            RequestReceived = requestReceived;
            ConsoleLogging = true;
            DebugRestRequests = true;
            DebugRestResponses = true;

            if (!skipLogo) DisplaySmallLogo();
            Console.WriteLine("Watson Webserver :: v" + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString());
            Task.Run(() => StartServer());
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void DisplaySmallLogo()
        {
            //
            // taken from http://www.heartnsoul.com/ascii_art/dogs.txt
            //

            if (Console.IsOutputRedirected) return;
            
            Console.WriteLine(@"                                  ");
            Console.WriteLine(@"                       ,--.       ");
            Console.WriteLine(@"                     _/ <`-'      ");
            Console.WriteLine(@"                 ,-.' \--\_       ");
            Console.WriteLine(@"                ((`-.__\   )      ");
            Console.WriteLine(@"                 \`'    @ (_      ");
            Console.WriteLine(@"                 (        (_)     ");
            Console.WriteLine(@"                ,'`-._(`-._/      ");
            Console.WriteLine(@"             ,-'    )&&) ))       ");
            Console.WriteLine(@"          ,-'      /&&&%-'        ");
            Console.WriteLine(@"        ,' __  ,- {&&&&/          ");
            Console.WriteLine(@"       / ,'  \|   |\&&'\          ");
            Console.WriteLine(@"      (       |   |' \  `--.      ");
            Console.WriteLine(@"  (%--'\   ,--.\   `-.`-._)))     ");
            Console.WriteLine(@"   `---'`-/__)))`-._)))       hjw ");
            Console.WriteLine("");
            Console.WriteLine("");

            return;
        }

        private void DisplayLargeLogo()
        {
            if (Console.IsOutputRedirected) return;

            Console.WriteLine("");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMmddhhhhdddNMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMNds+++++ooo/:--+yNMMMMMMMMMMMMMMMMMMMMMMmhysyhdmNMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMmssso+ssssyyy+-....:/+osyyhhhhdhhyhdmo/::/+++++////+ymMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMdyyyhso+osyyyo:--................````.``./ssooooo+++//+yMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMNyhhhhs+oosso:----...``.......`````````````:ossso++++/+sshNMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMNmmmm+:/++:-----..```````.....```  ````````-:/oooo++oymdhhNMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMNyo+:::::---``  ````.....`` ```````````.::////::NMMNdhNMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMm+::::::/:..``....--.--.```...``.`....-:///+smMMMMMNNMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMs//+yyyyo/:---...---::-..-::/:-......-:/ssmMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMy++hdhddhs+/:-..--..:/:::/oyyyy+-..---:hNMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMN++yhhddyso/:-...----/++oshyhyyy/--:://MMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMN:/oyyyyo+::--...``..-:+/osysss+:-:://yMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMy-/+oos+/-------.``````--::///:-....:+hMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMo://+o/----:::::/:..````...--:-.````-oyMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMh:://:--/+ooosooooo/-.` ```.--.`````.+mMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMN//::--/yhdhyyysyysyo:`   ``..``````-+yNMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMy//:-:+hddmdyyhmdhyo/.  `` ``..```.:o+odmmNNMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMs/--:+yhddddhyhhys/-``````````...-/s+////++ohMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMNo::/+syhddddddys+:.`````````..---+s//:+o++odMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMh::/+oyyhhdhhhys+:.` ``.`  ``.--:os//:odyyydMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMd/:/osyhyhhhhyys+:.`````..`.-:::+oo++/smddhdMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMh+oshyysssyyyyyy+-.``.://:::::/+ooo+/ydddddMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMNoosoooo+oossssss+/:/o+/:::::/+oo++//yhddddMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMoooo++++++++ooo+-..--:---:::/oo++//yhyhdddMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMm/+oo+++o++++++oo/-...-::::::+soo/+odhhhhhmMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMm+/ooso++++++++///++:--:::////oso+shshhddyymMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMNo//oysys+++//+++++++/::::////+ss+sdo/hhhhhyodMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMmMMMMMMMMMMMNo/+++yhysoo+///////::::::://++oso+ohyhhhhhho+omMMMMMMM");
            Console.WriteLine(@"MMMMMMMyydMMMMMMMMMMy+++++oyhysso+////////::::/+oo+osyyshdhhhyhy+++sMMMMMMM");
            Console.WriteLine(@"MMMMMNsoyhMMMMMMMMMMo+++o++oyhdysso+++++++o///+oooydhshddhdhhhs++++omMMMMMM");
            Console.WriteLine(@"MMMMMh+sydMMMMMMMMMm+++oooooosydddyoooooosys++shddmmdyddddhys++++++oyMMMMMM");
            Console.WriteLine(@"MMMMm++oshNMMMMMMMMd+o+ooooooosyhdmdhyyyyyhyyymmmmmmdddhyso++++++++ooNMMMMM");
            Console.WriteLine(@"MMMMMy/+ooshdmmmmddy+ooooooosssyyyhdmdhhhddoyhmddddhhysoo++++++++++oomMMMMM");
            Console.WriteLine(@"MMMMMMds+++++++ooooooooo++oosssyyyyyhhdhyhhhyyhyyyyysssooo++++++++ooodMMMMM");
            Console.WriteLine(@"MMMMMMMMNdyoooooosss+oo+++oosssyyyysohhsssyyyhhhyyyyssssooo+++/++ooosmMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMNNNNNNNNyoo++ooossssssyyyssssyhhydddsoosssoooooo++oooossmMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMNysoooooossssssssoooshddyss+/++oooososoooosssyssMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMdsyssssssoooooooo+ydhso++///+oooossssssssyyssoMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMM+oysssssssoosysooosoooooooosyyyhhhyyyyyssssssMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMyosssssyyyhddddhhhyyyyhyhhhhdNNNMMNhhhyyyyyymMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMd+syyyyhhhhNMMMMMMMMMMMMMMMMMMMMMMMNdhyyyyyhMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMs++syyyhhhhNMMMMMMMMMMMMMMMMMMMMMMMMMhyyyyydMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMM/o+oyyyyyhhMMMMMMMMMMMMMMMMMMMMMMMMMMhyyyyyhMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMso:+yoyyyhhdMMMMMMMMMMMMMMMMMMMMMMMMNhhhhyyyNMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMNhoyyssyhhhhNMMMMMMMMMMMMMMMMMMMMMMMNyyyyyyshMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMNNNsosyhhhdMMMMMMMMMMMMMMMMMMMMMMMMhyyyssssNMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMN+ossyyydMMMMMMMMMMMMMMMMMMMMMMMMdossooosmMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMm+oossyydMMMMMMMMMMMMMMMMMMMMMMMMdoyooo+yNMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMm+/+ssyshMMMMMMMMMMMMMMMMMMMMMMMMNhds+sodMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMh++soyyyMMMMMMMMMMMMMMMMMMMMMMMMMNMdyydMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMm++yosdmMMMMMMMMMMMMMMMMMMMMMMMMMMMMMmMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMMmydymMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMNNNMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM");
            Console.WriteLine(@"MMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM");
            Console.WriteLine("");

            return;
        }

        private void StartServer()
        {
            Task.Run(() => AcceptConnections());
            Terminator.WaitOne();
        }

        private void AcceptConnections()
        {
            try
            {
                HttpListener http = new HttpListener();
                if (ListenerSsl) ListenerPrefix = "https://" + ListenerIp + ":" + ListenerPort + "/";
                else ListenerPrefix = "http://" + ListenerIp + ":" + ListenerPort + "/";
                http.Prefixes.Add(ListenerPrefix);
                http.Start();

                Log("Listener started on " + ListenerPrefix);

                while (http.IsListening)
                {
                    ThreadPool.QueueUserWorkItem((c) =>
                    {
                        var context = c as HttpListenerContext;

                        try
                        {
                            #region Populate-Http-Request-Object

                            HttpRequest currRequest = new HttpRequest(context);
                            if (currRequest == null)
                            {
                                Log("Unable to populate HTTP request object on thread ID " + Thread.CurrentThread.ManagedThreadId + ", returning 400");
                                SendResponse(
                                    context,
                                    currRequest,
                                    BuildErrorResponse(500, "Unable to parse your request.", null),
                                    Common.AddToDict("content-type", "application/json", null),
                                    400);
                                return;
                            }

                            Log("Thread " + currRequest.ThreadId + " " + currRequest.SourceIp + ":" + currRequest.SourcePort + " " + currRequest.Method + " " + currRequest.RawUrlWithoutQuery);

                            #endregion

                            #region Process-OPTIONS-Request

                            if (currRequest.Method.ToLower().Trim().Contains("option"))
                            {
                                Log("Thread " + Thread.CurrentThread.ManagedThreadId + " OPTIONS request received");
                                OptionsHandler(context, currRequest);
                                return;
                            }

                            #endregion

                            #region Send-to-API-Handler

                            if (DebugRestRequests) Log(currRequest.ToString());

                            Task.Run(() =>
                            {
                                HttpResponse currResponse = Process(context, currRequest);
                                if (currResponse == null)
                                {
                                    Log("Null response from API handler for request from " + currRequest.SourceIp + ":" + currRequest.SourcePort + " " + currRequest.Method + " " + currRequest.RawUrlWithoutQuery);
                                    SendResponse(
                                        context,
                                        currRequest,
                                        BuildErrorResponse(500, "Unable to generate response", null),
                                        Common.AddToDict("content-type", "application/json", null),
                                        400);
                                    return;
                                }
                                else
                                {
                                    if (DebugRestResponses) Log(currResponse.ToString());
                                    if (currResponse.RawResponse)
                                    {
                                        SendResponse(
                                            context,
                                            currRequest,
                                            currResponse.Data,
                                            Common.AddToDict("content-type", currResponse.ContentType, null),
                                            currResponse.StatusCode);
                                        return;
                                    }
                                    else
                                    {
                                        SendResponse(
                                            context,
                                            currRequest,
                                            currResponse.ToJsonBytes(),
                                            Common.AddToDict("content-type", currResponse.ContentType, null),
                                            currResponse.StatusCode);
                                        return;
                                    }
                                }
                            });

                            #endregion
                        }
                        catch (Exception e)
                        {
                            LogException("StartServer", e);
                            throw;
                        }
                        finally
                        {

                        }
                    }, http.GetContext());
                }
            }
            catch (Exception eOuter)
            {
                LogException("AcceptConnections", eOuter);
                throw;
            }
            finally
            {
                Log("Exiting");
            }
        }

        private HttpResponse Process(HttpListenerContext context, HttpRequest request)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (request == null) throw new ArgumentNullException(nameof(request));
            HttpResponse ret = RequestReceived(request);
            if (ret == null)
            {
                Log("Null HttpResponse received from call to RequestReceived, sending 500");
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
                    Log("Unknown http status code " + status);
                    break;
            }

            ret.Add("text", text);
            string json = Common.SerializeJson(ret);
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
                    Log("Unknown http status code " + status);
                    break;
            }
            
            string json = Common.SerializeJson(ret);
            return Encoding.UTF8.GetBytes(json);
        }

        private void Log(string msg)
        {
            if (ConsoleLogging) Console.WriteLine(msg);
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
                        Log("Unknown object type for response body (must be either byte[] or string)");
                        return;
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
                        Log("Unknown http status code " + status);
                        return;
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

                if (String.Compare(req.Method.ToLower(), "head") == 0)
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
                            #region unknown

                            Log("Unknown object type for response body");
                            response.ContentLength64 = 0;
                            output.Flush();
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
                    Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have disconnected");
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
                Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have terminated connection prematurely (outer IOException)");
                return;
            }
            catch (HttpListenerException)
            {
                Log("Remote endpoint " + req.SourceIp + ":" + req.SourcePort + " appears to have terminated connection prematurely (outer HttpListenerException)");
                return;
            }
            catch (Exception e)
            {
                LogException("SendResponse", e);
                return;
            }
            finally
            {
                if (req != null)
                {
                    if (req.TimestampUtc != null)
                    {
                        Log("Thread " + req.ThreadId + " sending " + responseLen + "B status " + status + " " + req.SourceIp + ":" + req.SourcePort + " for " + req.Method + " " + req.RawUrlWithoutQuery + " (" + Common.TotalMsFrom(req.TimestampUtc) + "ms)");
                    }
                }

                if (response != null)
                {
                    response.Close();
                }
            }
        }
        
        private void OptionsHandler(HttpListenerContext context, HttpRequest req)
        {
            Log("Thread " + Thread.CurrentThread.ManagedThreadId + " processing OPTIONS request");

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

            string headers = "x-email, x-password, x-api-key, x-token";

            if (requestedHeaders != null)
            {
                foreach (string curr in requestedHeaders)
                {
                    headers += ", " + curr;
                }
            }

            response.AddHeader("Access-Control-Allow-Methods", "OPTIONS, HEAD, GET, PUT, POST, DELETE");
            response.AddHeader("Access-Control-Allow-Headers", "*, Content-Type, X-Requested-With, " + headers);
            response.AddHeader("Access-Control-Expose-Headers", "Content-Type, X-Requested-With, " + headers);
            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Accept", "*/*");
            response.AddHeader("Accept-Language", "en-US, en");
            response.AddHeader("Accept-Charset", "ISO-8859-1, utf-8");
            response.AddHeader("Connection", "keep-alive");
            response.AddHeader("Host", ListenerPrefix);
            response.ContentLength64 = 0;
            response.Close();

            Log("Thread " + Thread.CurrentThread.ManagedThreadId + " sent OPTIONS response");
            return;
        }

        public void LogException(string method, Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var st = new StackTrace(e, true);
            var frame = st.GetFrame(0);
            int fileLine = frame.GetFileLineNumber();
            string filename = frame.GetFileName();

            string message =
                Environment.NewLine +
                "---" + Environment.NewLine +
                "An exception was encountered which triggered this message" + Environment.NewLine +
                "  Method     : " + method + Environment.NewLine +
                "  Type       : " + e.GetType().ToString() + Environment.NewLine +
                "  Data       : " + e.Data + Environment.NewLine +
                "  Inner      : " + e.InnerException + Environment.NewLine +
                "  Message    : " + e.Message + Environment.NewLine +
                "  Source     : " + e.Source + Environment.NewLine +
                "  StackTrace : " + e.StackTrace + Environment.NewLine +
                "  Stack      : " + StackToString() + Environment.NewLine +
                "  Line       : " + fileLine + Environment.NewLine +
                "  File       : " + filename + Environment.NewLine +
                "  ToString   : " + e.ToString() + Environment.NewLine +
                "  Servername : " + Dns.GetHostName() + Environment.NewLine +
                "---";

            Log(message);
        }

        private string StackToString()
        {
            string ret = "";

            StackTrace t = new StackTrace();
            for (int i = 0; i < t.FrameCount; i++)
            {
                if (i == 0)
                {
                    ret += t.GetFrame(i).GetMethod().Name;
                }
                else
                {
                    ret += " <= " + t.GetFrame(i).GetMethod().Name;
                }
            }

            return ret;
        }

        #endregion
    }
}
