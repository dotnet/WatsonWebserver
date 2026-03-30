namespace Test.BrowserInterop
{
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Allocates ports for local browser interop hosts.
    /// </summary>
    internal static class BrowserPortFactory
    {
        /// <summary>
        /// Get an available loopback port.
        /// </summary>
        /// <returns>Port number.</returns>
        public static int GetAvailablePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);

            try
            {
                listener.Start();
                IPEndPoint endpoint = listener.LocalEndpoint as IPEndPoint;
                return endpoint.Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}
