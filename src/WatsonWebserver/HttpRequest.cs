using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using Timestamps;
using WatsonWebserver.Core;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP request.
    /// </summary>
    public class HttpRequest : HttpRequestBase
    {
        #region Public-Members

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public override Stream Data { get; set; } = new MemoryStream();
         
        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public override byte[] DataAsBytes
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
        public override string DataAsString
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
        public HttpListenerContext ListenerContext { get; set; }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private Uri _Uri = null;
        private byte[] _DataAsBytes = null;
        private ISerializationHelper _Serializer = null;
        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

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

            Headers = ctx.Request.Headers;
             
            for (int i = 0; i < Headers.Count; i++)
            {
                string key = Headers.GetKey(i);
                string[] vals = Headers.GetValues(i);

                if (String.IsNullOrEmpty(key)) continue;
                if (vals == null || vals.Length < 1) continue;

                if (key.ToLower().Equals("transfer-encoding"))
                {
                    if (vals.Contains("chunked", StringComparer.InvariantCultureIgnoreCase))
                        ChunkedTransfer = true;
                    if (vals.Contains("gzip", StringComparer.InvariantCultureIgnoreCase))
                        Gzip = true;
                    if (vals.Contains("deflate", StringComparer.InvariantCultureIgnoreCase))
                        Deflate = true;
                }
                else if (key.ToLower().Equals("x-amz-content-sha256"))
                {
                    if (vals.Contains("streaming", StringComparer.InvariantCultureIgnoreCase))
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
                        if (chunk.IsFinal) break;
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
    }
}
