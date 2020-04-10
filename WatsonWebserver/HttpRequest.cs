using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
 
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// Data extracted from an incoming HTTP request.
    /// </summary>
    public class HttpRequest
    {
        #region Public-Members
        
        /// <summary>
        /// UTC timestamp from when the request was received.
        /// </summary>
        public DateTime TimestampUtc;

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        public int ThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        public string ProtocolVersion;

        /// <summary>
        /// IP address of the requestor (client).
        /// </summary>
        public string SourceIp;

        /// <summary>
        /// TCP port from which the request originated on the requestor (client).
        /// </summary>
        public int SourcePort;

        /// <summary>
        /// IP address of the recipient (server).
        /// </summary>
        public string DestIp;

        /// <summary>
        /// TCP port on which the request was received by the recipient (server).
        /// </summary>
        public int DestPort;

        /// <summary>
        /// The destination hostname as found in the request line, if present.
        /// </summary>
        public string DestHostname;

        /// <summary>
        /// The destination host port as found in the request line, if present.
        /// </summary>
        public int DestHostPort;

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive;

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate = false;

        /// <summary>
        /// The full URL as sent by the requestor (client).
        /// </summary>
        public string FullUrl;

        /// <summary>
        /// The raw (relative) URL with the querystring attached.
        /// </summary>
        public string RawUrlWithQuery;

        /// <summary>
        /// The raw (relative) URL without the querystring attached.
        /// </summary>
        public string RawUrlWithoutQuery;

        /// <summary>
        /// List of items found in the raw URL.
        /// </summary>
        public List<string> RawUrlEntries;

        /// <summary>
        /// The querystring attached to the URL.
        /// </summary>
        public string Querystring;

        /// <summary>
        /// Dictionary containing key-value pairs from items found in the querystring.
        /// </summary>
        public Dictionary<string, string> QuerystringEntries;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        public long ContentLength;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        public Dictionary<string, string> Headers;
         
        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public Stream Data;
         
        /// <summary>
        /// The original HttpListenerContext from which the HttpRequest was constructed.
        /// </summary>
        [JsonIgnore]
        public HttpListenerContext ListenerContext;

        #endregion

        #region Private-Members
         
        private Uri _Uri; 
        private static int _TimeoutDataReadMs = 2000;
        private static int _DataReadSleepMs = 10;
        private byte[] _DataBytes = null;
        
        #endregion

        #region Constructors-and-Factories
         
        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpRequest()
        {
            ThreadId = Thread.CurrentThread.ManagedThreadId;
            TimestampUtc = DateTime.Now.ToUniversalTime();
            QuerystringEntries = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
        }
         
        /// <summary>
        /// Instantiate the object using an HttpListenerContext.
        /// </summary>
        /// <param name="ctx">HttpListenerContext.</param>
        public HttpRequest(HttpListenerContext ctx)
        {
            #region Check-for-Null-Values

            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));

            #endregion

            #region Parse-Variables

            int position = 0;
            int inQuery = 0;
            string tempString = "";
            string queryString = "";

            int inKey = 0;
            int inVal = 0;
            string tempKey = "";
            string tempVal = "";

            #endregion
            
            #region Standard-Request-Items

            ThreadId = Thread.CurrentThread.ManagedThreadId;
            TimestampUtc = DateTime.Now.ToUniversalTime();
            ProtocolVersion = "HTTP/" + ctx.Request.ProtocolVersion.ToString();
            SourceIp = ctx.Request.RemoteEndPoint.Address.ToString();
            SourcePort = ctx.Request.RemoteEndPoint.Port;
            DestIp = ctx.Request.LocalEndPoint.Address.ToString();
            DestPort = ctx.Request.LocalEndPoint.Port;
            Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), ctx.Request.HttpMethod, true);
            FullUrl = String.Copy(ctx.Request.Url.ToString().Trim());
            RawUrlWithQuery = String.Copy(ctx.Request.RawUrl.ToString().Trim());
            RawUrlWithoutQuery = String.Copy(ctx.Request.RawUrl.ToString().Trim());
            Keepalive = ctx.Request.KeepAlive;
            ContentLength = ctx.Request.ContentLength64;
            Useragent = ctx.Request.UserAgent;
            ContentType = ctx.Request.ContentType;
            ListenerContext = ctx;

            RawUrlEntries = new List<string>();
            QuerystringEntries = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();

            #endregion

            #region Raw-URL-and-Querystring

            if (!String.IsNullOrEmpty(RawUrlWithoutQuery))
            {
                #region Initialize-Variables

                RawUrlEntries = new List<string>();
                QuerystringEntries = new Dictionary<string, string>();

                #endregion

                #region Process-Raw-URL-and-Populate-Raw-URL-Elements

                while (RawUrlWithoutQuery.Contains("//"))
                {
                    RawUrlWithoutQuery = RawUrlWithoutQuery.Replace("//", "/");
                }

                foreach (char c in RawUrlWithoutQuery)
                {
                    if (inQuery == 1)
                    {
                        queryString += c;
                        continue;
                    }

                    if ((position == 0) &&
                        (String.Compare(tempString, "") == 0) &&
                        (c == '/'))
                    {
                        // skip the first slash
                        continue;
                    }

                    if ((c != '/') && (c != '?'))
                    {
                        tempString += c;
                    }

                    if ((c == '/') || (c == '?'))
                    {
                        if (!String.IsNullOrEmpty(tempString))
                        {
                            // add to raw URL entries list
                            RawUrlEntries.Add(tempString);
                        }

                        position++;
                        tempString = "";
                    }

                    if (c == '?')
                    {
                        inQuery = 1;
                    }
                }

                if (!String.IsNullOrEmpty(tempString))
                {
                    // add to raw URL entries list
                    RawUrlEntries.Add(tempString);
                }

                #endregion

                #region Populate-Querystring

                if (queryString.Length > 0) Querystring = queryString;
                else Querystring = null;

                #endregion

                #region Parse-Querystring

                if (!String.IsNullOrEmpty(Querystring))
                {
                    inKey = 1;
                    inVal = 0;
                    position = 0;
                    tempKey = "";
                    tempVal = "";

                    foreach (char c in Querystring)
                    {
                        if (inKey == 1)
                        {
                            if (c == '&')
                            {
                                // key with no value
                                if (!String.IsNullOrEmpty(tempKey))
                                {
                                    inKey = 1;
                                    inVal = 0;

                                    tempKey = WebUtility.UrlDecode(tempKey);
                                    QuerystringEntries = AddToDict(tempKey, null, QuerystringEntries);

                                    tempKey = "";
                                    tempVal = "";
                                    position++;
                                    continue;
                                }
                            }
                            else if (c != '=')
                            {
                                tempKey += c;
                            }
                            else
                            {
                                inKey = 0;
                                inVal = 1;
                                continue;
                            }
                        }

                        if (inVal == 1)
                        {
                            if (c != '&')
                            {
                                tempVal += c;
                            }
                            else
                            {
                                inKey = 1;
                                inVal = 0;

                                tempKey = WebUtility.UrlDecode(tempKey);
                                if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlDecode(tempVal);
                                QuerystringEntries = AddToDict(tempKey, tempVal, QuerystringEntries);

                                tempKey = "";
                                tempVal = "";
                                position++;
                                continue;
                            }
                        }
                    }

                    if (inVal == 0)
                    {
                        // val will be null
                        if (!String.IsNullOrEmpty(tempKey))
                        {
                            tempKey = WebUtility.UrlDecode(tempKey);
                            QuerystringEntries = AddToDict(tempKey, null, QuerystringEntries);
                        } 
                    }

                    if (inVal == 1)
                    {
                        if (!String.IsNullOrEmpty(tempKey))
                        {
                            tempKey = WebUtility.UrlDecode(tempKey);
                            if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlDecode(tempVal);
                            QuerystringEntries = AddToDict(tempKey, tempVal, QuerystringEntries);
                        }
                    }
                }

                #endregion
            }

            #endregion

            #region Remove-Querystring-from-Raw-URL

            if (RawUrlWithoutQuery.Contains("?"))
            {
                RawUrlWithoutQuery = RawUrlWithoutQuery.Substring(0, RawUrlWithoutQuery.IndexOf("?"));
            }

            #endregion

            #region Check-for-Full-URL

            try
            {
                _Uri = new Uri(FullUrl);
                DestHostname = _Uri.Host;
                DestHostPort = _Uri.Port;
            }
            catch (Exception)
            {

            }

            #endregion

            #region Headers

            Headers = new Dictionary<string, string>();
            for (int i = 0; i < ctx.Request.Headers.Count; i++)
            {
                string key = String.Copy(ctx.Request.Headers.GetKey(i));
                string val = String.Copy(ctx.Request.Headers.Get(i));
                Headers = AddToDict(key, val, Headers);
            }

            foreach (KeyValuePair<string, string> curr in Headers)
            {
                if (String.IsNullOrEmpty(curr.Key)) continue;
                if (String.IsNullOrEmpty(curr.Value)) continue;

                if (curr.Key.ToLower().Equals("transfer-encoding"))
                {
                    if (curr.Value.ToLower().Contains("chunked"))
                        ChunkedTransfer = true;
                    if (curr.Value.ToLower().Contains("gzip"))
                        Gzip = true;
                    if (curr.Value.ToLower().Contains("deflate"))
                        Deflate = true;
                }
            }

            #endregion

            #region Payload
             
            Data = ctx.Request.InputStream;   

            #endregion
        }

        /// <summary>
        /// Instantiate the object using a generic stream.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <returns>HttpRequest.</returns>
        public static HttpRequest FromStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            try
            {
                #region Variables

                HttpRequest ret;
                byte[] headerBytes = null;
                byte[] lastFourBytes = new byte[4];
                lastFourBytes[0] = 0x00;
                lastFourBytes[1] = 0x00;
                lastFourBytes[2] = 0x00;
                lastFourBytes[3] = 0x00;

                #endregion

                #region Check-Stream

                if (!stream.CanRead)
                {
                    throw new IOException("Unable to read from stream.");
                }

                #endregion

                #region Read-Headers

                using (MemoryStream headerMs = new MemoryStream())
                {
                    #region Read-Header-Bytes

                    byte[] headerBuffer = new byte[1];
                    int read = 0;
                    int headerBytesRead = 0;

                    while ((read = stream.Read(headerBuffer, 0, headerBuffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            #region Initialize-Header-Bytes-if-Needed

                            headerBytesRead += read;
                            if (headerBytes == null) headerBytes = new byte[1];

                            #endregion

                            #region Update-Last-Four

                            if (read == 1)
                            {
                                lastFourBytes[0] = lastFourBytes[1];
                                lastFourBytes[1] = lastFourBytes[2];
                                lastFourBytes[2] = lastFourBytes[3];
                                lastFourBytes[3] = headerBuffer[0];
                            }

                            #endregion

                            #region Append-to-Header-Buffer

                            byte[] tempHeader = new byte[headerBytes.Length + 1];
                            Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                            tempHeader[headerBytes.Length] = headerBuffer[0];
                            headerBytes = tempHeader;

                            #endregion

                            #region Check-for-End-of-Headers

                            if ((int)(lastFourBytes[0]) == 13
                                && (int)(lastFourBytes[1]) == 10
                                && (int)(lastFourBytes[2]) == 13
                                && (int)(lastFourBytes[3]) == 10)
                            {
                                break;
                            }

                            #endregion
                        }
                    }

                    #endregion
                }

                #endregion

                #region Process-Headers

                if (headerBytes == null || headerBytes.Length < 1) throw new IOException("No header data read from the stream.");
                ret = BuildHeaders(headerBytes);

                #endregion

                #region Read-Data

                ret.Data = null;
                if (ret.ContentLength > 0)
                {
                    #region Read-from-Stream

                    ret.Data = new MemoryStream();

                    long bytesRemaining = ret.ContentLength;
                    long bytesRead = 0;
                    bool timeout = false;
                    int currentTimeout = 0;

                    int read = 0;
                    byte[] buffer;
                    long bufferSize = 2048;
                    if (bufferSize > bytesRemaining) bufferSize = bytesRemaining;
                    buffer = new byte[bufferSize];

                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            ret.Data.Write(buffer, 0, read);
                            bytesRead = bytesRead + read;
                            bytesRemaining = bytesRemaining - read;

                            // reduce buffer size if number of bytes remaining is
                            // less than the pre-defined buffer size of 2KB
                            if (bytesRemaining < bufferSize)
                            {
                                bufferSize = bytesRemaining;
                            }

                            buffer = new byte[bufferSize];

                            // check if read fully
                            if (bytesRemaining == 0) break;
                            if (bytesRead == ret.ContentLength) break;
                        }
                        else
                        {
                            if (currentTimeout >= _TimeoutDataReadMs)
                            {
                                timeout = true;
                                break;
                            }
                            else
                            {
                                currentTimeout += _DataReadSleepMs;
                                Thread.Sleep(_DataReadSleepMs);
                            }
                        }
                    }

                    if (timeout)
                    {
                        throw new IOException("Timeout reading data from stream.");
                    }

                    ret.Data.Seek(0, SeekOrigin.Begin);

                    #endregion
                }
                else
                {
                    // do nothing
                }

                #endregion

                return ret;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Instantiate the object using a network stream.
        /// </summary>
        /// <param name="stream">NetworkStream.</param>
        /// <returns>HttpRequest.</returns>
        public static HttpRequest FromStream(NetworkStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            try
            {
                #region Variables

                HttpRequest ret;
                byte[] headerBytes = null;
                byte[] lastFourBytes = new byte[4];
                lastFourBytes[0] = 0x00;
                lastFourBytes[1] = 0x00;
                lastFourBytes[2] = 0x00;
                lastFourBytes[3] = 0x00;

                #endregion

                #region Check-Stream

                if (!stream.CanRead)
                {
                    throw new IOException("Unable to read from stream.");
                }

                #endregion

                #region Read-Headers

                using (MemoryStream headerMs = new MemoryStream())
                {
                    #region Read-Header-Bytes

                    byte[] headerBuffer = new byte[1];
                    int read = 0;
                    int headerBytesRead = 0;

                    while ((read = stream.Read(headerBuffer, 0, headerBuffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            #region Initialize-Header-Bytes-if-Needed

                            headerBytesRead += read;
                            if (headerBytes == null) headerBytes = new byte[1];

                            #endregion

                            #region Update-Last-Four

                            if (read == 1)
                            {
                                lastFourBytes[0] = lastFourBytes[1];
                                lastFourBytes[1] = lastFourBytes[2];
                                lastFourBytes[2] = lastFourBytes[3];
                                lastFourBytes[3] = headerBuffer[0];
                            }

                            #endregion

                            #region Append-to-Header-Buffer

                            byte[] tempHeader = new byte[headerBytes.Length + 1];
                            Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                            tempHeader[headerBytes.Length] = headerBuffer[0];
                            headerBytes = tempHeader;

                            #endregion

                            #region Check-for-End-of-Headers

                            if ((int)(lastFourBytes[0]) == 13
                                && (int)(lastFourBytes[1]) == 10
                                && (int)(lastFourBytes[2]) == 13
                                && (int)(lastFourBytes[3]) == 10)
                            {
                                break;
                            }

                            #endregion
                        }
                    }

                    #endregion
                }

                #endregion

                #region Process-Headers

                if (headerBytes == null || headerBytes.Length < 1) throw new IOException("No header data read from the stream.");
                ret = BuildHeaders(headerBytes);

                #endregion

                #region Read-Data

                ret.Data = null;
                if (ret.ContentLength > 0)
                {
                    #region Read-from-Stream

                    ret.Data = new MemoryStream();

                    long bytesRemaining = ret.ContentLength;
                    long bytesRead = 0;
                    bool timeout = false;
                    int currentTimeout = 0;

                    int read = 0;
                    byte[] buffer;
                    long bufferSize = 2048;
                    if (bufferSize > bytesRemaining) bufferSize = bytesRemaining;
                    buffer = new byte[bufferSize];

                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            ret.Data.Write(buffer, 0, read);
                            bytesRead = bytesRead + read;
                            bytesRemaining = bytesRemaining - read;

                            // reduce buffer size if number of bytes remaining is
                            // less than the pre-defined buffer size of 2KB
                            if (bytesRemaining < bufferSize)
                            {
                                bufferSize = bytesRemaining;
                            }

                            buffer = new byte[bufferSize];

                            // check if read fully
                            if (bytesRemaining == 0) break;
                            if (bytesRead == ret.ContentLength) break;
                        }
                        else
                        {
                            if (currentTimeout >= _TimeoutDataReadMs)
                            {
                                timeout = true;
                                break;
                            }
                            else
                            {
                                currentTimeout += _DataReadSleepMs;
                                Thread.Sleep(_DataReadSleepMs);
                            }
                        }
                    }

                    if (timeout)
                    {
                        throw new IOException("Timeout reading data from stream.");
                    }

                    ret.Data.Seek(0, SeekOrigin.Begin);

                    #endregion
                }
                else
                {
                    // do nothing
                }

                #endregion

                return ret;
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Instantiate the object using a TCP client.
        /// </summary>
        /// <param name="client">TcpClient.</param>
        /// <returns>HttpRequest.</returns>
        public static HttpRequest FromTcpClient(TcpClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            try
            {
                #region Variables

                HttpRequest ret;
                byte[] headerBytes = null;
                byte[] lastFourBytes = new byte[4];
                lastFourBytes[0] = 0x00;
                lastFourBytes[1] = 0x00;
                lastFourBytes[2] = 0x00;
                lastFourBytes[3] = 0x00;

                #endregion

                #region Attach-Stream

                NetworkStream stream = client.GetStream();

                if (!stream.CanRead)
                {
                    throw new IOException("Unable to read from stream.");
                }

                #endregion

                #region Read-Headers

                using (MemoryStream headerMs = new MemoryStream())
                {
                    #region Read-Header-Bytes

                    byte[] headerBuffer = new byte[1];
                    int read = 0;
                    int headerBytesRead = 0;

                    while ((read = stream.Read(headerBuffer, 0, headerBuffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            #region Initialize-Header-Bytes-if-Needed

                            headerBytesRead += read;
                            if (headerBytes == null) headerBytes = new byte[1];

                            #endregion

                            #region Update-Last-Four

                            if (read == 1)
                            {
                                lastFourBytes[0] = lastFourBytes[1];
                                lastFourBytes[1] = lastFourBytes[2];
                                lastFourBytes[2] = lastFourBytes[3];
                                lastFourBytes[3] = headerBuffer[0];
                            }

                            #endregion

                            #region Append-to-Header-Buffer

                            byte[] tempHeader = new byte[headerBytes.Length + 1];
                            Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                            tempHeader[headerBytes.Length] = headerBuffer[0];
                            headerBytes = tempHeader;

                            #endregion

                            #region Check-for-End-of-Headers

                            if ((int)(lastFourBytes[0]) == 13
                                && (int)(lastFourBytes[1]) == 10
                                && (int)(lastFourBytes[2]) == 13
                                && (int)(lastFourBytes[3]) == 10)
                            {
                                break;
                            }

                            #endregion
                        }
                    }

                    #endregion
                }

                #endregion

                #region Process-Headers

                if (headerBytes == null || headerBytes.Length < 1) throw new IOException("No header data read from the stream.");
                ret = BuildHeaders(headerBytes);

                #endregion

                #region Read-Data

                ret.Data = null;
                if (ret.ContentLength > 0)
                {
                    #region Read-from-Stream

                    ret.Data = new MemoryStream();

                    long bytesRemaining = ret.ContentLength;
                    long bytesRead = 0;
                    bool timeout = false;
                    int currentTimeout = 0;

                    int read = 0;
                    byte[] buffer;
                    long bufferSize = 2048;
                    if (bufferSize > bytesRemaining) bufferSize = bytesRemaining;
                    buffer = new byte[bufferSize];

                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (read > 0)
                        {
                            ret.Data.Write(buffer, 0, read);
                            bytesRead = bytesRead + read;
                            bytesRemaining = bytesRemaining - read;

                            // reduce buffer size if number of bytes remaining is
                            // less than the pre-defined buffer size of 2KB
                            if (bytesRemaining < bufferSize)
                            {
                                bufferSize = bytesRemaining;
                            }

                            buffer = new byte[bufferSize];

                            // check if read fully
                            if (bytesRemaining == 0) break;
                            if (bytesRead == ret.ContentLength) break;
                        }
                        else
                        {
                            if (currentTimeout >= _TimeoutDataReadMs)
                            {
                                timeout = true;
                                break;
                            }
                            else
                            {
                                currentTimeout += _DataReadSleepMs;
                                Thread.Sleep(_DataReadSleepMs);
                            }
                        }
                    }

                    if (timeout)
                    {
                        throw new IOException("Timeout reading data from stream.");
                    }

                    ret.Data.Seek(0, SeekOrigin.Begin);

                    #endregion
                }
                else
                {
                    // do nothing
                }

                #endregion

                return ret;
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a string-formatted, human-readable copy of the HttpRequest instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpRequest instance.</returns>
        public override string ToString()
        {
            string ret = ""; 

            ret += "--- HTTP Request ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + Environment.NewLine;
            ret += "  " + Method + " " + RawUrlWithoutQuery + " " + ProtocolVersion + Environment.NewLine;
            ret += "  Full URL    : " + FullUrl + Environment.NewLine;
            ret += "  Raw URL     : " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Querystring : " + Querystring + Environment.NewLine;
            ret += "  Useragent   : " + Useragent + " (Keepalive " + Keepalive + ")" + Environment.NewLine;
            ret += "  Content     : " + ContentType + " (" + ContentLength + " bytes)" + Environment.NewLine;
            ret += "  Destination : " + DestHostname + ":" + DestHostPort + Environment.NewLine;

            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers     : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "    " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Headers     : none" + Environment.NewLine;
            }
             
            return ret;
        }

        /// <summary>
        /// Retrieve a specified header value from either the headers or the querystring (case insensitive).
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Headers != null && Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            if (QuerystringEntries != null && QuerystringEntries.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in QuerystringEntries)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            return null;
        }
        
        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// </summary>
        /// <returns>Chunk.</returns>
        public async Task<Chunk> ReadChunk()
        {
            if (!ChunkedTransfer) throw new IOException("Request is not chunk transfer-encoded.");

            Chunk chunk = new Chunk();
             
            #region Get-Length-and-Metadata

            byte[] buffer = new byte[1];
            byte[] lenBytes = null;
            int bytesRead = 0;

            while (true)
            {
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    lenBytes = AppendBytes(lenBytes, buffer);  
                    string lenStr = Encoding.UTF8.GetString(lenBytes); 

                    if (lenBytes[lenBytes.Length - 1] == 10)
                    {
                        lenStr = lenStr.Trim();

                        if (lenStr.Contains(";"))
                        {
                            string[] lenStrParts = lenStr.Split(new char[] { ';' }, 2);
                            lenStr = lenStrParts[0];

                            if (lenStrParts.Length == 2)
                            {
                                chunk.Metadata = lenStrParts[1];
                            }
                        }
                        else
                        {
                            chunk.Length = int.Parse(lenStr, NumberStyles.HexNumber);
                        }

                        // Console.WriteLine("- Chunk length determined: " + chunk.Length); 
                        break;
                    }
                }
            }

            #endregion

            #region Get-Data

            // Console.WriteLine("- Reading " + chunk.Length + " bytes");

            if (chunk.Length > 0)
            {
                chunk.IsFinalChunk = false;
                buffer = new byte[chunk.Length];
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == chunk.Length)
                {
                    chunk.Data = new byte[chunk.Length];
                    Buffer.BlockCopy(buffer, 0, chunk.Data, 0, chunk.Length);
                    // Console.WriteLine("- Data: " + Encoding.UTF8.GetString(buffer));
                }
                else
                {
                    throw new IOException("Expected " + chunk.Length + " bytes but only read " + bytesRead + " bytes in chunk.");
                }
            }
            else
            {
                chunk.IsFinalChunk = true;
            }

            #endregion

            #region Get-Trailing-CRLF

            buffer = new byte[1];

            while (true)
            {
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    if (buffer[0] == 10) break;
                }
            }

            #endregion

            return chunk; 
        }
         
        /// <summary>
        /// Read the data stream fully and retrieve the byte data contained within.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <returns>Byte array.</returns>
        public byte[] DataAsBytes()
        {
            ReadStreamFully();
            return _DataBytes;
        }

        /// <summary>
        /// Read the data stream fully and retrieve the string data contained within.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <returns>String.</returns>
        public string DataAsString()
        {
            ReadStreamFully();
            if (_DataBytes == null) return null;
            else return Encoding.UTF8.GetString(_DataBytes);
        }

        /// <summary>
        /// Read the data stream fully and convert the data to the object type specified using JSON deserialization.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Object of type specified.</returns>
        public T DataAsJsonObject<T>() where T : class
        {
            string json = DataAsString();
            if (String.IsNullOrEmpty(json)) return null;
            return SerializationHelper.DeserializeJson<T>(json);
        }
         
        #endregion

        #region Private-Methods
         
        private static HttpRequest BuildHeaders(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            #region Initial-Values

            HttpRequest ret = new HttpRequest();
            ret.TimestampUtc = DateTime.Now.ToUniversalTime();
            ret.ThreadId = Thread.CurrentThread.ManagedThreadId;
            ret.SourceIp = "unknown";
            ret.SourcePort = 0;
            ret.DestIp = "unknown";
            ret.DestPort = 0;
            ret.Headers = new Dictionary<string, string>();

            #endregion

            #region Convert-to-String-List

            string str = Encoding.UTF8.GetString(bytes);
            string[] headers = str.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            #endregion

            #region Process-Each-Line

            for (int i = 0; i < headers.Length; i++)
            {
                if (i == 0)
                {
                    #region First-Line
                     
                    string[] requestLine = headers[i].Trim().Trim('\0').Split(' ');
                    if (requestLine.Length < 3) throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");
                      
                    ret.Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), requestLine[0], true);
                    ret.FullUrl = requestLine[1];
                    ret.ProtocolVersion = requestLine[2];
                    ret.RawUrlWithQuery = ret.FullUrl;
                    ret.RawUrlWithoutQuery = ExtractRawUrlWithoutQuery(ret.RawUrlWithQuery);
                    ret.RawUrlEntries = ExtractRawUrlEntries(ret.RawUrlWithoutQuery);
                    ret.Querystring = ExtractQuerystring(ret.RawUrlWithQuery);
                    ret.QuerystringEntries = ExtractQuerystringEntries(ret.Querystring);

                    try
                    {
                        Uri uri = new Uri(ret.FullUrl);
                        ret.DestHostname = uri.Host;
                        ret.DestHostPort = uri.Port;
                    }
                    catch (Exception)
                    {
                    }

                    if (String.IsNullOrEmpty(ret.DestHostname))
                    {
                        if (!ret.FullUrl.Contains("://") & ret.FullUrl.Contains(":"))
                        {
                            string[] hostAndPort = ret.FullUrl.Split(':');
                            if (hostAndPort.Length == 2)
                            {
                                ret.DestHostname = hostAndPort[0];
                                if (!Int32.TryParse(hostAndPort[1], out ret.DestHostPort))
                                {
                                    throw new Exception("Unable to parse destination hostname and port.");
                                }
                            }
                        } 
                    }

                    #endregion
                }
                else
                {
                    #region Subsequent-Line

                    string[] headerLine = headers[i].Split(':');
                    if (headerLine.Length == 2)
                    {
                        string key = headerLine[0].Trim();
                        string val = headerLine[1].Trim();

                        if (String.IsNullOrEmpty(key)) continue;
                        string keyEval = key.ToLower();

                        if (keyEval.Equals("keep-alive"))
                        {
                            ret.Keepalive = Convert.ToBoolean(val);
                        }
                        else if (keyEval.Equals("user-agent"))
                        {
                            ret.Useragent = val;
                        }
                        else if (keyEval.Equals("content-length"))
                        {
                            ret.ContentLength = Convert.ToInt64(val);
                        }
                        else if (keyEval.Equals("content-type"))
                        {
                            ret.ContentType = val;
                        }
                        else if (keyEval.Equals("transfer-encoding"))
                        {
                            if (String.IsNullOrEmpty(val)) continue;
                            if (val.ToLower().Contains("chunked"))
                                ret.ChunkedTransfer = true;
                            if (val.ToLower().Contains("gzip"))
                                ret.Gzip = true;
                            if (val.ToLower().Contains("deflate"))
                                ret.Deflate = true;
                        }
                        else
                        {
                            ret.Headers = AddToDict(key, val, ret.Headers);
                        }
                    }

                    #endregion
                }
            }

            #endregion
             
            return ret;
        }

        private static string ExtractRawUrlWithoutQuery(string rawUrlWithQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithQuery)) return null;
            if (!rawUrlWithQuery.Contains("?")) return rawUrlWithQuery;
            return rawUrlWithQuery.Substring(0, rawUrlWithQuery.IndexOf("?"));
        }

        private static List<string> ExtractRawUrlEntries(string rawUrlWithoutQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithoutQuery)) return null;

            int position = 0;
            string tempString = "";
            List<string> ret = new List<string>();

            foreach (char c in rawUrlWithoutQuery)
            { 
                if ((position == 0) &&
                    (String.Compare(tempString, "") == 0) &&
                    (c == '/'))
                {
                    // skip the first slash
                    continue;
                }

                if ((c != '/') && (c != '?'))
                {
                    tempString += c;
                }

                if ((c == '/') || (c == '?'))
                {
                    if (!String.IsNullOrEmpty(tempString))
                    {
                        // add to raw URL entries list
                        ret.Add(tempString);
                    }

                    position++;
                    tempString = "";
                }
            }

            if (!String.IsNullOrEmpty(tempString))
            {
                // add to raw URL entries list
                ret.Add(tempString);
            }

            return ret;
        }

        private static string ExtractQuerystring(string rawUrlWithQuery)
        {
            if (String.IsNullOrEmpty(rawUrlWithQuery)) return null;
            if (!rawUrlWithQuery.Contains("?")) return null;

            int qsStartPos = rawUrlWithQuery.IndexOf("?");
            if (qsStartPos >= (rawUrlWithQuery.Length - 1)) return null;
            return rawUrlWithQuery.Substring(qsStartPos + 1);
        }

        private static Dictionary<string, string> ExtractQuerystringEntries(string query)
        {
            if (String.IsNullOrEmpty(query)) return null;

            Dictionary<string, string> ret = new Dictionary<string, string>();
             
            int inKey = 1;
            int inVal = 0;
            int position = 0;
            string tempKey = "";
            string tempVal = "";

            foreach (char c in query)
            {
                if (inKey == 1)
                {
                    if (c != '=')
                    {
                        tempKey += c;
                    }
                    else
                    {
                        inKey = 0;
                        inVal = 1;
                        continue;
                    }
                }

                if (inVal == 1)
                {
                    if (c != '&')
                    {
                        tempVal += c;
                    }
                    else
                    {
                        inKey = 1;
                        inVal = 0;

                        if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlEncode(tempVal);
                        ret = AddToDict(tempKey, tempVal, ret);

                        tempKey = "";
                        tempVal = "";
                        position++;
                        continue;
                    }
                }
                
                if (inVal == 1)
                {
                    if (!String.IsNullOrEmpty(tempVal)) tempVal = WebUtility.UrlEncode(tempVal);
                    ret = AddToDict(tempKey, tempVal, ret);
                }
            }

            return ret;
        }

        private static Dictionary<string, string> AddToDict(string key, string val, Dictionary<string, string> existing)
        {
            if (String.IsNullOrEmpty(key)) return existing;

            Dictionary<string, string> ret = new Dictionary<string, string>();

            if (existing == null)
            {
                ret.Add(key, val);
                return ret;
            }
            else
            {
                if (existing.ContainsKey(key))
                {
                    if (String.IsNullOrEmpty(val)) return existing;
                    string tempVal = existing[key];
                    tempVal += "," + val;
                    existing.Remove(key);
                    existing.Add(key, tempVal);
                    return existing;
                }
                else
                {
                    existing.Add(key, val);
                    return existing;
                }
            }
        }

        private static byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (orig == null && append == null) return null;

            byte[] ret = null;

            if (append == null)
            {
                ret = new byte[orig.Length];
                Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
                return ret;
            }

            if (orig == null)
            {
                ret = new byte[append.Length];
                Buffer.BlockCopy(append, 0, ret, 0, append.Length);
                return ret;
            }

            ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }

        private byte[] StreamToBytes(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        private void ReadStreamFully()
        {
            if (Data == null) return;
            if (!Data.CanRead) return;

            if (_DataBytes == null)
            {
                if (!ChunkedTransfer)
                {
                    _DataBytes = StreamToBytes(Data);
                }
                else
                {
                    while (true)
                    {
                        Chunk chunk = ReadChunk().Result;
                        if (chunk.Data != null && chunk.Data.Length > 0) _DataBytes = AppendBytes(_DataBytes, chunk.Data);
                        if (chunk.IsFinalChunk) break;
                    }
                }
            }
        }

        #endregion
    }
}
