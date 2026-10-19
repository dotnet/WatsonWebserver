namespace WatsonWebserver.Core.Http2
{
    using System.Threading;

    /// <summary>Atomic ordering between complete batches and stream termination.</summary>
    internal sealed class Http2StreamWriteGate
    {
        // 0 active, 1 committing, 2 terminal, 3 committing with termination pending.
        private int _State;
        internal bool IsTerminal => (Volatile.Read(ref _State) & 2) != 0;
        internal bool TryCommit() => Interlocked.CompareExchange(ref _State, 1, 0) == 0;
        internal void Complete()
        {
            if (Interlocked.CompareExchange(ref _State, 0, 1) != 1)
                Interlocked.CompareExchange(ref _State, 2, 3);
        }
        internal bool Terminate()
        {
            while (true)
            {
                int state = Volatile.Read(ref _State);
                if ((state & 2) != 0) return false;
                if (Interlocked.CompareExchange(ref _State, state | 2, state) == state) return true;
            }
        }
    }
}
