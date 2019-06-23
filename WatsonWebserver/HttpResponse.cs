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

        //
        // Values from the request
        //

        /// <summary>
        /// UTC timestamp from when the response was generated.
        /// </summary>
        public DateTime TimestampUtc;

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
        /// The HTTP method used in the request.
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// The raw (relative) URL without the querystring attached.
        /// </summary>
        public string RawUrlWithoutQuery;

        //
        // Response values
        //

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        public int StatusCode;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        public string StatusDescription;
         
        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        public Dictionary<string, string> Headers;

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength;

        /// <summary>
        /// The data to return to the requestor in the response body.
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// The stream to read and send to the requestor in the response body.
        /// </summary>
        public Stream DataStream;

        /// <summary>
        /// The MD5 value calculated over the supplied Data.
        /// </summary>
        public string DataMd5;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create an uninitialized HttpResponse object.
        /// </summary>
        public HttpResponse()
        {
            TimestampUtc = DateTime.Now.ToUniversalTime();
            Headers = new Dictionary<string, string>();
        }

        /// <summary>
        /// Create a new HttpResponse object with no data.
        /// </summary>
        /// <param name="req">The HttpRequest object for which this request is being created.</param>
        /// <param name="status">The HTTP status code to return to the requestor (client).</param>
        /// <param name="headers">User-supplied headers to include in the response.</param>
        public HttpResponse(HttpRequest req, int status, Dictionary<string, string> headers)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            SetBaseVariables(req, status, headers, null);
            SetStatusDescription();

            DataStream = null;
            Data = null;
            ContentLength = 0;
        }

        /// <summary>
        /// Create a new HttpResponse object.
        /// </summary>
        /// <param name="req">The HttpRequest object for which this request is being created.</param>
        /// <param name="status">The HTTP status code to return to the requestor (client).</param>
        /// <param name="headers">User-supplied headers to include in the response.</param>
        /// <param name="contentType">User-supplied content-type to include in the response.</param>
        /// <param name="data">The data to return to the requestor in the response body.</param> 
        public HttpResponse(HttpRequest req, int status, Dictionary<string, string> headers, string contentType, byte[] data)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            SetBaseVariables(req, status, headers, contentType);
            SetStatusDescription();

            DataStream = null;
            Data = data;
            if (Data != null && Data.Length > 0) ContentLength = Data.Length;
        }

        /// <summary>
        /// Create a new HttpResponse object.
        /// </summary>
        /// <param name="req">The HttpRequest object for which this request is being created.</param>
        /// <param name="status">The HTTP status code to return to the requestor (client).</param>
        /// <param name="headers">User-supplied headers to include in the response.</param>
        /// <param name="contentType">User-supplied content-type to include in the response.</param>
        /// <param name="data">The data to return to the requestor in the response body.</param> 
        public HttpResponse(HttpRequest req, int status, Dictionary<string, string> headers, string contentType, string data)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            SetBaseVariables(req, status, headers, contentType);
            SetStatusDescription();

            DataStream = null;
            Data = null;
            if (!String.IsNullOrEmpty(data)) Data = Encoding.UTF8.GetBytes(data);
            if (Data != null && Data.Length > 0) ContentLength = Data.Length;
        }

        /// <summary>
        /// Create a new HttpResponse object.
        /// </summary>
        /// <param name="req">The HttpRequest object for which this request is being created.</param>
        /// <param name="status">The HTTP status code to return to the requestor (client).</param>
        /// <param name="headers">User-supplied headers to include in the response.</param>
        /// <param name="contentType">User-supplied content-type to include in the response.</param>
        /// <param name="contentLength">The number of bytes the client should expect to read from the data stream.</param>
        /// <param name="dataStream">The stream containing the data that should be read to return to the requestor.</param>
        public HttpResponse(HttpRequest req, int status, Dictionary<string, string> headers, string contentType, long contentLength, Stream dataStream)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            SetBaseVariables(req, status, headers, contentType);
            SetStatusDescription();

            Data = null;
            DataStream = dataStream;
            ContentLength = contentLength; 

            if (contentLength > 0 && !DataStream.CanRead) throw new IOException("Cannot read from input stream.");
            if (contentLength > 0 && !DataStream.CanSeek) throw new IOException("Cannot perform seek on input stream.");
            if (contentLength > 0) DataStream.Seek(0, SeekOrigin.Begin);
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
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + " " + Method + " " + RawUrlWithoutQuery + " [" + StatusCode + "]" + Environment.NewLine;
            ret += "  Content     : " + ContentType + " (" + ContentLength + " bytes)" + Environment.NewLine;
            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers     : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "  - " + curr.Key + ": " + curr.Value + Environment.NewLine;
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

            if (DataStream != null)
            {
                ret += "  Data Stream : [exists]" + Environment.NewLine;
            }

            return ret;
        }
          
        #endregion

        #region Private-Methods

        private void SetBaseVariables(HttpRequest req, int status, Dictionary<string, string> headers, string contentType)
        {
            TimestampUtc = req.TimestampUtc;
            SourceIp = req.SourceIp;
            SourcePort = req.SourcePort;
            DestIp = req.DestIp;
            DestPort = req.DestPort;
            Method = req.Method;
            RawUrlWithoutQuery = req.RawUrlWithoutQuery;
             
            Headers = headers;
            ContentType = contentType; 
            if (String.IsNullOrEmpty(ContentType)) ContentType = "application/octet-stream";

            StatusCode = status; 
        }

        private void SetStatusDescription()
        { 
            switch (StatusCode)
            {
                case 200:
                    StatusDescription = "OK";
                    break;

                case 201:
                    StatusDescription = "Created";
                    break;

                case 204:
                    StatusDescription = "No Content";
                    break;

                case 301:
                    StatusDescription = "Moved Permanently";
                    break;

                case 302:
                    StatusDescription = "Moved Temporarily";
                    break;

                case 304:
                    StatusDescription = "Not Modified";
                    break;

                case 400:
                    StatusDescription = "Bad Request";
                    break;

                case 401:
                    StatusDescription = "Unauthorized";
                    break;

                case 403:
                    StatusDescription = "Forbidden";
                    break;

                case 404:
                    StatusDescription = "Not Found";
                    break;

                case 405:
                    StatusDescription = "Method Not Allowed";
                    break;

                case 429:
                    StatusDescription = "Too Many Requests";
                    break;

                case 500:
                    StatusDescription = "Internal Server Error";
                    break;

                case 501:
                    StatusDescription = "Not Implemented";
                    break;

                case 503:
                    StatusDescription = "Service Unavailable";
                    break;

                default:
                    StatusDescription = "Unknown";
                    return;
            } 
        }

        private byte[] AppendBytes(byte[] orig, byte[] append)
        {
            if (append == null) return orig;
            if (orig == null) return append;

            byte[] ret = new byte[orig.Length + append.Length];
            Buffer.BlockCopy(orig, 0, ret, 0, orig.Length);
            Buffer.BlockCopy(append, 0, ret, orig.Length, append.Length);
            return ret;
        }

        #endregion
    }
}
