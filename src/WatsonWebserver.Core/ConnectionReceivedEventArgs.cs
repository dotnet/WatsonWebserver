namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Connection event arguments.
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        #region Public-Members

        /// <summary>
        /// Requestor IP address.
        /// </summary>
        public string Ip { get; set; } = null;

        /// <summary>
        /// Request TCP port.
        /// </summary>
        public int Port { get; set; } = 0;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
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

        #endregion

        #region Public-Members

        #endregion

        #region Private-Members

        #endregion
    }
}
