namespace WatsonWebserver.Core
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Timestamps;

    /// <summary>
    /// Source details.
    /// </summary>
    public class SourceDetails
    {
        #region Public-Members

        /// <summary>
        /// IP address of the requestor.
        /// </summary>
        public string IpAddress { get; set; } = null;

        /// <summary>
        /// TCP port from which the request originated on the requestor.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
                _Port = value;
            }
        }

        #endregion

        #region Private-Members

        private int _Port { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Source details.
        /// </summary>
        public SourceDetails()
        {

        }

        /// <summary>
        /// Source details.
        /// </summary>
        /// <param name="ip">IP address of the requestor.</param>
        /// <param name="port">TCP port from which the request originated on the requestor.</param>
        public SourceDetails(string ip, int port)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));

            IpAddress = ip;
            Port = port;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
