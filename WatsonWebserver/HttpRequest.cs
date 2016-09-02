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
using System.Web;

namespace WatsonWebserver
{
    /// <summary>
    /// Data extracted from an incoming HTTP request.
    /// </summary>
    public class HttpRequest
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request was received.
        /// </summary>
        public DateTime TimestampUtc;

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        public int ThreadId;

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
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive;

        /// <summary>
        /// The HTTP verb used in the request.
        /// </summary>
        public string Method;

        /// <summary>
        /// The full URL as sent by the requestor (client).
        /// </summary>
        public string FullUrl;

        /// <summary>
        /// The raw (relative) URL with the querystring attached.
        /// </summary>
        public string RawUrlWithQuery;

        /// <summary>
        /// The raw (relative) URL without the querystring attached.
        /// </summary>
        public string RawUrlWithoutQuery;

        /// <summary>
        /// List of items found in the raw URL.
        /// </summary>
        public List<string> RawUrlEntries;

        /// <summary>
        /// The querystring attached to the URL.
        /// </summary>
        public string Querystring;

        /// <summary>
        /// Dictionary containing key-value pairs from items found in the querystring.
        /// </summary>
        public Dictionary<string, string> QuerystringEntries;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        public long ContentLength;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        public string ContentType;

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        public Dictionary<string, string> Headers;

        /// <summary>
        /// The request body as sent by the requestor (client).
        /// </summary>
        public byte[] Data;

        #endregion

        #region Private-Members

        #endregion

        #region Constructor

        /// <summary>
        /// Construct a new HTTP request from a given HttpListenerContext.
        /// </summary>
        /// <param name="ctx">The HttpListenerContext for the request.</param>
        public HttpRequest(HttpListenerContext ctx)
        {
            #region Check-for-Null-Values

            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));

            #endregion

            #region Parse-Variables

            int position = 0;
            int inQuery = 0;
            string tempString = "";
            string queryString = "";

            int inKey = 0;
            int inVal = 0;
            string tempKey = "";
            string tempVal = "";

            #endregion
            
            #region Standard-Request-Items

            ThreadId = Thread.CurrentThread.ManagedThreadId;
            TimestampUtc = DateTime.Now.ToUniversalTime();
            SourceIp = ctx.Request.RemoteEndPoint.Address.ToString();
            SourcePort = ctx.Request.RemoteEndPoint.Port;
            DestIp = ctx.Request.LocalEndPoint.Address.ToString();
            DestPort = ctx.Request.LocalEndPoint.Port;
            Method = ctx.Request.HttpMethod;
            FullUrl = String.Copy(ctx.Request.Url.ToString().Trim());
            RawUrlWithQuery = String.Copy(ctx.Request.RawUrl.ToString().Trim());
            RawUrlWithoutQuery = String.Copy(ctx.Request.RawUrl.ToString().Trim());
            Keepalive = ctx.Request.KeepAlive;
            ContentLength = ctx.Request.ContentLength64;
            Useragent = ctx.Request.UserAgent;
            ContentType = ctx.Request.ContentType;

            RawUrlEntries = new List<string>();
            QuerystringEntries = new Dictionary<string, string>();
            Headers = new Dictionary<string, string>();

            #endregion

            #region Raw-URL-and-Querystring

            if (!String.IsNullOrEmpty(RawUrlWithoutQuery))
            {
                #region Initialize-Variables

                RawUrlEntries = new List<string>();
                QuerystringEntries = new Dictionary<string, string>();

                #endregion

                #region Process-Raw-URL-and-Populate-Raw-URL-Elements

                foreach (char c in RawUrlWithoutQuery)
                {
                    if (inQuery == 1)
                    {
                        queryString += c;
                        continue;
                    }

                    if ((position == 0) &&
                        (String.Compare(tempString, "") == 0) &&
                        (c == '/'))
                    {
                        // skip the first slash
                        continue;
                    }

                    if ((c != '/') && (c != '?'))
                    {
                        tempString += c;
                    }

                    if ((c == '/') || (c == '?'))
                    {
                        if (!String.IsNullOrEmpty(tempString))
                        {
                            // add to raw URL entries list
                            RawUrlEntries.Add(tempString);
                        }

                        position++;
                        tempString = "";
                    }

                    if (c == '?')
                    {
                        inQuery = 1;
                    }
                }

                if (!String.IsNullOrEmpty(tempString))
                {
                    // add to raw URL entries list
                    RawUrlEntries.Add(tempString);
                }

                #endregion

                #region Populate-Querystring

                if (queryString.Length > 0) Querystring = queryString;
                else Querystring = null;

                #endregion

                #region Parse-Querystring

                if (!String.IsNullOrEmpty(Querystring))
                {
                    inKey = 1;
                    inVal = 0;
                    position = 0;
                    tempKey = "";
                    tempVal = "";

                    foreach (char c in Querystring)
                    {
                        if (inKey == 1)
                        {
                            if (c != '=')
                            {
                                tempKey += c;
                            }
                            else
                            {
                                inKey = 0;
                                inVal = 1;
                                continue;
                            }
                        }

                        if (inVal == 1)
                        {
                            if (c != '&')
                            {
                                tempVal += c;
                            }
                            else
                            {
                                inKey = 1;
                                inVal = 0;

                                if (!String.IsNullOrEmpty(tempVal)) tempVal = HttpUtility.UrlDecode(tempVal);
                                QuerystringEntries.Add(tempKey, tempVal);

                                tempKey = "";
                                tempVal = "";
                                position++;
                                continue;
                            }
                        }
                    }

                    if (inVal == 1)
                    {
                        if (!String.IsNullOrEmpty(tempVal)) tempVal = HttpUtility.UrlDecode(tempVal);
                        QuerystringEntries.Add(tempKey, tempVal);
                    }
                }

                #endregion
            }

            #endregion

            #region Remove-Querystring-from-Raw-URL

            if (RawUrlWithoutQuery.Contains("?"))
            {
                RawUrlWithoutQuery = RawUrlWithoutQuery.Substring(0, RawUrlWithoutQuery.IndexOf("?"));
            }

            #endregion

            #region Headers

            Headers = new Dictionary<string, string>();
            for (int i = 0; i < ctx.Request.Headers.Count; i++)
            {
                string key = String.Copy(ctx.Request.Headers.GetKey(i));
                string val = String.Copy(ctx.Request.Headers.Get(i));
                Headers.Add(key, val);
            }

            #endregion

            #region Copy-Payload

            if (ContentLength > 0)
            {
                if (String.Compare(Method.ToLower().Trim(), "get") != 0)
                {
                    try
                    {
                        if (ContentLength < 1)
                        {
                            Data = null;
                        }
                        else
                        {
                            Data = new byte[ContentLength];
                            Stream bodyStream = ctx.Request.InputStream;

                            Data = Common.StreamToBytes(bodyStream);
                        }
                    }
                    catch (Exception)
                    {
                        Data = null;
                    }
                }
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
        /// Retrieve a string-formatted, human-readable copy of the HttpRequest instance.
        /// </summary>
        /// <returns>String-formatted, human-readable copy of the HttpRequest instance.</returns>
        public override string ToString()
        {
            string ret = "";
            int contentLength = 0;
            if (Data != null)
            {
                contentLength = Data.Length;
            }

            ret += "--- HTTP Request ---" + Environment.NewLine;
            ret += TimestampUtc.ToString("MM/dd/yyyy HH:mm:ss") + " " + SourceIp + ":" + SourcePort + " to " + DestIp + ":" + DestPort + "  " + Method + " " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Full URL    : " + FullUrl + Environment.NewLine;
            ret += "  Raw URL     : " + RawUrlWithoutQuery + Environment.NewLine;
            ret += "  Querystring : " + Querystring + Environment.NewLine;
            ret += "  Useragent   : " + Useragent + " (Keepalive " + Keepalive + ")" + Environment.NewLine;
            ret += "  Content     : " + ContentType + " (" + contentLength + " bytes)" + Environment.NewLine;

            if (Headers != null && Headers.Count > 0)
            {
                ret += "  Headers     : " + Environment.NewLine;
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    ret += "    " + curr.Key + ": " + curr.Value + Environment.NewLine;
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

            return ret;
        }

        /// <summary>
        /// Retrieve a specified header value from either the headers or the querystring.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Headers != null && Headers.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in Headers)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            if (QuerystringEntries != null && QuerystringEntries.Count > 0)
            {
                foreach (KeyValuePair<string, string> curr in QuerystringEntries)
                {
                    if (String.IsNullOrEmpty(curr.Key)) continue;
                    if (String.Compare(curr.Key.ToLower(), key.ToLower()) == 0) return curr.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieve the integer value of the last raw URL element, if found.
        /// </summary>
        /// <returns>A nullable integer.</returns>
        public int? RetrieveIdValue()
        {
            if (RawUrlEntries == null || RawUrlEntries.Count < 1) return null;
            string[] entries = RawUrlEntries.ToArray();
            int len = entries.Length;
            string entry = entries[(len - 1)];
            int ret = -1;
            if (Int32.TryParse(entry, out ret))
            {
                return ret;
            }
            return null;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
