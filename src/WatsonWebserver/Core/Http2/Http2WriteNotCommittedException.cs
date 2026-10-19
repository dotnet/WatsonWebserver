namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.Threading;

    /// <summary>The rejected batch wrote no bytes to the connection.</summary>
    internal sealed class Http2WriteNotCommittedException : OperationCanceledException
    {
        internal Http2WriteNotCommittedException(CancellationToken token)
            : base("HTTP/2 stream write was cancelled before commit.", token) { }
    }
}
