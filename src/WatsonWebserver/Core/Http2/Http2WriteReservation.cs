namespace WatsonWebserver.Core.Http2
{
    using System.Threading;

    /// <summary>Exactly one of commit or refund owns reserved connection credit.</summary>
    internal sealed class Http2WriteReservation
    {
        private int _Count;
        internal void Add(int count) => Interlocked.Add(ref _Count, count);
        internal int Claim() => Interlocked.Exchange(ref _Count, 0);
    }
}
