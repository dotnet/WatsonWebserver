namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Hosts a Watson server bound to a loopback port for test use.
    /// </summary>
    public class LoopbackServerHost : IDisposable
    {
        private static readonly object _PortSync = new object();
        private static readonly HashSet<int> _ReservedPorts = new HashSet<int>();
        private readonly int _Port;
        private readonly X509Certificate2 _Certificate;
        private readonly Webserver _Server;

        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="enableTls">Enable TLS.</param>
        /// <param name="enableHttp2">Enable HTTP/2.</param>
        /// <param name="enableHttp3">Enable HTTP/3.</param>
        /// <param name="configureRoutes">Route configuration callback.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configureRoutes"/> is null.</exception>
        public LoopbackServerHost(bool enableTls, bool enableHttp2, bool enableHttp3, Action<Webserver> configureRoutes)
        {
            if (configureRoutes == null) throw new ArgumentNullException(nameof(configureRoutes));

            _Port = GetAvailablePort();

            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, enableTls);
            settings.IO.EnableKeepAlive = true;
            settings.IO.MaxRequests = 512;
            settings.IO.ReadTimeoutMs = 30000;
            settings.Protocols.IdleTimeoutMs = 30000;
            settings.Protocols.EnableHttp2 = enableHttp2;
            settings.Protocols.EnableHttp3 = enableHttp3;
            settings.Protocols.EnableHttp2Cleartext = !enableTls && enableHttp2;

            if (enableTls)
            {
                _Certificate = LoopbackCertificateFactory.Create("localhost");
                settings.Ssl.SslCertificate = _Certificate;
            }

            _Server = new Webserver(settings, DefaultRouteAsync);
            configureRoutes(_Server);
        }

        /// <summary>
        /// The allocated loopback port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
        }

        /// <summary>
        /// The hosted server instance.
        /// </summary>
        public Webserver Server
        {
            get
            {
                return _Server;
            }
        }

        /// <summary>
        /// Base address.
        /// </summary>
        public Uri BaseAddress
        {
            get
            {
                string scheme = _Server.Settings.Ssl.Enable ? "https" : "http";
                return new Uri(scheme + "://127.0.0.1:" + _Port.ToString());
            }
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync()
        {
            _Server.Start();
            await Task.Delay(250).ConfigureAwait(false);
        }

        /// <summary>
        /// Dispose the server host.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _Server.Stop();
            }
            catch
            {
            }

            _Server.Dispose();

            if (_Certificate != null)
            {
                _Certificate.Dispose();
            }

            ReleasePort(_Port);
        }

        private static Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            return context.Response.Send("not-found", context.Token);
        }

        private static int GetAvailablePort()
        {
            while (true)
            {
                using (TcpListener listener = new TcpListener(IPAddress.Loopback, 0))
                {
                    listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                    using (UdpClient datagramListener = new UdpClient(AddressFamily.InterNetwork))
                    lock (_PortSync)
                    {
                        try
                        {
                            datagramListener.Client.Bind(new IPEndPoint(IPAddress.Loopback, port));
                        }
                        catch (SocketException)
                        {
                            continue;
                        }

                        if (_ReservedPorts.Contains(port))
                        {
                            continue;
                        }

                        _ReservedPorts.Add(port);
                        return port;
                    }
                }
            }
        }

        private static void ReleasePort(int port)
        {
            if (port < 1) return;

            lock (_PortSync)
            {
                _ReservedPorts.Remove(port);
            }
        }
    }
}
