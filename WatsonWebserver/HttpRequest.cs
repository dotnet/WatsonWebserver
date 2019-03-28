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
using Newtonsoft;
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
        /// The request body as sent by the requestor (client).
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// The original HttpListenerContext from which the HttpRequest was constructed.
        /// </summary>
        [JsonIgnore]
        public HttpListenerContext ListenerContext;

        #endregion

        #region Private-Members

        private Uri _Uri;
        private static int TimeoutDataReadMs = 2000;
        private static int DataReadSleepMs = 10;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Construct a new HTTP request.
        /// </summary>
        public HttpRequest()
        {
            QuerystringEntries = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Construct a new HTTP request from a given HttpListenerContext.
        /// </summary>
        /// <param name="ctx">The HttpListenerContext for the request.</param>
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

                                if (!String.IsNullOrEmpty(tempVal)) tempVal = System.Uri.EscapeUriString(tempVal);
                                QuerystringEntries = WatsonCommon.AddToDict(tempKey, tempVal, QuerystringEntries);
                                
                                tempKey = "";
                                tempVal = "";
                                position++;
                                continue;
                            }
                        }
                    }

                    if (inVal == 1)
                    {
                        if (!String.IsNullOrEmpty(tempVal)) tempVal = System.Uri.EscapeUriString(tempVal);
                        QuerystringEntries = WatsonCommon.AddToDict(tempKey, tempVal, QuerystringEntries);
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
                Headers = WatsonCommon.AddToDict(key, val, Headers);
            }

            #endregion

            #region Copy-Payload

            if (ContentLength > 0)
            {
                if (Method != HttpMethod.GET 
                    && Method != HttpMethod.HEAD)
                {
                    try
                    {
                        if (ContentLength < 1)
                        {
                            Data = null;
                        }
                        else
                        {
                            Data = new byte[ContentLength];
                            Stream bodyStream = ctx.Request.InputStream;
                            Data = WatsonCommon.StreamToBytes(bodyStream);
                        }
                    }
                    catch (Exception)
                    {
                        Data = null;
                    }
                }
            }

            #endregion
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
            int contentLength = 0;
            if (Data != null)
            {
                contentLength = Data.Length;
            }

            ret += "--- HTTP Request ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + Environment.NewLine;
            ret += "  " + Method + " " + RawUrlWithoutQuery + " " + ProtocolVersion + Environment.NewLine;
            ret += "  Full URL    : " + FullUrl + Environment.NewLine;
            ret += "  Raw URL     : " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Querystring : " + Querystring + Environment.NewLine;
            ret += "  Useragent   : " + Useragent + " (Keepalive " + Keepalive + ")" + Environment.NewLine;
            ret += "  Content     : " + ContentType + " (" + contentLength + " bytes)" + Environment.NewLine;
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

            if (Data != null)
            {
                ret += "  Data        : " + Environment.NewLine;
                ret += Encoding.UTF8.GetString(Data) + Environment.NewLine;
            }
            else
            {
                ret += "  Data        : [null]" + Environment.NewLine;
            }

            return ret;
        }

        /// <summary>
        /// Retrieve a specified header value from either the headers or the querystring.
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
        /// Retrieve the integer value of the last raw URL element, if found.
        /// </summary>
        /// <returns>A nullable integer.</returns>
        public int? RetrieveIdValue()
        {
            if (RawUrlEntries == null || RawUrlEntries.Count < 1) return null;
            string[] entries = RawUrlEntries.ToArray();
            int len = entries.Length;
            string entry = entries[(len - 1)];
            int ret;
            if (Int32.TryParse(entry, out ret))
            {
                return ret;
            }
            return null;
        }

        /// <summary>
        /// Create an HttpRequest object from a byte array.
        /// </summary>
        /// <param name="bytes">Byte data.</param>
        /// <returns>A populated HttpRequest.</returns>
        public static HttpRequest FromBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < 4) throw new ArgumentException("Too few bytes supplied to form a valid HTTP request.");

            bool endOfHeader = false;
            byte[] headerBytes = new byte[1]; 

            HttpRequest ret = new HttpRequest();

            for (int i = 0; i < bytes.Length; i++)
            {
                if (headerBytes.Length == 1)
                {
                    #region First-Byte

                    headerBytes[0] = bytes[i];
                    continue;

                    #endregion
                }

                if (!endOfHeader && headerBytes.Length < 4)
                {
                    #region Fewer-Than-Four-Bytes

                    byte[] tempHeader = new byte[i + 1];
                    Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                    tempHeader[i] = bytes[i];
                    headerBytes = tempHeader;
                    continue;

                    #endregion
                }

                if (!endOfHeader)
                {
                    #region Check-for-End-of-Header

                    // check if end of headers reached
                    if (
                        (int)headerBytes[(headerBytes.Length - 1)] == 10
                        && (int)headerBytes[(headerBytes.Length - 2)] == 13
                        && (int)headerBytes[(headerBytes.Length - 3)] == 10
                        && (int)headerBytes[(headerBytes.Length - 4)] == 13
                        )
                    {
                        #region End-of-Header

                        // end of headers reached
                        endOfHeader = true;
                        ret = BuildHeaders(headerBytes);

                        #endregion
                    }
                    else
                    {
                        #region Still-Reading-Header

                        byte[] tempHeader = new byte[i + 1];
                        Buffer.BlockCopy(headerBytes, 0, tempHeader, 0, headerBytes.Length);
                        tempHeader[i] = bytes[i];
                        headerBytes = tempHeader;
                        continue;

                        #endregion
                    }

                    #endregion
                }
                else
                {
                    if (ret.ContentLength > 0)
                    {
                        #region Append-Data

                        //           1         2
                        // 01234567890123456789012345
                        // content-length: 5rnrnddddd
                        // bytes.length = 26
                        // i = 21

                        if (ret.ContentLength != (bytes.Length - i))
                        {
                            throw new ArgumentException("Content-Length header does not match the number of data bytes.");
                        }

                        ret.Data = new byte[ret.ContentLength];
                        Buffer.BlockCopy(bytes, i, ret.Data, 0, (int)ret.ContentLength);
                        break;

                        #endregion
                    }
                    else
                    {
                        #region No-Data

                        ret.Data = null;
                        break;

                        #endregion
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Create an HttpRequest object from a Stream.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <returns>A populated HttpRequest.</returns>
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

                    ret.Data = new byte[ret.ContentLength];

                    using (MemoryStream dataMs = new MemoryStream())
                    {
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
                                dataMs.Write(buffer, 0, read);
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
                                if (currentTimeout >= TimeoutDataReadMs)
                                {
                                    timeout = true;
                                    break;
                                }
                                else
                                {
                                    currentTimeout += DataReadSleepMs;
                                    Thread.Sleep(DataReadSleepMs);
                                }
                            }
                        }

                        if (timeout)
                        {
                            throw new IOException("Timeout reading data from stream.");
                        }

                        ret.Data = dataMs.ToArray();
                    }

                    #endregion

                    #region Validate-Data

                    if (ret.Data == null || ret.Data.Length < 1)
                    {
                        throw new IOException("Unable to read data from stream.");
                    }

                    if (ret.Data.Length != ret.ContentLength)
                    {
                        throw new IOException("Data read does not match specified content length.");
                    }

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
        /// Create an HttpRequest object from a NetworkStream.
        /// </summary>
        /// <param name="stream">NetworkStream.</param>
        /// <returns>A populated HttpRequest.</returns>
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

                    ret.Data = new byte[ret.ContentLength];

                    using (MemoryStream dataMs = new MemoryStream())
                    {
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
                                dataMs.Write(buffer, 0, read);
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
                                if (currentTimeout >= TimeoutDataReadMs)
                                {
                                    timeout = true;
                                    break;
                                }
                                else
                                {
                                    currentTimeout += DataReadSleepMs;
                                    Thread.Sleep(DataReadSleepMs);
                                }
                            }
                        }

                        if (timeout)
                        {
                            throw new IOException("Timeout reading data from stream.");
                        }

                        ret.Data = dataMs.ToArray();
                    }

                    #endregion

                    #region Validate-Data

                    if (ret.Data == null || ret.Data.Length < 1)
                    {
                        throw new IOException("Unable to read data from stream.");
                    }

                    if (ret.Data.Length != ret.ContentLength)
                    {
                        throw new IOException("Data read does not match specified content length.");
                    }

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
        /// Create an HttpRequest object from a TcpClient.
        /// </summary>
        /// <param name="client">TcpClient.</param>
        /// <returns>A populated HttpRequest.</returns>
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

                    ret.Data = new byte[ret.ContentLength];

                    using (MemoryStream dataMs = new MemoryStream())
                    {
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
                                dataMs.Write(buffer, 0, read);
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
                                if (currentTimeout >= TimeoutDataReadMs)
                                {
                                    timeout = true;
                                    break;
                                }
                                else
                                {
                                    currentTimeout += DataReadSleepMs;
                                    Thread.Sleep(DataReadSleepMs);
                                }
                            }
                        }

                        if (timeout)
                        {
                            throw new IOException("Timeout reading data from stream.");
                        }

                        ret.Data = dataMs.ToArray();
                    }

                    #endregion

                    #region Validate-Data

                    if (ret.Data == null || ret.Data.Length < 1)
                    {
                        throw new IOException("Unable to read data from stream.");
                    }

                    if (ret.Data.Length != ret.ContentLength)
                    {
                        throw new IOException("Data read does not match specified content length.");
                    }

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
                        else
                        {
                            ret.Headers = WatsonCommon.AddToDict(key, val, ret.Headers);
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

                        if (!String.IsNullOrEmpty(tempVal)) tempVal = System.Uri.EscapeUriString(tempVal);
                        ret = WatsonCommon.AddToDict(tempKey, tempVal, ret);

                        tempKey = "";
                        tempVal = "";
                        position++;
                        continue;
                    }
                }
                
                if (inVal == 1)
                {
                    if (!String.IsNullOrEmpty(tempVal)) tempVal = System.Uri.EscapeUriString(tempVal);
                    ret = WatsonCommon.AddToDict(tempKey, tempVal, ret);
                }
            }

            return ret;
        }

        #endregion
    }
}
