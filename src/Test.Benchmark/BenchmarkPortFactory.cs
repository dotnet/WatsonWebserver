namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;

    /// <summary>
    /// Allocates ports for local benchmark hosts.
    /// </summary>
    internal static class BenchmarkPortFactory
    {
        private const int RangeSize = 2000;
        private static readonly object _Lock = new object();
        private static readonly HashSet<int> _ReservedPorts = new HashSet<int>();
        private static readonly Random _Random = new Random();

        /// <summary>
        /// Get an available loopback port in the range reserved for a target.
        /// </summary>
        /// <param name="target">Benchmark target.</param>
        /// <returns>Port number.</returns>
        public static int GetAvailablePort(BenchmarkTarget target)
        {
            int minimumPort = GetMinimumPort(target);
            int maximumPort = minimumPort + RangeSize - 1;

            lock (_Lock)
            {
                for (int attempt = 0; attempt < RangeSize; attempt++)
                {
                    int candidatePort = _Random.Next(minimumPort, maximumPort + 1);

                    if (_ReservedPorts.Contains(candidatePort))
                    {
                        continue;
                    }

                    if (PortIsAvailable(candidatePort))
                    {
                        _ReservedPorts.Add(candidatePort);
                        return candidatePort;
                    }
                }
            }

            throw new InvalidOperationException("No available benchmark port could be allocated.");
        }

        /// <summary>
        /// Release a previously reserved benchmark port.
        /// </summary>
        /// <param name="port">Port number.</param>
        public static void ReleasePort(int port)
        {
            if (port < 1) return;

            lock (_Lock)
            {
                _ReservedPorts.Remove(port);
            }
        }

        private static int GetMinimumPort(BenchmarkTarget target)
        {
            if (target == BenchmarkTarget.Watson6) return 20000;
            if (target == BenchmarkTarget.WatsonLite6) return 23000;
            if (target == BenchmarkTarget.Watson7) return 26000;
            return 29000;
        }

        private static bool PortIsAvailable(int port)
        {
            IPGlobalProperties globalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] activeTcpListeners = globalProperties.GetActiveTcpListeners();
            for (int i = 0; i < activeTcpListeners.Length; i++)
            {
                if (activeTcpListeners[i].Port == port)
                {
                    return false;
                }
            }

            IPEndPoint[] activeUdpListeners = globalProperties.GetActiveUdpListeners();
            for (int i = 0; i < activeUdpListeners.Length; i++)
            {
                if (activeUdpListeners[i].Port == port)
                {
                    return false;
                }
            }

            TcpListener tcpListener = null;
            Socket udpSocket = null;

            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();

                udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                udpSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                if (udpSocket != null)
                {
                    udpSocket.Dispose();
                }

                if (tcpListener != null)
                {
                    tcpListener.Stop();
                }
            }
        }
    }
}
