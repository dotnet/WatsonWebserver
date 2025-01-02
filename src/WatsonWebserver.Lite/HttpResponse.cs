namespace WatsonWebserver.Lite
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using CavemanTcp;
    using WatsonWebserver.Core;

    /// <summary>
    /// Response to an HTTP request.
    /// </summary>
    public class HttpResponse : HttpResponseBase
    {
        #region Public-Members

        /// <summary>
        /// Retrieve the response body sent using a Send() or Send() method.
        /// </summary>
        [JsonIgnore]
        public override string DataAsString
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
        /// Retrieve the response body sent using a Send() or Send() method.
        /// </summary>
        [JsonIgnore]
        public override byte[] DataAsBytes
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
        public override MemoryStream Data
        {
            get
            {
                return _Data;
            }
        }

        #endregion

        #region Private-Members

        private bool _HeadersSet = false;
        private bool _HeadersSent = false;
        private byte[] _DataAsBytes = null;
        private MemoryStream _Data = null;
        private string _IpPort;
        private WebserverSettings.HeaderSettings _HeaderSettings = null;
        private int _StreamBufferSize = 65536;
        private Stream _Stream;
        private HttpRequestBase _Request;  
        private WebserverEvents _Events = new WebserverEvents();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(
            string ipPort, 
            WebserverSettings.HeaderSettings headers, 
            Stream stream, 
            HttpRequestBase req, 
            WebserverEvents events, 
            int bufferSize)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (headers == null) throw new ArgumentNullException(nameof(headers));
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (events == null) throw new ArgumentNullException(nameof(events));

            ProtocolVersion = req.ProtocolVersion;

            _IpPort = ipPort;
            _HeaderSettings = headers;
            _Request = req;
            _Stream = stream;
            _Events = events;
            _StreamBufferSize = bufferSize; 
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override async Task<bool> Send(CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            return await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(long contentLength, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            ContentLength = contentLength;
            return await SendInternalAsync(0, null, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(string data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (String.IsNullOrEmpty(data))
                return await SendInternalAsync(0, null, true, token).ConfigureAwait(false);

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return await SendInternalAsync(bytes.Length, ms, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(byte[] data, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (data == null || data.Length < 1)
                return await SendInternalAsync(0, null, true, token).ConfigureAwait(false);

            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin); 
            return await SendInternalAsync(data.Length, ms, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> Send(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            if (contentLength <= 0 || stream == null || !stream.CanRead)
                return await SendInternalAsync(0, null, true, token).ConfigureAwait(false);

            return await SendInternalAsync(contentLength, stream, true, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> SendChunk(byte[] chunk, bool isFinal, CancellationToken token = default)
        {
            if (!ChunkedTransfer) throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            if (!_HeadersSet) SetDefaultHeaders();

            if (chunk != null && chunk.Length > 0)
                ContentLength += chunk.Length;

            try
            {
                if (chunk == null || chunk.Length < 1) chunk = Array.Empty<byte>();

                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] message = AppendBytes(Encoding.UTF8.GetBytes(chunk.Length.ToString("X") + "\r\n"), chunk);
                    message = AppendBytes(message, Encoding.UTF8.GetBytes("\r\n"));
                    if (isFinal) message = AppendBytes(message, Encoding.UTF8.GetBytes("0\r\n\r\n"));
                    await ms.WriteAsync(message, 0, message.Length, token).ConfigureAwait(false);
                    ms.Seek(0, SeekOrigin.Begin);
                    await SendInternalAsync(message.Length, ms, isFinal, token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override async Task<bool> SendEvent(string eventData, bool isFinal, CancellationToken token = default)
        {
            if (!ServerSentEvents) throw new IOException("Response is not configured to use server-sent events.  Set ServerSentEvents to true first, otherwise use Send().");
            if (!_HeadersSet) SetDefaultHeaders();

            if (!String.IsNullOrEmpty(eventData)) 
                ContentLength += eventData.Length;

            try
            {
                if (String.IsNullOrEmpty(eventData)) eventData = "";

                string dataLine = "data: " + eventData + "\n\n";

                byte[] message = Encoding.UTF8.GetBytes(
                    Encoding.UTF8.GetBytes(dataLine).Length.ToString("X") 
                    + "\r\n"
                    + dataLine 
                    + "\r\n");

                using (MemoryStream ms = new MemoryStream())
                {
                    await ms.WriteAsync(message, 0, message.Length, token).ConfigureAwait(false);

                    if (isFinal)
                    {
                        byte[] finalBytes = Encoding.UTF8.GetBytes("0\r\n\r\n");
                        await ms.WriteAsync(finalBytes, 0, finalBytes.Length, token).ConfigureAwait(false);
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    byte[] bytes = ms.ToArray();
                    await SendInternalAsync(bytes.Length, ms, isFinal, token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Close the connection.
        /// </summary>
        public void Close()
        {
            SendInternalAsync(0, null, true).Wait();
            ResponseSent = true;
        }

        #endregion

        #region Private-Methods

        private byte[] GetHeaderBytes()
        {
            byte[] ret = Array.Empty<byte>();

            ret = AppendBytes(ret, Encoding.UTF8.GetBytes(ProtocolVersion + " " + StatusCode + " " + GetStatusDescription(StatusCode) + "\r\n"));

            bool contentTypeSet = false;
            if (!String.IsNullOrEmpty(ContentType))
            {
                ret = AppendBytes(ret, Encoding.UTF8.GetBytes(WebserverConstants.HeaderContentType + ": " + ContentType + "\r\n"));
                contentTypeSet = true;
            }

            bool contentLengthSet = false;
            if (!ChunkedTransfer && ContentLength >= 0)
            {
                ret = AppendBytes(ret, Encoding.UTF8.GetBytes(WebserverConstants.HeaderContentLength + ": " + ContentLength + "\r\n"));
                contentLengthSet = true;
            }

            bool transferEncodingSet = false;
            if (ChunkedTransfer)
            {
                ret = AppendBytes(ret, Encoding.UTF8.GetBytes(WebserverConstants.HeaderTransferEncoding + ": chunked\r\n"));
                transferEncodingSet = true;
            }

            ret = AppendBytes(
                ret, 
                Encoding.UTF8.GetBytes(WebserverConstants.HeaderDate + ": " + DateTime.UtcNow.ToString(WebserverConstants.HeaderDateValueFormat) + "\r\n"));

            for (int i = 0; i < Headers.Count; i++)
            {
                string header = Headers.GetKey(i);
                if (String.IsNullOrEmpty(header)) continue;
                if (contentTypeSet && header.ToLower().Equals(WebserverConstants.HeaderContentType.ToLower())) continue;
                if (contentLengthSet && header.ToLower().Equals(WebserverConstants.HeaderContentLength.ToLower())) continue;
                if (transferEncodingSet && header.ToLower().Equals(WebserverConstants.HeaderTransferEncoding.ToLower())) continue;
                if (header.ToLower().Equals(WebserverConstants.HeaderDate.ToLower())) continue;

                string[] vals = Headers.GetValues(i);
                if (vals != null && vals.Length > 0)
                {
                    foreach (string val in vals)
                    {
                        ret = AppendBytes(ret, Encoding.UTF8.GetBytes(header + ": " + val + "\r\n"));
                    }
                }
            }

            ret = AppendBytes(ret, Encoding.UTF8.GetBytes("\r\n"));
            return ret;
        }

        private string GetStatusDescription(int statusCode)
        {
            //
            // Helpful links:
            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
            // https://en.wikipedia.org/wiki/List_of_HTTP_status_codes
            // 

            switch (statusCode)
            {
                case 100:
                    return "Continue";
                case 101:
                    return "Switching Protocols";
                case 102:
                    return "Processing";
                case 103:
                    return "Early Hints";

                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 202:
                    return "Accepted";
                case 203:
                    return "Non-Authoritative Information";
                case 204:
                    return "No Content";
                case 205:
                    return "Reset Content";
                case 206:
                    return "Partial Content";
                case 207:
                    return "Multi-Status";
                case 208:
                    return "Already Reported";
                case 226:
                    return "IM Used";

                case 300:
                    return "Multiple Choices";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Moved Temporarily";
                case 303:
                    return "See Other";
                case 304:
                    return "Not Modified";
                case 305:
                    return "Use Proxy";
                case 306:
                    return "Switch Proxy";
                case 307:
                    return "Temporary Redirect";
                case 308:
                    return "Permanent Redirect";

                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 402:
                    return "Payment Required";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 406:
                    return "Not Acceptable";
                case 407:
                    return "Proxy Authentication Required";
                case 408:
                    return "Request Timeout";
                case 409:
                    return "Conflict";
                case 410:
                    return "Gone";
                case 411:
                    return "Length Required";
                case 412:
                    return "Precondition Failed";
                case 413:
                    return "Payload too Large";
                case 414:
                    return "URI Too Long";
                case 415:
                    return "Unsupported Media Type";
                case 416:
                    return "Range Not Satisfiable";
                case 417:
                    return "Expectation Failed";
                case 418:
                    return "I'm a teapot";
                case 421:
                    return "Misdirected Request";
                case 422:
                    return "Unprocessable Content";
                case 423:
                    return "Locked";
                case 424:
                    return "Failed Dependency";
                case 425:
                    return "Too Early";
                case 426:
                    return "Upgrade Required";
                case 428:
                    return "Precondition Required";
                case 429:
                    return "Too Many Requests";
                case 431:
                    return "Request Header Fields Too Large";
                case 451:
                    return "Unavailable For Legal Reasons";

                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 502:
                    return "Bad Gateway";
                case 503:
                    return "Service Unavailable";
                case 504:
                    return "Gateway Timeout";
                case 505:
                    return "HTTP Version Not Supported";
                case 506:
                    return "Variant Also Negotiates";
                case 507:
                    return "Insufficient Storage";
                case 508:
                    return "Loop Detected";
                case 510:
                    return "Not Extended";
                case 511:
                    return "Network Authentication Required";
            }

            return "Unknown";
        }

        private void SetDefaultHeaders()
        {
            if (!_HeadersSet)
            {
                if (ChunkedTransfer || ServerSentEvents)
                {
                    ProtocolVersion = "HTTP/1.1";
                    Headers.Add("Transfer-Encoding", "chunked");
                }

                if (ServerSentEvents)
                {
                    Headers.Add("Content-Type", "text/event-stream; charset=utf-8");
                    Headers.Add("Cache-Control", "no-cache");
                    Headers.Add("Connection", "keep-alive");
                }

                if (_HeaderSettings != null && Headers != null)
                {
                    foreach (KeyValuePair<string, string> defaultHeader in _HeaderSettings.DefaultHeaders)
                    {
                        string key = defaultHeader.Key;
                        string val = defaultHeader.Value;

                        if (!Headers.AllKeys.Any(k => k.ToLower().Equals(key.ToLower())))
                        {
                            Headers.Add(key, val);
                        }
                    }
                }

                _HeadersSet = true;
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

        private void SetContentLength(long contentLength)
        {
            if (_HeaderSettings.IncludeContentLength
                && !ChunkedTransfer
                && !ServerSentEvents)
            {
                if (Headers.Count > 0)
                {
                    for (int i = 0; i < Headers.Count; i++)
                    {
                        string val = Headers.GetKey(i);
                        if (!String.IsNullOrEmpty(val)
                            && val.ToLower().Equals("content-length"))
                        {
                            Headers.Remove(val);
                        }
                    }
                }

                Headers.Add("Content-Length", contentLength.ToString());
            }
        }

        private async Task<bool> SendInternalAsync(long contentLength, Stream stream, bool close, CancellationToken token = default)
        {
            if (_HeaderSettings.IncludeContentLength
                && contentLength > 0
                && !ChunkedTransfer)
            {
                ContentLength = contentLength;
            }

            if (!_HeadersSet)
            {
                SetDefaultHeaders();
                SetContentLength(contentLength);
            }

            if (!_HeadersSent)
            {
                byte[] headers = GetHeaderBytes(); 
                try
                {
                    await _Stream.WriteAsync(headers, 0, headers.Length, token).ConfigureAwait(false);
                    await _Stream.FlushAsync(token).ConfigureAwait(false);
                    _HeadersSent = true; 
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            if (contentLength > 0 && stream != null && stream.CanRead)
            {
                long bytesRemaining = contentLength;

                byte[] buffer = new byte[_StreamBufferSize];
                int bytesToRead = _StreamBufferSize;
                int bytesRead = 0;
                 
                try
                {
                    while (bytesRemaining > 0)
                    {
                        if (bytesRemaining > _StreamBufferSize) bytesToRead = _StreamBufferSize;
                        else bytesToRead = (int)bytesRemaining;

                        bytesRead = await stream.ReadAsync(buffer, 0, bytesToRead, token).ConfigureAwait(false);
                        if (bytesRead > 0)
                        { 
                            await _Stream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                            bytesRemaining -= bytesRead;
                        }
                    }
				
                    await _Stream.FlushAsync(token).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            if (close)
            { 
                try
                {
                    _Stream.Close();
                    ResponseSent = true;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            return true;
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

        #endregion
    }
}