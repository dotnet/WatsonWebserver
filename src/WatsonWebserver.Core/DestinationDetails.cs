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
    /// Destination details.
    /// </summary>
    public class DestinationDetails
    {
        #region Public-Members

        /// <summary>
        /// IP address to which the request was made.
        /// </summary>
        public string IpAddress { get; set; } = null;

        /// <summary>
        /// TCP port on which the request was received.
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

        /// <summary>
        /// Hostname to which the request was directed.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Hostname elements.
        /// </summary>
        public string[] HostnameElements
        {
            get
            {
                string hostname = Hostname;
                string[] ret;

                if (!String.IsNullOrEmpty(hostname))
                {
                    if (!IPAddress.TryParse(hostname, out _))
                    {
                        ret = hostname.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        return ret;
                    }
                    else
                    {
                        ret = new string[1];
                        ret[0] = hostname;
                        return ret;
                    }
                }

                ret = new string[0];
                return ret;
            }
        }

        #endregion

        #region Private-Members

        private int _Port = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Destination details.
        /// </summary>
        public DestinationDetails()
        {

        }

        /// <summary>
        /// Source details.
        /// </summary>
        /// <param name="ip">IP address to which the request was made.</param>
        /// <param name="port">TCP port on which the request was received.</param>
        /// <param name="hostname">Hostname.</param>
        public DestinationDetails(string ip, int port, string hostname)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port < 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));

            IpAddress = ip;
            Port = port;
            Hostname = hostname;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
