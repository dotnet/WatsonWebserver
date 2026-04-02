namespace WatsonWebserver.Http1
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only stream wrapper that enforces a content-length bound.
    /// After the specified number of bytes have been read the stream
    /// returns zero (EOF) instead of blocking on the inner stream.
    /// </summary>
    internal class ContentLengthStream : Stream
    {
        private readonly Stream _InnerStream;
        private readonly long _ContentLength;
        private long _BytesRemaining;

        /// <summary>
        /// Instantiate the stream wrapper.
        /// </summary>
        /// <param name="innerStream">Inner stream to read from.</param>
        /// <param name="contentLength">Maximum number of bytes to read.</param>
        public ContentLengthStream(Stream innerStream, long contentLength)
        {
            if (innerStream == null) throw new ArgumentNullException(nameof(innerStream));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));

            _InnerStream = innerStream;
            _ContentLength = contentLength;
            _BytesRemaining = contentLength;
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override long Length
        {
            get
            {
                return _ContentLength;
            }
        }

        /// <inheritdoc />
        public override long Position
        {
            get
            {
                return _ContentLength - _BytesRemaining;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((buffer.Length - offset) < count) throw new ArgumentException("The supplied buffer is too small.");

            if (_BytesRemaining <= 0) return 0;

            int toRead = (int)Math.Min(count, _BytesRemaining);
            int bytesRead = _InnerStream.Read(buffer, offset, toRead);
            _BytesRemaining -= bytesRead;
            return bytesRead;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((buffer.Length - offset) < count) throw new ArgumentException("The supplied buffer is too small.");

            if (_BytesRemaining <= 0) return 0;

            int toRead = (int)Math.Min(count, _BytesRemaining);
            int bytesRead = await _InnerStream.ReadAsync(buffer, offset, toRead, cancellationToken).ConfigureAwait(false);
            _BytesRemaining -= bytesRead;
            return bytesRead;
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// The number of bytes remaining before EOF.
        /// </summary>
        internal long BytesRemaining
        {
            get
            {
                return _BytesRemaining;
            }
        }

        /// <summary>
        /// The inner wrapped stream.
        /// </summary>
        internal Stream InnerStream
        {
            get
            {
                return _InnerStream;
            }
        }
    }
}
