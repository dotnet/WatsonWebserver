namespace WatsonWebserver.Http3
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// HTTP/3 request.
    /// </summary>
    public class Http3Request : HttpRequestBase
    {
        [JsonIgnore]
        public override Stream Data
        {
            get
            {
                return _Data;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Data));
                _Data = value;
            }
        }

        [JsonIgnore]
        public override byte[] DataAsBytes
        {
            get
            {
                if (_DataAsBytes != null) return _DataAsBytes;
                if (_Data == Stream.Null)
                {
                    _DataAsBytes = Array.Empty<byte>();
                    return _DataAsBytes;
                }

                _DataAsBytes = ReadBodyBytes();
                return _DataAsBytes;
            }
        }

        [JsonIgnore]
        public override string DataAsString
        {
            get
            {
                byte[] bodyBytes = DataAsBytes;
                if (bodyBytes == null) return null;
                return Encoding.UTF8.GetString(bodyBytes);
            }
        }

        private Stream _Data = null;
        private byte[] _DataAsBytes = null;
        private HttpHeaderField[] _HeaderFields = Array.Empty<HttpHeaderField>();

        public Http3Request()
        {
        }

        internal Http3Request(
            WebserverSettings settings,
            SourceDetails source,
            DestinationDetails destination,
            HttpMethod method,
            string methodRaw,
            string scheme,
            string authority,
            string pathAndQuery,
            HttpHeaderField[] headers,
            NameValueCollection trailers,
            Stream bodyStream,
            long bodyLength)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (String.IsNullOrEmpty(methodRaw)) throw new ArgumentNullException(nameof(methodRaw));
            if (String.IsNullOrEmpty(scheme)) throw new ArgumentNullException(nameof(scheme));
            if (String.IsNullOrEmpty(pathAndQuery)) throw new ArgumentNullException(nameof(pathAndQuery));
            if (bodyStream == null) throw new ArgumentNullException(nameof(bodyStream));
            if (!bodyStream.CanRead) throw new IOException("Cannot read from supplied body stream.");
            if (bodyLength < 0) throw new ArgumentOutOfRangeException(nameof(bodyLength));

            Protocol = HttpProtocol.Http3;
            ProtocolVersion = "HTTP/3";
            Source = source;
            Destination = destination;
            Method = method;
            MethodRaw = methodRaw;
            Keepalive = true;
            ChunkedTransfer = false;
            _HeaderFields = headers ?? Array.Empty<HttpHeaderField>();
            SetHeadersFactory(() => HttpHeaderField.ToNameValueCollection(_HeaderFields));
            Trailers = trailers ?? new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            ContentType = HttpHeaderField.GetValue(_HeaderFields, WebserverConstants.HeaderContentType);
            Useragent = HttpHeaderField.GetValue(_HeaderFields, "user-agent");

            string effectiveAuthority = !String.IsNullOrEmpty(authority)
                ? authority
                : (!String.IsNullOrEmpty(Destination.Hostname) ? Destination.Hostname : settings.Hostname);

            Url = new UrlDetails(pathAndQuery, scheme, effectiveAuthority);
            if (pathAndQuery.IndexOf("?", StringComparison.Ordinal) >= 0)
            {
                Query = new QueryDetails(pathAndQuery);
            }

            ContentLength = bodyLength;
            if (bodyStream.CanSeek)
            {
                bodyStream.Position = 0;
            }

            _Data = bodyStream;
            OwnsDataStream = true;

            string contentLengthHeader = HttpHeaderField.GetValue(_HeaderFields, WebserverConstants.HeaderContentLength);
            if (!String.IsNullOrEmpty(contentLengthHeader))
            {
                if (!Int64.TryParse(contentLengthHeader, out long parsedContentLength) || parsedContentLength < 0)
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 Content-Length must be a non-negative integer.");
                }

                if (parsedContentLength != bodyLength)
                {
                    throw new Http3ProtocolException(Http3ErrorCode.MessageError, "HTTP/3 Content-Length does not match the received request body length.");
                }

                ContentLength = parsedContentLength;
            }
        }

        public override Task<Chunk> ReadChunk(CancellationToken token = default)
        {
            throw new InvalidOperationException("HTTP/3 requests do not expose HTTP/1.1 chunked transfer-encoding semantics.");
        }

        public override bool HeaderExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            return HttpHeaderField.Exists(_HeaderFields, key);
        }

        public override bool QuerystringExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Query == null || Query.Elements == null) return false;
            return Query.Elements.AllKeys != null && Query.Elements.Get(key) != null;
        }

        public override string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            return HttpHeaderField.GetValue(_HeaderFields, key);
        }

        public override string RetrieveQueryValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (Query == null || Query.Elements == null) return null;

            string value = Query.Elements.Get(key);
            if (!String.IsNullOrEmpty(value))
            {
                value = WebUtility.UrlDecode(value);
            }

            return value;
        }

        private byte[] ReadBodyBytes()
        {
            if (_Data == null) return null;
            if (!_Data.CanRead) throw new InvalidOperationException("Request body stream is not readable.");

            long originalPosition = 0;
            if (_Data.CanSeek)
            {
                originalPosition = _Data.Position;
                _Data.Seek(0, SeekOrigin.Begin);
            }

            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    _Data.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            finally
            {
                if (_Data.CanSeek)
                {
                    _Data.Seek(originalPosition, SeekOrigin.Begin);
                }
            }
        }
    }
}
