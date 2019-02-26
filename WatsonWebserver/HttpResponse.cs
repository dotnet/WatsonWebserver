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
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength;

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
        /// Create an uninitialized HttpResponse object.
        /// </summary>
        public HttpResponse()
        {
            TimestampUtc = DateTime.Now.ToUniversalTime();
            Headers = new Dictionary<string, string>();
        }

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

            #region Set-Base-Variables

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

            #endregion

            #region Set-Status

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

            #endregion

            #region Check-Data

            if (Data != null)
            {
                if (Data is byte[])
                {
                    ContentLength = ((byte[])Data).Length;
                }
                else if (Data is string)
                {
                    ContentLength = ((string)Data).Length;
                }
                else
                {
                    ContentLength = (WatsonCommon.SerializeJson(Data)).Length;
                    Data = WatsonCommon.SerializeJson(Data);
                } 
            }
            else
            {
                ContentLength = 0;
            }

            #endregion
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
  
            ret += "--- HTTP Response ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + "  " + Method + " " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Success : " + Success + Environment.NewLine;
            ret += "  Content : " + ContentType + " (" + ContentLength + " bytes)" + Environment.NewLine;
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
                    ret += WatsonCommon.SerializeJson(Data) + Environment.NewLine;
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
        /// <returns>String containing JSON representation of the HTTP response.</returns>
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
        /// <returns>Byte array containing the JSON representation of the HTTP response.</returns>
        public byte[] ToJsonBytes()
        {
            return Encoding.UTF8.GetBytes(ToJson());
        }

        /// <summary>
        /// Creates a byte array containing the HTTP response (useful for sockets apps what want to transmit the response directly).
        /// </summary>
        /// <returns></returns>
        public byte[] ToHttpBytes()
        { 
            byte[] ret = null;

            string statusLine = ProtocolVersion + " " + StatusCode + " " + StatusDescription + "\r\n";
            ret = AppendBytes(ret, Encoding.UTF8.GetBytes(statusLine));

            if (!String.IsNullOrEmpty(ContentType))
            {
                string contentTypeLine = "Content-Type: " + ContentType + "\r\n";
                ret = AppendBytes(ret, Encoding.UTF8.GetBytes(contentTypeLine));
            }

            if (Headers != null && Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> currHeader in Headers)
                {
                    if (String.IsNullOrEmpty(currHeader.Key)) continue;
                    if (currHeader.Key.ToLower().Trim().Equals("content-type")) continue;

                    string headerLine = currHeader.Key + ": " + currHeader.Value + "\r\n";
                    ret = AppendBytes(ret, Encoding.UTF8.GetBytes(headerLine));
                }
            }

            ret = AppendBytes(ret, Encoding.UTF8.GetBytes("\r\n"));

            if (Data != null)
            {
                if (Data is byte[])
                {
                    ret = AppendBytes(ret, (byte[])Data);
                }
                else if (Data is string)
                {
                    ret = AppendBytes(ret, Encoding.UTF8.GetBytes((string)Data));
                }
                else
                {
                    ret = AppendBytes(ret, Encoding.UTF8.GetBytes(WatsonCommon.SerializeJson(Data)));
                }
            }

            return ret;
        }

        #endregion

        #region Private-Methods

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
