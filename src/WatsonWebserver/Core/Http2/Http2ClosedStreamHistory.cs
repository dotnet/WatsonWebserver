namespace WatsonWebserver.Core.Http2
{
    using System.Collections.Generic;

    /// <summary>
    /// Bounded closure history. Callers serialize access with the session lock.
    /// Evicted history uses RFC 9113 section 5.1 minimal processing so eviction
    /// never turns an in-flight frame after a local reset into a connection error.
    /// </summary>
    internal sealed class Http2ClosedStreamHistory
    {
        private const int _Capacity = 128;
        private readonly Dictionary<int, bool> _LocalResets = new Dictionary<int, bool>();
        private int _EvictedThrough;
        private readonly Queue<int> _Order = new Queue<int>();

        internal void Record(int streamIdentifier, bool locallyReset)
        {
            if (_LocalResets.ContainsKey(streamIdentifier)) return;
            _LocalResets.Add(streamIdentifier, locallyReset);
            _Order.Enqueue(streamIdentifier);
            if (_Order.Count > _Capacity)
            {
                int evicted = _Order.Dequeue();
                _EvictedThrough = System.Math.Max(_EvictedThrough, evicted);
                _LocalResets.Remove(evicted);
            }
        }

        // Recent IDs skipped by the peer were implicitly closed, not reset.
        internal bool IsImplicitlyClosed(int streamIdentifier)
        {
            return streamIdentifier > _EvictedThrough && !_LocalResets.ContainsKey(streamIdentifier);
        }

        internal bool AllowsLateFrames(int streamIdentifier)
        {
            return _LocalResets.TryGetValue(streamIdentifier, out bool locallyReset)
                ? locallyReset : streamIdentifier <= _EvictedThrough;
        }
    }
}
