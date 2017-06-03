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
    public class HttpResponse
    {
        //
        //
        // Do not serialize this object directly when sending a response.  Use the .ToJson() method instead
        // since the JSON output will not match in terms of actual class member names and such.
        //
        //

        #region Public-Members

        //
        // Values from the request
        //

        /// <summary>
        /// UTC timestamp from when the response was generated.
        /// </summary>
        public DateTime TimestampUtc;

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
        /// The HTTP verb used in the request.
        /// </summary>
        public string Method;

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
        /// Indicates whether or not the request was successful, which populates the 'success' flag in the JSON response.
        /// </summary>
        public bool Success;

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        public Dictionary<string, string> Headers;

        /// <summary>
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The data to return to the requestor in the response body.  This must be either a byte[] or string.
        /// </summary>
        public object Data;

        /// <summary>
        /// The MD5 value calculated over the supplied Data.
        /// </summary>
        public string DataMd5;

        /// <summary>
        /// Indicates whether or not the response Data should be enapsulated in a JSON object containing standard fields including 'success'.
        /// </summary>
        public bool RawResponse;

        #endregion

        #region Private-Members

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new HttpResponse object.
        /// </summary>
        /// <param name="req">The HttpRequest object for which this request is being created.</param>
        /// <param name="success">Indicates whether or not the request was successful.</param>
        /// <param name="status">The HTTP status code to return to the requestor (client).</param>
        /// <param name="headers">User-supplied headers to include in the response.</param>
        /// <param name="contentType">User-supplied content-type to include in the response.</param>
        /// <param name="data">The data to return to the requestor in the response body.  This must be either a byte[] or string.</param>
        /// <param name="rawResponse">Indicates whether or not the response Data should be enapsulated in a JSON object containing standard fields including 'success'.</param>
        public HttpResponse(HttpRequest req, bool success, int status, Dictionary<string, string> headers, string contentType, object data, bool rawResponse)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            TimestampUtc = req.TimestampUtc;
            SourceIp = req.SourceIp;
            SourcePort = req.SourcePort;
            DestIp = req.DestIp;
            DestPort = req.DestPort;
            Method = req.Method;
            RawUrlWithoutQuery = req.RawUrlWithoutQuery;

            Success = success;
            Headers = headers;
            ContentType = contentType;
            if (String.IsNullOrEmpty(ContentType)) ContentType = "application/json";
            StatusCode = status;
            RawResponse = rawResponse;
            Data = data;
            
            switch (status)
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

        #endregion

        #region Public-Internal-Classes

        #endregion

        #region Private-Internal-Classes

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve a string-formatted, human-readable copy of the HttpResponse instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpResponse instance.</returns>
        public override string ToString()
        {
            string ret = "";
            int contentLength = 0;
            if (Data != null)
            {
                if (Data is byte[]) contentLength = ((byte[])Data).Length;
                else contentLength = Data.ToString().Length;
            }
            string contentType = "text/plain";
            if (!String.IsNullOrEmpty(ContentType)) contentType = ContentType;

            ret += "--- HTTP Response ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + "  " + Method + " " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Success : " + Success + Environment.NewLine;
            ret += "  Content : " + ContentType + " (" + contentLength + " bytes)" + Environment.NewLine;
            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "    " + curr.Key + ": " + curr.Value + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Headers : none" + Environment.NewLine;
            }

            if (Data != null)
            {
                ret += "  Data    : " + Environment.NewLine;
                if (Data is byte[])
                {
                    ret += Encoding.UTF8.GetString(((byte[])Data)) + Environment.NewLine;
                }
                else if (Data is string)
                {
                    ret += Data + Environment.NewLine;
                }
                else
                {
                    ret += "  [unknown data type]" + Environment.NewLine;
                }
            }
            else
            {
                ret += "  Data    : [null]" + Environment.NewLine;
            }

            return ret;
        }

        /// <summary>
        /// Creates a JSON string of the response and data including fields indicating success and data MD5.
        /// </summary>
        /// <returns></returns>
        public string ToJson()
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            ret.Add("success", Success);
            if (Data != null)
            {
                ret.Add("md5", WatsonCommon.CalculateMd5(Data.ToString()));
                ret.Add("data", Data);
            }
            return WatsonCommon.SerializeJson(ret);
        }

        /// <summary>
        /// Creates a byte array containing a JSON string of the response and data including fields indicatng success and data MD5.
        /// </summary>
        /// <returns></returns>
        public byte[] ToJsonBytes()
        {
            return Encoding.UTF8.GetBytes(ToJson());
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
