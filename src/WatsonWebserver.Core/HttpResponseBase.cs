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
using Timestamps;

namespace WatsonWebserver.Core
{
    /// <summary>
    /// HTTP response.
    /// </summary>
    public abstract class HttpResponseBase
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the response object was created.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-3)]
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-2)]
        public string StatusDescription
        {
            get
            {
                return StatusCodeToDescription(StatusCode);
            }
        }

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
        public string ContentType { get; set; } = String.Empty;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength = 0;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding should be indicated in the response. 
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
        /// </summary>
        [JsonIgnore]
        public abstract string DataAsString { get; }

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
        /// </summary>
        [JsonIgnore]
        public abstract byte[] DataAsBytes { get; }

        /// <summary>
        /// Response data stream sent to the requestor.
        /// </summary>
        [JsonIgnore]
        public abstract MemoryStream Data { get; }

        /// <summary>
        /// Boolean indicating if the response has been sent.
        /// </summary>
        public bool ResponseSent { get; set; } = false;

        #endregion

        #region Private-Members

        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> Send(CancellationToken token = default);

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <param name="contentLength">Content length.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> Send(long contentLength, CancellationToken token = default);

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> Send(string data, CancellationToken token = default);

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> Send(byte[] data, CancellationToken token = default);

        /// <summary>
        /// Send headers and data to the requestor and terminate.
        /// </summary>
        /// <param name="contentLength">Number of bytes to send.</param>
        /// <param name="stream">Stream containing the data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> Send(long contentLength, Stream stream, CancellationToken token = default);
        
        /// <summary>
        /// Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection in-tact.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> SendChunk(byte[] chunk, CancellationToken token = default);

        /// <summary>
        /// Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the connection.
        /// </summary>
        /// <param name="chunk">Chunk of data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public abstract Task<bool> SendFinalChunk(byte[] chunk, CancellationToken token = default);

        #endregion

        #region Private-Methods

        private string StatusCodeToDescription(int statusCode)
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
                    return "No Contact";
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
                    return "Found";
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

        #endregion
    }
}
