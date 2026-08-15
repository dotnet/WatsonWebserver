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
        private const int _MaxStartAttempts = 6;

        private static readonly object _PortSync = new object();
        private static readonly HashSet<int> _ReservedPorts = new HashSet<int>();

        private readonly bool _EnableTls;
        private readonly bool _EnableHttp2;
        private readonly bool _EnableHttp3;
        private readonly Action<Webserver> _ConfigureRoutes;
        private readonly Action<WebserverSettings> _ConfigureSettings;

        private int _Port;
        private X509Certificate2 _Certificate;
        private Webserver _Server;

        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="enableTls">Enable TLS.</param>
        /// <param name="enableHttp2">Enable HTTP/2.</param>
        /// <param name="enableHttp3">Enable HTTP/3.</param>
        /// <param name="configureRoutes">Route configuration callback.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configureRoutes"/> is null.</exception>
        public LoopbackServerHost(bool enableTls, bool enableHttp2, bool enableHttp3, Action<Webserver> configureRoutes)
            : this(enableTls, enableHttp2, enableHttp3, configureRoutes, null)
        {
        }

        /// <summary>
        /// Instantiate the host with an optional settings-configuration callback invoked before the
        /// server is constructed.
        /// </summary>
        /// <param name="enableTls">Enable TLS.</param>
        /// <param name="enableHttp2">Enable HTTP/2.</param>
        /// <param name="enableHttp3">Enable HTTP/3.</param>
        /// <param name="configureRoutes">Route configuration callback.</param>
        /// <param name="configureSettings">Optional callback to mutate settings before construction.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configureRoutes"/> is null.</exception>
        public LoopbackServerHost(bool enableTls, bool enableHttp2, bool enableHttp3, Action<Webserver> configureRoutes, Action<WebserverSettings> configureSettings)
        {
            if (configureRoutes == null) throw new ArgumentNullException(nameof(configureRoutes));

            _EnableTls = enableTls;
            _EnableHttp2 = enableHttp2;
            _EnableHttp3 = enableHttp3;
            _ConfigureRoutes = configureRoutes;
            _ConfigureSettings = configureSettings;

            BuildServer();
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
        /// Start the server. When the transport fails to bind its listener because of a transient
        /// loopback port collision (most common with HTTP/3 over QUIC), the server is rebuilt on a
        /// fresh port and startup is retried a bounded number of times.
        /// </summary>
        /// <returns>Task.</returns>
        public async Task StartAsync()
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    _Server.Start();
                    await Task.Delay(250).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt >= _MaxStartAttempts || !IsTransientBindFailure(ex))
                    {
                        throw;
                    }

                    RebuildAfterFailedStart();
                    await Task.Delay(150).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Dispose the server host.
        /// </summary>
        public void Dispose()
        {
            DisposeServer();

            // Intentionally do NOT release the port back into the pool. Reusing a just-released
            // loopback port can race with the operating system finishing teardown of the previous
            // server's UDP/QUIC socket, which surfaces as "Only one usage of each socket address is
            // normally permitted." Ports are cheap; never recycling them within a single test
            // process keeps HTTP/3 allocation deterministic.
        }

        private void BuildServer()
        {
            _Port = GetAvailablePort();

            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, _EnableTls);
            settings.IO.EnableKeepAlive = true;
            settings.IO.MaxRequests = 512;
            settings.IO.ReadTimeoutMs = 30000;
            settings.Protocols.IdleTimeoutMs = 30000;
            settings.Protocols.EnableHttp2 = _EnableHttp2;
            settings.Protocols.EnableHttp3 = _EnableHttp3;
            settings.Protocols.EnableHttp2Cleartext = !_EnableTls && _EnableHttp2;

            if (_EnableTls)
            {
                _Certificate = LoopbackCertificateFactory.Create("localhost");
                settings.Ssl.SslCertificate = _Certificate;
            }

            _ConfigureSettings?.Invoke(settings);

            _Server = new Webserver(settings, DefaultRouteAsync);
            _ConfigureRoutes(_Server);
        }

        private void RebuildAfterFailedStart()
        {
            DisposeServer();
            BuildServer();
        }

        private void DisposeServer()
        {
            if (_Server != null)
            {
                try
                {
                    _Server.Stop();
                }
                catch
                {
                }

                _Server.Dispose();
                _Server = null;
            }

            if (_Certificate != null)
            {
                _Certificate.Dispose();
                _Certificate = null;
            }
        }

        private static bool IsTransientBindFailure(Exception ex)
        {
            Exception current = ex;

            while (current != null)
            {
                if (current is SocketException)
                {
                    return true;
                }

                string typeName = current.GetType().FullName ?? String.Empty;
                if (typeName.IndexOf("Quic", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                string message = current.Message ?? String.Empty;
                if (message.IndexOf("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("address already in use", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                current = current.InnerException;
            }

            return false;
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
    }
}
