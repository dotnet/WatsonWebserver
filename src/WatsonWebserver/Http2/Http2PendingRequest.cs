namespace WatsonWebserver.Http2
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using WatsonWebserver.Core;

    /// <summary>
    /// Pending HTTP/2 request state awaiting END_STREAM.
    /// </summary>
    internal class Http2PendingRequest : IDisposable
    {
        /// <summary>
        /// Parsed request method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// Raw request method token.
        /// </summary>
        public string MethodRaw
        {
            get
            {
                return _MethodRaw;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(MethodRaw));
                _MethodRaw = value;
            }
        }

        /// <summary>
        /// Request URL scheme.
        /// </summary>
        public string Scheme
        {
            get
            {
                return _Scheme;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Scheme));
                _Scheme = value;
            }
        }

        /// <summary>
        /// Request authority value.
        /// </summary>
        public string Authority { get; set; } = null;

        /// <summary>
        /// Request path and optional query string.
        /// </summary>
        public string Path
        {
            get
            {
                return _Path;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Path));
                _Path = value;
            }
        }

        /// <summary>
        /// Regular request headers decoded from the header block.
        /// </summary>
        public HttpHeaderField[] Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Headers));
                _Headers = value;
            }
        }

        /// <summary>
        /// Request trailers decoded after the initial header block.
        /// </summary>
        public NameValueCollection Trailers
        {
            get
            {
                return _Trailers;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Trailers));
                _Trailers = value;
            }
        }

        /// <summary>
        /// Request body buffer.
        /// </summary>
        public MemoryStream Body
        {
            get
            {
                if (_ExactBodyBuffer != null)
                {
                    if (_Body == null)
                    {
                        _Body = new MemoryStream(_ExactBodyBuffer, 0, _ExactBodyBytesWritten, writable: false, publiclyVisible: true);
                    }

                    return _Body;
                }

                if (_Body == null)
                {
                    _Body = new MemoryStream();
                }

                return _Body;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Body));
                _Body = value;
                _ExactBodyBuffer = null;
                _ExactBodyBytesWritten = 0;
            }
        }

        /// <summary>
        /// Request body buffer when any body bytes have been received; otherwise null.
        /// </summary>
        public MemoryStream BodyOrNull
        {
            get
            {
                if (_ExactBodyBuffer != null)
                {
                    if (_ExactBodyBytesWritten < 1) return null;
                    return Body;
                }

                return _Body;
            }
        }

        /// <summary>
        /// Declared content length when supplied by the peer.
        /// </summary>
        public long? ExpectedContentLength { get; set; } = null;

        /// <summary>
        /// Number of body bytes currently buffered.
        /// </summary>
        public long BodyLength
        {
            get
            {
                if (_ExactBodyBuffer != null) return _ExactBodyBytesWritten;
                return _Body != null ? _Body.Length : 0;
            }
        }

        internal MemoryStream DetachBodyStream()
        {
            MemoryStream body = BodyOrNull;
            _Body = null;
            _ExactBodyBuffer = null;
            _ExactBodyBytesWritten = 0;
            return body;
        }

        internal void ReleaseResources()
        {
            if (_Body != null)
            {
                _Body.Dispose();
                _Body = null;
            }

            _ExactBodyBuffer = null;
            _ExactBodyBytesWritten = 0;
        }

        public void Dispose()
        {
            ReleaseResources();
        }

        internal void InitializeExactBodyBuffer(int contentLength)
        {
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));

            _ExactBodyBuffer = contentLength > 0 ? new byte[contentLength] : Array.Empty<byte>();
            _ExactBodyBytesWritten = 0;
            _Body = null;
        }

        internal void AppendBody(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((offset + count) > buffer.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (count < 1) return;

            if (_ExactBodyBuffer != null)
            {
                if ((_ExactBodyBytesWritten + count) > _ExactBodyBuffer.Length)
                {
                    throw new IOException("Request body exceeds the declared content length.");
                }

                Buffer.BlockCopy(buffer, offset, _ExactBodyBuffer, _ExactBodyBytesWritten, count);
                _ExactBodyBytesWritten += count;
                _Body = null;
                return;
            }

            Body.Write(buffer, offset, count);
        }

        private string _MethodRaw = "GET";
        private string _Scheme = "http";
        private string _Path = "/";
        private HttpHeaderField[] _Headers = Array.Empty<HttpHeaderField>();
        private NameValueCollection _Trailers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
        private MemoryStream _Body = null;
        private byte[] _ExactBodyBuffer = null;
        private int _ExactBodyBytesWritten = 0;
    }
}
