using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Connection event arguments.
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// Requestor IP address.
        /// </summary>
        public string Ip { get; private set; } = null;

        /// <summary>
        /// Request TCP port.
        /// </summary>
        public int Port { get; private set; } = 0;

        /// <summary>
        /// Connection event arguments.
        /// </summary>
        /// <param name="ip">Requestor IP address.</param>
        /// <param name="port">Request TCP port.</param>
        public ConnectionEventArgs(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            Ip = ip;
            Port = port;
        }
    }
}
