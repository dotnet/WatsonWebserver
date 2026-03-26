namespace Test.BrowserInterop
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Runs browser interoperability checks against a live Watson server.
    /// </summary>
    internal sealed class BrowserInteropHarness : IDisposable
    {
        private readonly BrowserLocalEndpoint _Endpoint;
        private readonly int _Port;
        private readonly BrowserTemporaryCertificateAuthority _CertificateAuthority;
        private Webserver _Server = null;

        /// <summary>
        /// Instantiate the harness.
        /// </summary>
        public BrowserInteropHarness()
        {
            _Endpoint = BrowserLocalEndpointResolver.Resolve();
            _Port = BrowserPortFactory.GetAvailablePort();
            _CertificateAuthority = new BrowserTemporaryCertificateAuthority(_Endpoint.Hostname);
        }

        /// <summary>
        /// HTTPS base URL.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return "https://" + _Endpoint.Hostname + ":" + _Port.ToString();
            }
        }

        /// <summary>
        /// HTTPS test hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Endpoint.Hostname;
            }
        }

        /// <summary>
        /// HTTPS origin authority.
        /// </summary>
        public string OriginAuthority
        {
            get
            {
                return _Endpoint.Hostname + ":" + _Port.ToString();
            }
        }

        /// <summary>
        /// Browser SPKI certificate pin.
        /// </summary>
        public string CertificatePin
        {
            get
            {
                return _CertificateAuthority.CertificatePin;
            }
        }

        /// <summary>
        /// Start the Watson server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token)
        {
            WebserverSettings settings = new WebserverSettings(_Endpoint.BindAddress, _Port, true);
            settings.Ssl.SslCertificate = _CertificateAuthority.ServerCertificate;
            settings.Protocols.EnableHttp2 = true;
            settings.Protocols.EnableHttp3 = true;
            settings.AltSvc.Enabled = true;
            settings.AltSvc.Http3Alpn = "h3";
            settings.AltSvc.MaxAgeSeconds = 3600;
            settings.IO.MaxRequests = 256;

            _Server = new Webserver(settings, DefaultRouteAsync);
            _Server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/browser", BrowserRouteAsync);
            _Server.Start(token);
            return Task.Delay(250, token);
        }

        /// <summary>
        /// Stop the Watson server.
        /// </summary>
        public void Stop()
        {
            if (_Server != null)
            {
                _Server.Stop();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Server != null)
            {
                _Server.Dispose();
                _Server = null;
            }

            if (_CertificateAuthority != null)
            {
                _CertificateAuthority.Dispose();
            }
        }

        private Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            return context.Response.Send("not-found", context.Token);
        }

        private Task BrowserRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/html; charset=utf-8";
            return context.Response.Send("<html><body>browser-interop-ok</body></html>", context.Token);
        }
    }
}
