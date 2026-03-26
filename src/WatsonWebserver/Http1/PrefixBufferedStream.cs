namespace WatsonWebserver.Http1
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stream wrapper that replays prefetched bytes before delegating to the inner stream.
    /// </summary>
    internal class PrefixBufferedStream : Stream
    {
        /// <summary>
        /// Instantiate the stream wrapper.
        /// </summary>
        /// <param name="innerStream">Inner stream.</param>
        /// <param name="prefixBytes">Prefetched bytes to replay first.</param>
        public PrefixBufferedStream(Stream innerStream, byte[] prefixBytes)
        {
            if (innerStream == null) throw new ArgumentNullException(nameof(innerStream));
            if (prefixBytes == null) throw new ArgumentNullException(nameof(prefixBytes));

            _InnerStream = innerStream;
            _PrefixBytes = prefixBytes;
        }

        public override bool CanRead
        {
            get
            {
                return _InnerStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return _InnerStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
            _InnerStream.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _InnerStream.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((buffer.Length - offset) < count) throw new ArgumentException("The supplied buffer is too small.");

            int bytesCopied = CopyPrefixBytes(buffer, offset, count);
            if (bytesCopied == count) return bytesCopied;

            int bytesRead = _InnerStream.Read(buffer, offset + bytesCopied, count - bytesCopied);
            return bytesCopied + bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if ((buffer.Length - offset) < count) throw new ArgumentException("The supplied buffer is too small.");

            int bytesCopied = CopyPrefixBytes(buffer, offset, count);
            if (bytesCopied == count) return bytesCopied;

            int bytesRead = await _InnerStream.ReadAsync(buffer, offset + bytesCopied, count - bytesCopied, cancellationToken).ConfigureAwait(false);
            return bytesCopied + bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            _InnerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _InnerStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _InnerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _InnerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        private readonly Stream _InnerStream;
        private readonly byte[] _PrefixBytes;
        private int _PrefixOffset = 0;

        /// <summary>
        /// Inner wrapped stream.
        /// </summary>
        internal Stream InnerStream
        {
            get
            {
                return _InnerStream;
            }
        }

        /// <summary>
        /// Indicates whether all prefix bytes have been consumed.
        /// </summary>
        internal bool PrefixConsumed
        {
            get
            {
                return _PrefixOffset >= _PrefixBytes.Length;
            }
        }

        private int CopyPrefixBytes(byte[] buffer, int offset, int count)
        {
            int availableBytes = _PrefixBytes.Length - _PrefixOffset;
            if (availableBytes < 1) return 0;

            int bytesToCopy = Math.Min(availableBytes, count);
            Buffer.BlockCopy(_PrefixBytes, _PrefixOffset, buffer, offset, bytesToCopy);
            _PrefixOffset += bytesToCopy;
            return bytesToCopy;
        }
    }
}

