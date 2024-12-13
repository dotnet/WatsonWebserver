namespace WatsonWebserver.Lite
{
    using System;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using CavemanTcp;
    using Timestamps;
    using WatsonWebserver.Core;

    /// <summary>
    /// Data extracted from an incoming HTTP request.
    /// </summary>
    public class HttpRequest : HttpRequestBase
    {
        #region Public-Members

        /// <summary>
        /// The stream containing request data.
        /// </summary>
        [JsonIgnore]
        public override Stream Data { get; set; } = new MemoryStream();

        /// <summary>
        /// Bytes from the DataStream property.  Using Data will fully read the DataStream property and thus it cannot be read again.
        /// </summary>
        public override byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes == null)
                {
                    if (Data != null && Data.CanRead && ContentLength > 0)
                    {
                        _DataAsBytes = ReadStream(Data, ContentLength);
                        return _DataAsBytes;
                    }
                    else
                    {
                        return _DataAsBytes;
                    }
                }
                else
                {
                    return _DataAsBytes;
                }
            }
        }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public override string DataAsString
        {
            get
            {
                if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                if (Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStream(Data, ContentLength);
                    if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                }
                return null;
            }
        }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private string _IpPort;
        private string _RequestHeader = null;  
        private byte[] _DataAsBytes = null; 
        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpRequest()
        {
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Create an HttpRequest.
        /// </summary>
        /// <param name="ipPort">IP:port of the requestor.</param>
        /// <param name="stream">Client stream.</param>
        /// <param name="requestHeader">Request header.</param>
        /// <returns>HttpRequest.</returns>
        public HttpRequest(string ipPort, Stream stream, string requestHeader)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (String.IsNullOrEmpty(requestHeader)) throw new ArgumentNullException(nameof(requestHeader));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            _IpPort = ipPort;
            _RequestHeader = requestHeader;
            Data = stream;

            Build();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public override async Task<Chunk> ReadChunk(CancellationToken token = default)
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
                chunk.IsFinal = false;
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
                chunk.IsFinal = true;
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
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if exists.</returns>
        public override bool HeaderExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Headers != null)
            {
                return Headers.AllKeys.Any(k => k.ToLower().Equals(key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public override bool QuerystringExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                return Query.Elements.AllKeys.Any(k => k.ToLower().Equals(key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public override string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Headers != null)
            {
                return Headers.Get(key);
            }

            return null;
        }

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public override string RetrieveQueryValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (Query != null
                && Query.Elements != null)
            {
                string val = Query.Elements.Get(key);
                if (!String.IsNullOrEmpty(val))
                {
                    val = WebUtility.UrlDecode(val);
                }

                return val;
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private void Build()
        { 
            #region Initial-Values

            Source = new SourceDetails(IpFromIpPort(_IpPort), PortFromIpPort(_IpPort));
            ThreadId = Thread.CurrentThread.ManagedThreadId; 
             
            #endregion

            #region Convert-to-String-List
             
            string[] headers = _RequestHeader.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            #endregion

            #region Process-Each-Line

            for (int i = 0; i < headers.Length; i++)
            {
                if (i == 0)
                {
                    #region First-Line

                    string[] requestLine = headers[i].Trim().Trim('\0').Split(' ');
                    if (requestLine.Length < 3) throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");
                    
                    string tempUrl = requestLine[1];
                    string tempPath = "";
                    if (tempUrl.ToLower().StartsWith("http"))
                    {
                        // absolute path
                        var modifiedUri = new UriBuilder(tempUrl);
                        tempPath = modifiedUri.Path;
                    }
                    else
                    {
                        // relative path
                        tempPath = tempUrl;
                    }

                    Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), requestLine[0], true); 
                    Url = new UrlDetails(null, tempPath);
                    Query = new QueryDetails(requestLine[1]);
                     
                    ProtocolVersion = requestLine[2]; 
                     
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
                            Keepalive = Convert.ToBoolean(val);
                        }
                        else if (keyEval.Equals("user-agent"))
                        {
                            Useragent = val;
                        }
                        else if (keyEval.Equals("content-length"))
                        {
                            ContentLength = Convert.ToInt32(val);
                        }
                        else if (keyEval.Equals("content-type"))
                        {
                            ContentType = val;
                        }
                        else if (keyEval.ToLower().Equals("x-amz-content-sha256"))
                        {
                            if (val.ToLower().Contains("streaming"))
                            {
                                ChunkedTransfer = true;
                            }
                        }

                        Headers.Add(key, val);
                    }

                    #endregion
                }
            }

            #endregion
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

        private byte[] ReadStream(Stream input, long contentLength)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");
            if (contentLength < 1) return Array.Empty<byte>();

            byte[] buffer = new byte[_StreamBufferSize];
            long bytesRemaining = contentLength;

            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while (bytesRemaining > 0)
                {
                    read = input.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        ms.Write(buffer, 0, read);
                        bytesRemaining -= read;
                    }
                }

                byte[] ret = ms.ToArray();
                return ret;
            }
        }

        private string IpFromIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                return ipPort.Substring(0, colonIndex);
            }

            return null;
        }

        private int PortFromIpPort(string ipPort)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                return Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }

            return 0;
        }

        #endregion
    }
}