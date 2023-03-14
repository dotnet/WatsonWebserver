using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP response.
    /// </summary>
    public class HttpResponse
    {
        #region Public-Members

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-3)]
        public int StatusCode = 200;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-2)]
        public string StatusDescription = "OK";

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public NameValueCollection Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Headers = value;
            }
        }

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType = String.Empty;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength = 0;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding should be indicated in the response. 
        /// </summary>
        public bool ChunkedTransfer = false;
        
        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
        /// </summary>
        [JsonIgnore]
        public string DataAsString
        {
            get
            {
                if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                if (_Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(_Data);
                    if (_DataAsBytes != null) return Encoding.UTF8.GetString(_DataAsBytes);
                }
                return null;
            }
        }

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
        /// </summary>
        [JsonIgnore]
        public byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (_Data != null && ContentLength > 0)
                {
                    _DataAsBytes = ReadStreamFully(_Data);
                    return _DataAsBytes;
                }
                return null;
            }
        }

        /// <summary>
        /// Response data stream sent to the requestor.
        /// </summary>
        [JsonIgnore]
        public MemoryStream Data
        {
            get
            {
                return _Data;
            }
        }

        #endregion

        #region Internal-Members

        internal bool ResponseSent
        {
            get
            {
                return _ResponseSent;
            } 
        }

        #endregion

        #region Private-Members

        private HttpRequest _Request = null;
        private HttpListenerContext _Context = null;
        private HttpListenerResponse _Response = null;
        private Stream _OutputStream = null;
        private bool _HeadersSent = false;
        private bool _ResponseSent = false;

        private WatsonWebserverSettings _Settings = new WatsonWebserverSettings();
        private WatsonWebserverEvents _Events = new WatsonWebserverEvents();

        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        private byte[] _DataAsBytes = null;
        private MemoryStream _Data = null;
        private ISerializationHelper _Serializer = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(
            HttpRequest req, 
            HttpListenerContext ctx, 
            WatsonWebserverSettings settings, 
            WatsonWebserverEvents events,
            ISerializationHelper serializer)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));

            _Serializer = serializer;
            _Request = req;
            _Context = ctx;
            _Response = _Context.Response;
            _Settings = settings;
            _Events = events; 

            _OutputStream = _Response.OutputStream;
        }

        #endregion

        #region Public-Methods
         
        /// <summary>
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");

            try
            {
                if (!_HeadersSent) SendHeaders();

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            } 
            catch (Exception)
            {
                return false;
            } 
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <param name="contentLength">Content length.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            ContentLength = contentLength;

            try
            {
                if (!_HeadersSent) SendHeaders();

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            } 
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (!_HeadersSent) SendHeaders();

            byte[] bytes = null;

            if (!String.IsNullOrEmpty(data))
            {
                bytes = Encoding.UTF8.GetBytes(data);

                _Data = new MemoryStream();
                await _Data.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
                _Data.Seek(0, SeekOrigin.Begin);

                _Response.ContentLength64 = bytes.Length;
                ContentLength = bytes.Length;
            }
            else
            {
                _Response.ContentLength64 = 0;
            }

            try
            {
                if (_Request.Method != HttpMethod.HEAD)
                {
                    if (bytes != null && bytes.Length > 0)
                    {
                        await _OutputStream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
                    }
                }

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (!_HeadersSent) SendHeaders();

            if (data != null && data.Length > 0)
            {
                _Data = new MemoryStream();
                await _Data.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                _Data.Seek(0, SeekOrigin.Begin);

                _Response.ContentLength64 = data.Length;
                ContentLength = data.Length;
            }
            else
            {
                _Response.ContentLength64 = 0;
            }

            try
            {
                if (_Request.Method != HttpMethod.HEAD)
                {
                    if (data != null && data.Length > 0)
                    {
                        await _OutputStream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                    }
                }

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            } 
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate.
        /// </summary>
        /// <param name="contentLength">Number of bytes to send.</param>
        /// <param name="stream">Stream containing the data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            ContentLength = contentLength;
            if (!_HeadersSent) SendHeaders();

            try
            {
                if (_Request.Method != HttpMethod.HEAD)
                {
                    if (stream != null && stream.CanRead && contentLength > 0)
                    {
                        long bytesRemaining = contentLength;

                        _Data = new MemoryStream();

                        while (bytesRemaining > 0)
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[_Settings.IO.StreamBufferSize];
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                            if (bytesRead > 0)
                            {
                                await _Data.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                                await _OutputStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                                bytesRemaining -= bytesRead;
                            }
                        }

                        stream.Close();
                        stream.Dispose();

                        _Data.Seek(0, SeekOrigin.Begin);
                    }
                }

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection in-tact.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <param name="numBytes">Number of bytes to send from the chunk, i.e. the actual data size (for example, return value of FileStream.ReadAsync(buffer, 0, buffer.Length)).</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendChunk(byte[] chunk, int numBytes, CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSent) SendHeaders();

            if (chunk != null && chunk.Length > 0)
                ContentLength += chunk.Length;

            try
            {
                if (chunk == null || chunk.Length < 1) chunk = new byte[0];
                await _OutputStream.WriteAsync(chunk, 0, numBytes, token).ConfigureAwait(false);
                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the connection.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>/// <param name="numBytes">Number of bytes to send from the chunk, i.e. the actual data size (for example, return value of FileStream.ReadAsync(buffer, 0, buffer.Length)).</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendFinalChunk(byte[] chunk, int numBytes, CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSent) SendHeaders();

            if (chunk != null && chunk.Length > 0)
                ContentLength += chunk.Length;

            try
            { 
                if (chunk != null && chunk.Length > 0)
                    await _OutputStream.WriteAsync(chunk, 0, numBytes, token).ConfigureAwait(false);

                byte[] endChunk = new byte[0];
                await _OutputStream.WriteAsync(endChunk, 0, endChunk.Length, token).ConfigureAwait(false);

                await _OutputStream.FlushAsync(token).ConfigureAwait(false);
                _OutputStream.Close();

                if (_Response != null) _Response.Close();

                _ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Convert the response data sent using a Send() method to the object type specified using JSON deserialization.
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

        private void SendHeaders()
        {
            if (_HeadersSent) throw new IOException("Headers already sent.");

            _Response.ContentLength64 = ContentLength;
            _Response.StatusCode = StatusCode;
            _Response.StatusDescription = GetStatusDescription(StatusCode);
            _Response.SendChunked = ChunkedTransfer;
            _Response.ContentType = ContentType;

            if (Headers != null && Headers.Count > 0)
            {
                for (int i = 0; i < Headers.Count; i++)
                {
                    string key = Headers.GetKey(i);
                    string[] vals = Headers.GetValues(i);

                    if (vals == null || vals.Length < 1)
                    {
                        _Response.AddHeader(key, null);
                    }
                    else
                    {
                        for (int j = 0; j < vals.Length; j++)
                        {
                            _Response.AddHeader(key, vals[j]);
                        }
                    }
                }
            }

            if (_Settings.Headers != null && _Settings.Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> header in _Settings.Headers)
                {
                    if (Headers.Get(header.Key) != null || Headers.AllKeys.Contains(header.Key))
                    {
                        // already present
                    }
                    else
                    {
                        _Response.AddHeader(header.Key, header.Value);
                    }
                }
            }

            _HeadersSent = true;
        }

        private string GetStatusDescription(int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Moved Temporarily";
                case 304:
                    return "Not Modified";
                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 429:
                    return "Too Many Requests";
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 503:
                    return "Service Unavailable";
                default:
                    return "Unknown Status";
            }
        }

        private byte[] PackageChunk(byte[] chunk)
        {
            if (chunk == null || chunk.Length < 1)
            {
                return Encoding.UTF8.GetBytes("0\r\n\r\n");
            }
             
            MemoryStream ms = new MemoryStream();
             
            string newlineStr = "\r\n";
            byte[] newline = Encoding.UTF8.GetBytes(newlineStr);

            string chunkLenHex = chunk.Length.ToString("X");
            byte[] chunkLen = Encoding.UTF8.GetBytes(chunkLenHex); 

            ms.Write(chunkLen, 0, chunkLen.Length);
            ms.Write(newline, 0, newline.Length);
            ms.Write(chunk, 0, chunk.Length);
            ms.Write(newline, 0, newline.Length);
            ms.Seek(0, SeekOrigin.Begin);

            byte[] ret = ms.ToArray();
             
            return ret;
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
