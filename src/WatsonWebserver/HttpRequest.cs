using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks; 

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
        [JsonPropertyOrder(-10)]
        public DateTime TimestampUtc { get; private set; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public int ThreadId { get; private set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public SourceDetails Source { get; set; } = new SourceDetails();

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public DestinationDetails Destination { get; set; } = new DestinationDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonPropertyOrder(-6)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The string version of the HTTP method, useful if Method is UNKNOWN.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public string MethodRaw { get; set; } = null;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public UrlDetails Url { get; set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public QueryDetails Query { get; set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public Dictionary<string, string> Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                else _Headers = value;
            }
        }

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; set; } = false;
         
        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long ContentLength { get; private set; } = 0;

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public Stream Data;
         
        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(Data);
                    return _DataAsBytes;
                }
                return null;
            }
        }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public string DataAsString
        {
            get
            {
                if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                if (Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(Data);
                    if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                }
                return null;
            }
        }

        /// <summary>
        /// The original HttpListenerContext from which the HttpRequest was constructed.
        /// </summary>
        [JsonIgnore]
        public HttpListenerContext ListenerContext;

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private Uri _Uri = null;
        private byte[] _DataAsBytes = null;
        private ISerializationHelper _Serializer = null;
        private Dictionary<string, string> _Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

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
        /// <param name="serializer">Serialization helper.</param>
        public HttpRequest(HttpListenerContext ctx, ISerializationHelper serializer)
        { 
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            _Serializer = serializer;

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
            Destination = new DestinationDetails(ctx.Request.LocalEndPoint.Address.ToString(), ctx.Request.LocalEndPoint.Port, _Uri.Host);
            Url = new UrlDetails(ctx.Request.Url.ToString().Trim(), ctx.Request.RawUrl.ToString().Trim()); 
            Query = new QueryDetails(Url.Full);
            MethodRaw = ctx.Request.HttpMethod;

            try
            {
                Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), ctx.Request.HttpMethod, true);
            }
            catch (Exception)
            {
                Method = HttpMethod.UNKNOWN;
            }

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
                else if (curr.Key.ToLower().Equals("x-amz-content-sha256"))
                {
                    if (curr.Value.ToLower().Contains("streaming"))
                    {
                        ChunkedTransfer = true;
                    }
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
        [Obsolete("This API will be deprecated in a future release.  Header dictionary is now case insensitive.")]
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
        [Obsolete("This API will be deprecated in a future release.  Header dictionary is now case insensitive.")]
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
        [Obsolete("This API will be deprecated in a future release.  Query elements dictionary is now case insensitive.")]
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
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public async Task<Chunk> ReadChunk(CancellationToken token = default)
        {
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
                            string[] lenParts = lenStr.Split(new char[] { ';' }, 2);
                            chunk.Length = int.Parse(lenParts[0], NumberStyles.HexNumber);
                            if (lenParts.Length >= 2) chunk.Metadata = lenParts[1];
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

            int bytesRemaining = chunk.Length;

            if (chunk.Length > 0)
            {
                chunk.IsFinalChunk = false;
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        if (bytesRemaining > _StreamBufferSize) buffer = new byte[_StreamBufferSize];
                        else buffer = new byte[bytesRemaining];

                        bytesRead = await Data.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                        if (bytesRead > 0)
                        {
                            await ms.WriteAsync(buffer, 0, bytesRead);
                            bytesRemaining -= bytesRead;
                        }

                        if (bytesRemaining == 0) break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    chunk.Data = ms.ToArray();
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
        /// Read the data stream fully and convert the data to the object type specified using JSON deserialization.
        /// Note: if you use this method, you will not be able to read from the data stream afterward.
        /// </summary>
        /// <typeparam name="T">Type.</typeparam>
        /// <returns>Object of type specified.</returns>
        public T DataAsJsonObject<T>() where T : class
        {
            string json = DataAsString;
            if (String.IsNullOrEmpty(json)) return null;
            return _Serializer.DeserializeJson<T>(json);
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

            if (_DataAsBytes == null)
            {
                if (!ChunkedTransfer)
                {
                    _DataAsBytes = StreamToBytes(Data);
                }
                else
                {
                    while (true)
                    {
                        Chunk chunk = ReadChunk().Result;
                        if (chunk.Data != null && chunk.Data.Length > 0) _DataAsBytes = AppendBytes(_DataAsBytes, chunk.Data);
                        if (chunk.IsFinalChunk) break;
                    }
                }
            }
        }

        private byte[] ReadStreamFully(Stream input)
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

                byte[] ret = ms.ToArray();
                return ret;
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
            public string IpAddress { get; set; } = null;

            /// <summary>
            /// TCP port from which the request originated on the requestor.
            /// </summary>
            public int Port { get; set; } = 0;

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
            public string IpAddress { get; set; } = null;

            /// <summary>
            /// TCP port on which the request was received.
            /// </summary>
            public int Port { get; set; } = 0;

            /// <summary>
            /// Hostname to which the request was directed.
            /// </summary>
            public string Hostname { get; set; } = null;

            /// <summary>
            /// Hostname elements.
            /// </summary>
            public string[] HostnameElements
            {
                get
                {
                    string hostname = Hostname;
                    string[] ret;

                    if (!String.IsNullOrEmpty(hostname))
                    {
                        if (!IPAddress.TryParse(hostname, out _))
                        {
                            ret = hostname.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                            return ret;
                        }
                        else
                        {
                            ret = new string[1];
                            ret[0] = hostname;
                            return ret;
                        }
                    }

                    ret = new string[0];
                    return ret;
                }
            }

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
            public DestinationDetails(string ip, int port, string hostname)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
                if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

                IpAddress = ip;
                Port = port;
                Hostname = hostname;
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
            public string Full { get; set; } = null;

            /// <summary>
            /// Raw URL with query.
            /// </summary>
            public string RawWithQuery { get; set; } = null;

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

                    string[] ret = new string[0];
                    return ret;
                }
            }

            /// <summary>
            /// Parameters found within the URL, if using parameter routes.
            /// </summary>
            public Dictionary<string, string> Parameters
            {
                get
                {
                    return _Parameters;
                }
                set
                {
                    if (value == null) _Parameters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    else _Parameters = value;
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

            private Dictionary<string, string> _Parameters = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
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
                    Dictionary<string, string> ret = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
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
            private Dictionary<string, string> _Elements = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        #endregion
    }
}
