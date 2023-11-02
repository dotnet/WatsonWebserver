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

namespace WatsonWebserver.Core
{
    /// <summary>
    /// HTTP request.
    /// </summary>
    public abstract class HttpRequestBase
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the request object was received.
        /// </summary>
        [JsonPropertyOrder(-11)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// Globally-unique identifier for the request.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public int ThreadId { get; set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public SourceDetails Source { get; set; } = new SourceDetails();

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public DestinationDetails Destination { get; set; } = new DestinationDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonPropertyOrder(-6)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The string version of the HTTP method, useful if Method is UNKNOWN.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public string MethodRaw { get; set; } = null;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public UrlDetails Url { get; set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public QueryDetails Query { get; set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-2)]
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
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; set; } = false;
         
        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public abstract Stream Data { get; set; }

        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public abstract byte[] DataAsBytes { get; }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public abstract string DataAsString { get; }

        #endregion

        #region Private-Members

        private NameValueCollection _Headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public abstract Task<Chunk> ReadChunk(CancellationToken token = default);

        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool HeaderExists(string key);

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public abstract bool QuerystringExists(string key);

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public abstract string RetrieveHeaderValue(string key);

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Value.</returns>
        public abstract string RetrieveQueryValue(string key);

        #endregion

        #region Private-Methods

        #endregion

        #region Embedded-Classes

        /// <summary>
        /// Source details.
        /// </summary>
        public class SourceDetails
        {
            /// <summary>
            /// IP address of the requestor.
            /// </summary>
            public string IpAddress { get; set; } = null;

            /// <summary>
            /// TCP port from which the request originated on the requestor.
            /// </summary>
            public int Port { get; set; } = 0;

            /// <summary>
            /// Source details.
            /// </summary>
            public SourceDetails()
            {

            }

            /// <summary>
            /// Source details.
            /// </summary>
            /// <param name="ip">IP address of the requestor.</param>
            /// <param name="port">TCP port from which the request originated on the requestor.</param>
            public SourceDetails(string ip, int port)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

                IpAddress = ip;
                Port = port;
            }
        }

        /// <summary>
        /// Destination details.
        /// </summary>
        public class DestinationDetails
        {
            /// <summary>
            /// IP address to which the request was made.
            /// </summary>
            public string IpAddress { get; set; } = null;

            /// <summary>
            /// TCP port on which the request was received.
            /// </summary>
            public int Port { get; set; } = 0;

            /// <summary>
            /// Hostname to which the request was directed.
            /// </summary>
            public string Hostname { get; set; } = null;

            /// <summary>
            /// Hostname elements.
            /// </summary>
            public string[] HostnameElements
            {
                get
                {
                    string hostname = Hostname;
                    string[] ret;

                    if (!String.IsNullOrEmpty(hostname))
                    {
                        if (!IPAddress.TryParse(hostname, out _))
                        {
                            ret = hostname.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                            return ret;
                        }
                        else
                        {
                            ret = new string[1];
                            ret[0] = hostname;
                            return ret;
                        }
                    }

                    ret = new string[0];
                    return ret;
                }
            }

            /// <summary>
            /// Destination details.
            /// </summary>
            public DestinationDetails()
            {

            }

            /// <summary>
            /// Source details.
            /// </summary>
            /// <param name="ip">IP address to which the request was made.</param>
            /// <param name="port">TCP port on which the request was received.</param>
            /// <param name="hostname">Hostname.</param>
            public DestinationDetails(string ip, int port, string hostname)
            {
                if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
                if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
                if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

                IpAddress = ip;
                Port = port;
                Hostname = hostname;
            }
        }

        /// <summary>
        /// URL details.
        /// </summary>
        public class UrlDetails
        {
            /// <summary>
            /// Full URL.
            /// </summary>
            public string Full { get; set; } = null;

            /// <summary>
            /// Raw URL with query.
            /// </summary>
            public string RawWithQuery { get; set; } = null;

            /// <summary>
            /// Raw URL without query.
            /// </summary>
            public string RawWithoutQuery
            {
                get
                {
                    if (!String.IsNullOrEmpty(RawWithQuery))
                    {
                        if (RawWithQuery.Contains("?")) return RawWithQuery.Substring(0, RawWithQuery.IndexOf("?"));
                        else return RawWithQuery;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Raw URL elements.
            /// </summary>
            public string[] Elements
            {
                get
                { 
                    string rawUrl = RawWithoutQuery;

                    if (!String.IsNullOrEmpty(rawUrl))
                    {
                        while (rawUrl.Contains("//")) rawUrl = rawUrl.Replace("//", "/");
                        while (rawUrl.StartsWith("/")) rawUrl = rawUrl.Substring(1);
                        while (rawUrl.EndsWith("/")) rawUrl = rawUrl.Substring(0, rawUrl.Length - 1);
                        string[] encoded = rawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        if (encoded != null && encoded.Length > 0)
                        {
                            string[] decoded = new string[encoded.Length];
                            for (int i = 0; i < encoded.Length; i++)
                            {
                                decoded[i] = WebUtility.UrlDecode(encoded[i]);
                            }

                            return decoded;
                        }
                    }

                    string[] ret = new string[0];
                    return ret;
                }
            }

            /// <summary>
            /// Parameters found within the URL, if using parameter routes.
            /// </summary>
            public NameValueCollection Parameters
            {
                get
                {
                    return _Parameters;
                }
                set
                {
                    if (value == null) _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    else _Parameters = value;
                }
            }

            /// <summary>
            /// URL details.
            /// </summary>
            public UrlDetails()
            {

            }

            /// <summary>
            /// URL details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param>
            /// <param name="rawUrl">Raw URL.</param>
            public UrlDetails(string fullUrl, string rawUrl)
            {
                if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

                Full = fullUrl;
                RawWithQuery = rawUrl;
            }

            private NameValueCollection _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        }
        
        /// <summary>
        /// Query details.
        /// </summary>
        public class QueryDetails
        {
            /// <summary>
            /// Querystring, excluding the leading '?'.
            /// </summary>
            public string Querystring
            {
                get
                {
                    if (_FullUrl.Contains("?"))
                    {
                        return _FullUrl.Substring(_FullUrl.IndexOf("?") + 1, (_FullUrl.Length - _FullUrl.IndexOf("?") - 1));
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Query elements.
            /// </summary>
            public NameValueCollection Elements
            {
                get
                {
                    NameValueCollection ret = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                    string qs = Querystring;
                    if (!String.IsNullOrEmpty(qs))
                    {
                        string[] queries = qs.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                        if (queries.Length > 0)
                        {
                            for (int i = 0; i < queries.Length; i++)
                            {
                                string[] queryParts = queries[i].Split('=');
                                if (queryParts != null && queryParts.Length == 2)
                                {
                                    ret.Add(queryParts[0], queryParts[1]);
                                }
                                else if (queryParts != null && queryParts.Length == 1)
                                {
                                    ret.Add(queryParts[0], null);
                                }
                            }
                        }
                    }

                    return ret;
                }
            }

            /// <summary>
            /// Query details.
            /// </summary>
            public QueryDetails()
            {

            }

            /// <summary>
            /// Query details.
            /// </summary>
            /// <param name="fullUrl">Full URL.</param>
            public QueryDetails(string fullUrl)
            {
                if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));

                _FullUrl = fullUrl;
            }

            private string _FullUrl = null;
        }

        #endregion
    }
}
