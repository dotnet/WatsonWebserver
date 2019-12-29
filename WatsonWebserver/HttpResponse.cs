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

        /// <summary>
        /// Indicates whether or not chunked transfer encoding should be indicated in the response. 
        /// </summary>
        public bool ChunkedTransfer = false;

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
        public HttpResponse()
        {

        }

        internal HttpResponse(HttpRequest req, HttpListenerContext ctx, EventCallbacks events)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (events == null) throw new ArgumentNullException(nameof(events));

            _Request = req;
            _Context = ctx;
            _Response = _Context.Response;
            _Events = events;
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
            ret += "  Chunked Transfer   : " + ChunkedTransfer + Environment.NewLine;
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
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send()
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (!_HeadersSent) SendHeaders();

            await _OutputStream.FlushAsync();
            _OutputStream.Close();

            if (_Response != null) _Response.Close();
            return true;
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            ContentLength = contentLength;
            if (!_HeadersSent) SendHeaders();

            await _OutputStream.FlushAsync();
            _OutputStream.Close();

            if (_Response != null) _Response.Close();
            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string data)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (!_HeadersSent) SendHeaders();

            byte[] bytes = null;
            if (!String.IsNullOrEmpty(data))
            {
                bytes = Encoding.UTF8.GetBytes(data);
                _Response.ContentLength64 = bytes.Length;
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
                        await _OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // do nothing
                return false;
            }
            catch (Exception eInner)
            {
                _Events.ExceptionEncountered(_Request.SourceIp, _Request.SourcePort, eInner);
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                _OutputStream.Close();

                if (_Response != null) _Response.Close();
            }

            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] data)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (!_HeadersSent) SendHeaders();

            if (data != null && data.Length > 0) _Response.ContentLength64 = data.Length;
            else _Response.ContentLength64 = 0;

            try
            {
                if (_Request.Method != HttpMethod.HEAD)
                {
                    if (data != null && data.Length > 0)
                    {
                        await _OutputStream.WriteAsync(data, 0, (int)_Response.ContentLength64);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // do nothing
                return false;
            }
            catch (Exception eInner)
            {
                _Events.ExceptionEncountered(_Request.SourceIp, _Request.SourcePort, eInner);
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                _OutputStream.Close();

                if (_Response != null) _Response.Close();
            }

            return true;
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate.
        /// </summary>
        /// <param name="contentLength">Number of bytes to send.</param>
        /// <param name="stream">Stream containing the data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long contentLength, Stream stream)
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

                        while (bytesRemaining > 0)
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[4096];
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                await _OutputStream.WriteAsync(buffer, 0, bytesRead);
                                bytesRemaining -= bytesRead;
                            }
                        }

                        stream.Close();
                        stream.Dispose();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception eInner)
            {
                _Events.ExceptionEncountered(_Request.SourceIp, _Request.SourcePort, eInner);
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                _OutputStream.Close();

                if (_Response != null) _Response.Close();
            }

            return true;
        }

        /// <summary>
        /// Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection in-tact.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendChunk(byte[] chunk)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSent) SendHeaders();

            try
            {
                if (chunk == null || chunk.Length < 1) chunk = new byte[0];

                // byte[] packagedChunk = PackageChunk(chunk);
                // await _OutputStream.WriteAsync(packagedChunk, 0, packagedChunk.Length);
                await _OutputStream.WriteAsync(chunk, 0, chunk.Length);
            }
            catch (OperationCanceledException)
            {
                // do nothing
                return false;
            }
            catch (Exception eInner)
            {
                _Events.ExceptionEncountered(_Request.SourceIp, _Request.SourcePort, eInner);
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                // do not close or dispose
            }

            return true;
        }

        /// <summary>
        /// Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the connection.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendFinalChunk(byte[] chunk)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSent) SendHeaders();

            try
            { 
                if (chunk != null && chunk.Length > 0)
                {
                    await _OutputStream.WriteAsync(chunk, 0, chunk.Length);
                }

                byte[] endChunk = new byte[0];
                await _OutputStream.WriteAsync(endChunk, 0, endChunk.Length);
            }
            catch (OperationCanceledException)
            {
                // do nothing
                return false;
            }
            catch (Exception eInner)
            {
                _Events.ExceptionEncountered(_Request.SourceIp, _Request.SourcePort, eInner);
                return false;
            }
            finally
            {
                await _OutputStream.FlushAsync();
                _OutputStream.Close();

                if (_Response != null) _Response.Close();
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
            _Response.SendChunked = ChunkedTransfer;
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
