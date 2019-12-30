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
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// Response to an HTTP request.
    /// </summary>
    public class HttpResponse
    {
        #region Public-Members

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        public int StatusCode = 200;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        public string StatusDescription = "OK";

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        public Dictionary<string, string> Headers = new Dictionary<string, string>();

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType = String.Empty;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength = 0;

        #endregion

        #region Private-Members

        private HttpRequest _Request;
        private HttpListenerContext _Context;
        private HttpListenerResponse _Response;
        private Stream _OutputStream;
        private bool _HeadersSent = false;

        private EventCallbacks _Events;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        private HttpResponse()
        {

        }

        internal HttpResponse(HttpRequest req, HttpListenerContext ctx, EventCallbacks events)
        {
            _Request = req ?? throw new ArgumentNullException(nameof(req));
            _Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _Response = _Context.Response;
            _Events = events ?? throw new ArgumentNullException(nameof(events));
            _OutputStream = _Response.OutputStream;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a string-formatted, human-readable copy of the HttpResponse instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpResponse instance.</returns>
        public override string ToString()
        {
            string ret = "";

            ret += "--- HTTP Response ---" + Environment.NewLine;
            ret += "  Status Code        : " + StatusCode + Environment.NewLine;
            ret += "  Status Description : " + StatusDescription + Environment.NewLine;
            ret += "  Content            : " + ContentType + Environment.NewLine;
            ret += "  Content Length     : " + ContentLength + " bytes" + Environment.NewLine;
            ret += "  Chunked Transfer   : " + true + Environment.NewLine;
            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers            : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "  - " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Headers          : none" + Environment.NewLine;
            }

            return ret;
        }

        /// <summary>
        ///   Send headers and no data to the requestor and terminate the connection.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send()
        {
            if (!_HeadersSent) SendHeaders();

            await _OutputStream.FlushAsync();
            _OutputStream.Close();

            _Response?.Close();
            return true;
        }

        /// <summary>
        /// Send headers (statusCode) and no data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="statusCode">StatusCode</param>
        /// <returns>True if successful.</returns>
        public Task<bool> Send(int statusCode)
        {
            StatusCode = statusCode;
            return Send();
        }

        /// <summary>
        /// Send headers (statusCode) and no data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="statusCode">StatusCode</param>
        /// <returns>True if successful.</returns>
        public Task<bool> Send(HttpStatusCode statusCode)
        {
          StatusCode = (int)statusCode;
          return Send();
        }

    /// <summary>
    /// Send headers (statusCode) and a error message to the requestor and terminate the connection.
    /// </summary>
    /// <param name="statusCode">StatusCode</param>
    /// <param name="errorMessage">Plaintext error message</param>
    /// <returns>True if successful.</returns>
    public Task<bool> Send(int statusCode, string errorMessage)
        {
            StatusCode = statusCode;
            ContentType = "text/plain";
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(errorMessage)))
                return Send(ms.Length, ms);
        }

        /// <summary>
        ///   Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <returns>True if successful.</returns>
        public Task<bool> Send(object obj)
        {
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj))))
                return Send(ms.Length, ms);
        }

        /// <summary>
        ///   Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for
        ///   HEAD requests where the content length must be set.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength)
        {
          ContentLength = contentLength;
          return await Send();
        }

        /// <summary>
        ///   Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="mimeType">Force a special MIME-Type</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string data, string mimeType = "application/json")
        {
            ContentType = mimeType;
            return await Send(Encoding.UTF8.GetBytes(data));
        }

        /// <summary>
        ///   Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                    await Send(data.Length, ms);
                return true;
            }
            catch
            {
                // do nothing
                return false;
            }
        }

        private const int _bufferSize = 1024 * 1024;

        /// <summary>
        ///   Send headers and data to the requestor and terminate.
        /// </summary>
        /// <param name="contentLength">Number of bytes to send.</param>
        /// <param name="stream">Stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, Stream stream)
        {
            if (!_HeadersSent)
                SendHeaders();
            ContentLength = contentLength;

            try
            {
                if (stream != null && stream.CanRead && contentLength > 0)
                {
                    var buffer = new byte[_bufferSize];
                    var read = 0;
                    do
                    {
                        read = stream.Read(buffer, 0, buffer.Length);
                        if (read > 0)
                            await SendChunk(buffer, read);
                    } while (read != 0);
                }
            }
            catch
            {
                // do nothing
                return false;
            }
            finally
            {
                await SendFinalChunk(null, 0);
            }

            return true;
        }

        /// <summary>
        ///   Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection
        ///   in-tact.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendChunk(byte[] chunk)
        {
            if (!_HeadersSent)
                SendHeaders();
            return await SendChunk(chunk, chunk.Length);
        }

        /// <summary>
        ///   Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection
        ///   in-tact.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        private async Task<bool> SendChunk(byte[] chunk, int length)
        {
            try
            {
                if (chunk == null || chunk.Length < 1)
                    chunk = new byte[0];
                _OutputStream.Write(chunk, 0, length);
            }
            catch
            {
                // do nothing
                return false;
            }

            return true;
        }

        /// <summary>
        ///   Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the
        ///   connection.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendFinalChunk(byte[] chunk)
        {
            if (!_HeadersSent)
                SendHeaders();

            return await SendFinalChunk(chunk, chunk.Length);
        }

        /// <summary>
        ///   Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the
        ///   connection.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        private async Task<bool> SendFinalChunk(byte[] chunk, int length)
        {
            try
            {
                if (chunk != null && length > 0) await _OutputStream.WriteAsync(chunk, 0, length);

                var endChunk = new byte[0];
                await _OutputStream.WriteAsync(endChunk, 0, endChunk.Length);
            }
            catch
            {
                // do nothing
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                _OutputStream.Close();

                _Response?.Close();
            }

            return true;
        }

        #endregion

        #region Private-Methods

        private void SendHeaders()
        {
            if (_HeadersSent) throw new IOException("Headers already sent.");

            _Response.ContentLength64 = ContentLength;
            _Response.StatusCode = StatusCode;
            _Response.StatusDescription = GetStatusDescription(StatusCode);
            _Response.SendChunked = true;
            _Response.AddHeader("Access-Control-Allow-Origin", "*");
            _Response.ContentType = ContentType;

            if (Headers != null && Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    _Response.AddHeader(curr.Key, curr.Value);
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

        #endregion
    }
}
