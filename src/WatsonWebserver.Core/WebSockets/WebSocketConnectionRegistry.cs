namespace WatsonWebserver.Core.WebSockets
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading.Tasks;

    /// <summary>
    /// Registry of active WebSocket sessions.
    /// </summary>
    public class WebSocketConnectionRegistry
    {
        private readonly ConcurrentDictionary<Guid, WebSocketSession> _Sessions = new ConcurrentDictionary<Guid, WebSocketSession>();

        /// <summary>
        /// Add or replace a session.
        /// </summary>
        public void Add(WebSocketSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            _Sessions[session.Id] = session;
        }

        /// <summary>
        /// Remove a session.
        /// </summary>
        public bool Remove(Guid guid)
        {
            return _Sessions.TryRemove(guid, out _);
        }

        /// <summary>
        /// List all known sessions.
        /// </summary>
        public IEnumerable<WebSocketSession> List()
        {
            return _Sessions.Values.ToArray();
        }

        /// <summary>
        /// Determine whether the specified session is currently connected.
        /// </summary>
        public bool IsConnected(Guid guid)
        {
            return _Sessions.TryGetValue(guid, out WebSocketSession session) && session != null && session.IsConnected;
        }

        /// <summary>
        /// Disconnect a session by identifier.
        /// </summary>
        public async Task<bool> DisconnectAsync(Guid guid, WebSocketCloseStatus status, string reason)
        {
            if (!_Sessions.TryGetValue(guid, out WebSocketSession session) || session == null)
            {
                return false;
            }

            await session.CloseAsync(status, reason).ConfigureAwait(false);
            return true;
        }
    }
}
