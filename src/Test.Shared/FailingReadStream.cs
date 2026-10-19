namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>A response source that fails after one buffer.</summary>
    internal sealed class FailingReadStream : MemoryStream
    {
        private int _Reads;
        internal FailingReadStream() : base(new byte[100000]) { }
        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            if (Interlocked.Increment(ref _Reads) > 1) throw new IOException("Injected source failure.");
            return base.ReadAsync(buffer, offset, count, token);
        }
    }
}
