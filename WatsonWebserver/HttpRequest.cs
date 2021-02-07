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
    /// HTTP request.
    /// </summary>
    public class HttpRequest
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request was received.
        /// </summary>
        [JsonProperty(Order = -10)]
        public DateTime TimestampUtc { get; private set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonProperty(Order = -9)]
        public int ThreadId { get; private set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonProperty(Order = -8)]
        public string ProtocolVersion { get; private set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonProperty(Order = -7)]
        public SourceDetails Source { get; private set; } = new SourceDetails();

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonProperty(Order = -6)]
        public DestinationDetails Destination { get; private set; } = new DestinationDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonProperty(Order = -5)]
        public HttpMethod Method { get; private set; } = HttpMethod.GET;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonProperty(Order = -4)]
        public UrlDetails Url { get; private set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonProperty(Order = -3)]
        public QueryDetails Query { get; private set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonProperty(Order = -2)]
        public Dictionary<string, string> Headers { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; private set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; private set; } = false;
         
        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; private set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonProperty(Order = 990)]
        public string ContentType { get; private set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonProperty(Order = 991)]
        public long ContentLength { get; private set; } = 0;

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

        private Uri _Uri = null;
        private byte[] _DataBytes = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// HTTP request.
        /// </summary>
        public HttpRequest()
        { 
        }

        /// <summary>
        /// HTTP request.
        /// Instantiate the object using an HttpListenerContext.
        /// </summary>
        /// <param name="ctx">HttpListenerContext.</param>
        public HttpRequest(HttpListenerContext ctx)
        { 
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
             
            ListenerContext = ctx; 
            Keepalive = ctx.Request.KeepAlive;
            ContentLength = ctx.Request.ContentLength64;
            Useragent = ctx.Request.UserAgent;
            ContentType = ctx.Request.ContentType;

            _Uri = new Uri(ctx.Request.Url.ToString().Trim()); 

            ThreadId = Thread.CurrentThread.ManagedThreadId;
            TimestampUtc = DateTime.Now.ToUniversalTime();
            ProtocolVersion = "HTTP/" + ctx.Request.ProtocolVersion.ToString(); 
            Source = new SourceDetails(ctx.Request.RemoteEndPoint.Address.ToString(), ctx.Request.RemoteEndPoint.Port);
            Destination = new DestinationDetails(ctx.Request.LocalEndPoint.Address.ToString(), ctx.Request.LocalEndPoint.Port, _Uri.Host, _Uri.Port);
            Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), ctx.Request.HttpMethod, true); 
            Url = new UrlDetails(ctx.Request.Url.ToString().Trim(), ctx.Request.RawUrl.ToString().Trim()); 
            Query = new QueryDetails(Url.Full);
              
            Headers = new Dictionary<string, string>();
            for (int i = 0; i < ctx.Request.Headers.Count; i++)
            {
                string key = ctx.Request.Headers.GetKey(i);
                string val = ctx.Request.Headers.Get(i);
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
              
            Data = ctx.Request.InputStream;
        }

        #endregion

        #region Public-Methods
         
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

            if (Query != null && Query.Elements != null && Query.Elements.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Query.Elements)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            return null;
        }
        
        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <param name="caseSensitive">Specify whether a case sensitive search should be used.</param>
        /// <returns>True if exists.</returns>
        public bool HeaderExists(string key, bool caseSensitive)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Headers != null && Headers.Count > 0)
            {
                if (caseSensitive)
                {
                    return Headers.ContainsKey(key);
                }
                else
                { 
                    foreach (KeyValuePair<string, string> header in Headers)
                    {
                        if (String.IsNullOrEmpty(header.Key)) continue;
                        if (header.Key.ToLower().Trim().Equals(key)) return true;
                    } 
                }
            }

            return false;
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <param name="caseSensitive">Specify whether a case sensitive search should be used.</param>
        /// <returns>True if exists.</returns>
        public bool QuerystringExists(string key, bool caseSensitive)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null && Query.Elements != null && Query.Elements.Count > 0)
            {
                if (caseSensitive)
                {
                    return Query.Elements.ContainsKey(key);
                }
                else
                { 
                    foreach (KeyValuePair<string, string> queryElement in Query.Elements)
                    {
                        if (String.IsNullOrEmpty(queryElement.Key)) continue;
                        if (queryElement.Key.ToLower().Trim().Equals(key)) return true;
                    } 
                }
            }

            return false;
        }

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public async Task<Chunk> ReadChunk(CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Request is not chunk transfer-encoded.");

            Chunk chunk = new Chunk();
             
            #region Get-Length-and-Metadata

            byte[] buffer = new byte[1];
            byte[] lenBytes = null;
            int bytesRead = 0;

            while (true)
            {
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
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
                         
                        break;
                    }
                }
            }

            #endregion

            #region Get-Data
             
            if (chunk.Length > 0)
            {
                chunk.IsFinalChunk = false;
                buffer = new byte[chunk.Length];
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead == chunk.Length)
                {
                    chunk.Data = new byte[chunk.Length];
                    Buffer.BlockCopy(buffer, 0, chunk.Data, 0, chunk.Length); 
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
                bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
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

        private byte[] AppendBytes(byte[] orig, byte[] append)
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

        #region Embedded-Classes

        /// <summary>
        /// Source details.
        /// </summary>
        public class SourceDetails
        {
            /// <summary>
            /// IP address of the requestor.
            /// </summary>
            public string IpAddress { get; private set; } = null;

            /// <summary>
            /// TCP port from which the request originated on the requestor.
            /// </summary>
            public int Port { get; private set; } = 0;

            /// <summary>
            /// Source details.
            /// </summary>
            public SourceDetails()
            {

            }

            /// <summary>
            /// Source details.
            /// </summary>
            /// <param name="ip">IP address of the requestor.</param>
            /// <param name="port">TCP port from which the request originated on the requestor.</param>
            public SourceDetails(string ip, int port)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

                IpAddress = ip;
                Port = port;
            }
        }

        /// <summary>
        /// Destination details.
        /// </summary>
        public class DestinationDetails
        {
            /// <summary>
            /// IP address to which the request was made.
            /// </summary>
            public string IpAddress { get; private set; } = null;

            /// <summary>
            /// TCP port on which the request was received.
            /// </summary>
            public int Port { get; private set; } = 0;

            /// <summary>
            /// Hostname to which the request was directed.
            /// </summary>
            public string Hostname { get; private set; } = null;

            /// <summary>
            /// Host port to which the request was directed.
            /// </summary>
            public int HostPort { get; private set; } = 0;

            /// <summary>
            /// Destination details.
            /// </summary>
            public DestinationDetails()
            {

            }

            /// <summary>
            /// Source details.
            /// </summary>
            /// <param name="ip">IP address to which the request was made.</param>
            /// <param name="port">TCP port on which the request was received.</param>
            /// <param name="hostname">Hostname.</param>
            /// <param name="hostPort">Host TCP port.</param>
            public DestinationDetails(string ip, int port, string hostname, int hostPort)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
                if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
                if (hostPort < 0) throw new ArgumentOutOfRangeException(nameof(hostPort));

                IpAddress = ip;
                Port = port;
                Hostname = hostname;
                HostPort = hostPort;
            }
        }

        /// <summary>
        /// URL details.
        /// </summary>
        public class UrlDetails
        {
            /// <summary>
            /// Full URL.
            /// </summary>
            public string Full { get; private set; } = null;

            /// <summary>
            /// Raw URL with query.
            /// </summary>
            public string RawWithQuery { get; private set; } = null;

            /// <summary>
            /// Raw URL without query.
            /// </summary>
            public string RawWithoutQuery
            {
                get
                {
                    if (!String.IsNullOrEmpty(RawWithQuery))
                    {
                        if (RawWithQuery.Contains("?")) return RawWithQuery.Substring(0, RawWithQuery.IndexOf("?"));
                        else return RawWithQuery;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Raw URL elements.
            /// </summary>
            public string[] Elements
            {
                get
                { 
                    string rawUrl = RawWithoutQuery;

                    if (!String.IsNullOrEmpty(rawUrl))
                    {
                        while (rawUrl.Contains("//")) rawUrl = rawUrl.Replace("//", "/");
                        while (rawUrl.StartsWith("/")) rawUrl = rawUrl.Substring(1);
                        while (rawUrl.EndsWith("/")) rawUrl = rawUrl.Substring(0, rawUrl.Length - 1);
                        string[] encoded = rawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (encoded != null && encoded.Length > 0)
                        {
                            string[] decoded = new string[encoded.Length];
                            for (int i = 0; i < encoded.Length; i++)
                            {
                                decoded[i] = WebUtility.UrlDecode(encoded[i]);
                            }

                            return decoded;
                        }
                    }

                    return null;
                }
            }

            /// <summary>
            /// URL details.
            /// </summary>
            public UrlDetails()
            {

            }

            /// <summary>
            /// URL details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param>
            /// <param name="rawUrl">Raw URL.</param>
            public UrlDetails(string fullUrl, string rawUrl)
            {
                if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));
                if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

                Full = fullUrl;
                RawWithQuery = rawUrl;
            }
        }
        
        /// <summary>
        /// Query details.
        /// </summary>
        public class QueryDetails
        {
            /// <summary>
            /// Querystring, excluding the leading '?'.
            /// </summary>
            public string Querystring
            {
                get
                {
                    if (_FullUrl.Contains("?"))
                    {
                        return _FullUrl.Substring(_FullUrl.IndexOf("?") + 1, (_FullUrl.Length - _FullUrl.IndexOf("?") - 1));
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Query elements.
            /// </summary>
            public Dictionary<string, string> Elements
            {
                get
                {
                    Dictionary<string, string> ret = new Dictionary<string, string>();
                    string qs = Querystring;
                    if (!String.IsNullOrEmpty(qs))
                    {
                        string[] queries = qs.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                        if (queries.Length > 0)
                        {
                            for (int i = 0; i < queries.Length; i++)
                            {
                                string[] queryParts = queries[i].Split('=');
                                if (queryParts != null && queryParts.Length == 2)
                                {
                                    ret = AddToDict(queryParts[0], queryParts[1], ret);
                                }
                                else if (queryParts != null && queryParts.Length == 1)
                                {
                                    ret = AddToDict(queryParts[0], null, ret);
                                }
                            }
                        }
                    }

                    return ret;
                }
            }

            /// <summary>
            /// Query details.
            /// </summary>
            public QueryDetails()
            {

            }

            /// <summary>
            /// Query details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param>
            public QueryDetails(string fullUrl)
            {
                if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));

                _FullUrl = fullUrl;
            }

            private string _FullUrl = null;
        }

        #endregion
    }
}
