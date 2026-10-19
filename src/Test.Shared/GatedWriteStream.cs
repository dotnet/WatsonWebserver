namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>A deterministic transport gate for cancellation and partial-write tests.</summary>
    internal sealed class GatedWriteStream : Stream
    {
        private readonly MemoryStream _Bytes = new MemoryStream();
        private readonly TaskCompletionSource<bool> _Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _Release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _Writes;
        internal int BlockWrite { get; set; } = 1;
        internal bool Fail { get; set; }
        internal Task Entered => _Entered.Task;
        internal byte[] Bytes => _Bytes.ToArray();
        internal void Release() => _Release.TrySetResult(true);
        /// <inheritdoc />
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _Writes) == BlockWrite)
            {
                _Entered.TrySetResult(true);
                await _Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("Injected transport failure.");
            _Bytes.Write(buffer, offset, count);
        }
        /// <inheritdoc />
        public override bool CanRead => false;
        /// <inheritdoc />
        public override bool CanSeek => false;
        /// <inheritdoc />
        public override bool CanWrite => true;
        /// <inheritdoc />
        public override long Length => _Bytes.Length;
        /// <inheritdoc />
        public override long Position { get => _Bytes.Position; set => throw new NotSupportedException(); }
        /// <inheritdoc />
        public override void Flush() { }
        /// <inheritdoc />
        public override Task FlushAsync(CancellationToken token) => Task.CompletedTask;
        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();
        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => _Bytes.Write(buffer, offset, count);
    }
}
