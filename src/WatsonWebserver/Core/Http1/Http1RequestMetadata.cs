namespace WatsonWebserver.Core.Http1
{
    using System;
    using System.Collections.Specialized;
    using System.Text;

    /// <summary>
    /// Parsed HTTP/1.1 request metadata.
    /// </summary>
    public class Http1RequestMetadata
    {
        /// <summary>
        /// Source endpoint details.
        /// </summary>
        public SourceDetails Source
        {
            get
            {
                if (_Source == null) _Source = new SourceDetails();
                return _Source;
            }
            set
            {
                _Source = value ?? new SourceDetails();
            }
        }

        /// <summary>
        /// Destination endpoint details.
        /// </summary>
        public DestinationDetails Destination
        {
            get
            {
                if (_Destination == null) _Destination = new DestinationDetails();
                return _Destination;
            }
            set
            {
                _Destination = value ?? new DestinationDetails();
            }
        }

        /// <summary>
        /// Request method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// Raw HTTP method string.
        /// </summary>
        public string MethodRaw { get; set; } = null;

        /// <summary>
        /// Raw request target as received on the request line.
        /// </summary>
        public string RawUrl { get; set; } = null;

        /// <summary>
        /// Protocol version string.
        /// </summary>
        public string ProtocolVersion { get; set; } = "HTTP/1.1";

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection Headers
        {
            get
            {
                if (_Headers == null)
                {
                    _Headers = MaterializeHeaders();
                }

                return _Headers;
            }
            set
            {
                _Headers = value ?? new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// URL details.
        /// </summary>
        public UrlDetails Url
        {
            get
            {
                if (_Url == null && !String.IsNullOrEmpty(RawUrl))
                {
                    _Url = new UrlDetails(RawUrl, _Scheme, _Host, _Port);
                }

                return _Url;
            }
            set
            {
                _Url = value ?? new UrlDetails();
            }
        }

        /// <summary>
        /// Query details.
        /// </summary>
        public QueryDetails Query
        {
            get
            {
                if (_QueryEvaluated) return _Query;

                if (!String.IsNullOrEmpty(RawUrl)
                    && RawUrl.IndexOf('?', StringComparison.Ordinal) >= 0)
                {
                    _Query = new QueryDetails(RawUrl);
                }

                _QueryEvaluated = true;
                return _Query;
            }
            set
            {
                _Query = value;
                _QueryEvaluated = true;
            }
        }

        /// <summary>
        /// Indicates whether the client requested keepalive.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether the client sent an Expect: 100-continue header.
        /// </summary>
        public bool ExpectContinue { get; set; } = false;

        /// <summary>
        /// Indicates whether chunked transfer encoding is in use.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Indicates whether gzip content encoding is in use.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether deflate content encoding is in use.
        /// </summary>
        public bool Deflate { get; set; } = false;

        /// <summary>
        /// User-Agent header value.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// Content-Type header value.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Content-Length header value.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        internal void SetUrlParts(string scheme, string host, int port)
        {
            _Scheme = scheme;
            _Host = host;
            _Port = port;
        }

        internal void InitializeParsedHeaders(byte[] headerBytes, HeaderSlice[] headerSlices)
        {
            _HeaderBytes = headerBytes ?? Array.Empty<byte>();
            _HeaderSlices = headerSlices ?? Array.Empty<HeaderSlice>();
            _Headers = null;
        }

        /// <summary>
        /// Determine if a header exists without forcing full header materialization.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>True if the header exists.</returns>
        public bool HeaderExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (_Headers != null)
            {
                return _Headers.Get(key) != null;
            }

            for (int i = 0; i < _HeaderSlices.Length; i++)
            {
                if (HeaderNameEquals(_HeaderSlices[i], key)) return true;
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header value without forcing full header materialization.
        /// </summary>
        /// <param name="key">Header key.</param>
        /// <returns>Header value if found.</returns>
        public string RetrieveHeaderValue(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (_Headers != null)
            {
                return _Headers.Get(key);
            }

            for (int i = 0; i < _HeaderSlices.Length; i++)
            {
                if (HeaderNameEquals(_HeaderSlices[i], key))
                {
                    return DecodeValue(_HeaderSlices[i]);
                }
            }

            return null;
        }

        private bool HeaderNameEquals(HeaderSlice slice, string key)
        {
            int keyLength = key.Length;
            if (slice.NameLength != keyLength) return false;
            if (_HeaderBytes == null || _HeaderBytes.Length < (slice.NameOffset + slice.NameLength)) return false;

            ReadOnlySpan<byte> name = new ReadOnlySpan<byte>(_HeaderBytes, slice.NameOffset, slice.NameLength);
            for (int i = 0; i < keyLength; i++)
            {
                byte current = name[i];
                char expected = key[i];

                if (current >= (byte)'A' && current <= (byte)'Z') current = (byte)(current + 32);
                if (expected >= 'A' && expected <= 'Z') expected = (char)(expected + 32);
                if (current != (byte)expected) return false;
            }

            return true;
        }

        private string DecodeValue(HeaderSlice slice)
        {
            if (_HeaderBytes == null || slice.ValueLength < 1) return String.Empty;
            return Encoding.ASCII.GetString(_HeaderBytes, slice.ValueOffset, slice.ValueLength);
        }

        private NameValueCollection MaterializeHeaders()
        {
            NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            if (_HeaderBytes == null || _HeaderSlices == null || _HeaderSlices.Length < 1)
            {
                return headers;
            }

            for (int i = 0; i < _HeaderSlices.Length; i++)
            {
                HeaderSlice slice = _HeaderSlices[i];
                string key = Encoding.ASCII.GetString(_HeaderBytes, slice.NameOffset, slice.NameLength);
                string value = slice.ValueLength > 0
                    ? Encoding.ASCII.GetString(_HeaderBytes, slice.ValueOffset, slice.ValueLength)
                    : String.Empty;
                headers.Add(key, value);
            }

            return headers;
        }

        private SourceDetails _Source = null;
        private DestinationDetails _Destination = null;
        private UrlDetails _Url = null;
        private QueryDetails _Query = null;
        private bool _QueryEvaluated = false;
        private NameValueCollection _Headers = null;
        private string _Scheme = "http";
        private string _Host = "localhost";
        private int _Port = 80;
        private byte[] _HeaderBytes = Array.Empty<byte>();
        private HeaderSlice[] _HeaderSlices = Array.Empty<HeaderSlice>();

        internal readonly struct HeaderSlice
        {
            internal HeaderSlice(int nameOffset, int nameLength, int valueOffset, int valueLength)
            {
                NameOffset = nameOffset;
                NameLength = nameLength;
                ValueOffset = valueOffset;
                ValueLength = valueLength;
            }

            internal int NameOffset { get; }
            internal int NameLength { get; }
            internal int ValueOffset { get; }
            internal int ValueLength { get; }
        }
    }
}
