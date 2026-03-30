namespace Test.CurlInterop
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Runs curl interoperability checks against a live Watson server.
    /// </summary>
    internal sealed class CurlInteropHarness : IDisposable
    {
        private readonly string _CurlExecutable;
        private readonly int _Port;
        private readonly string _HelloPayload = "curl-interop-hello";
        private readonly string _EchoPayload = "curl-interop-echo";
        private readonly string _ServerSentEventPayload = "data: curl-interop-event\n\n";
        private readonly X509Certificate2 _Certificate;
        private Webserver _Server = null;

        /// <summary>
        /// Instantiate the harness.
        /// </summary>
        public CurlInteropHarness(string curlExecutable = null)
        {
            _CurlExecutable = !string.IsNullOrEmpty(curlExecutable) ? curlExecutable : "curl.exe";
            _Port = GetAvailablePort();
            _Certificate = CreateCertificate();
        }

        /// <summary>
        /// HTTPS base URL.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return "https://127.0.0.1:" + _Port.ToString();
            }
        }

        /// <summary>
        /// Start the Watson server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token)
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", _Port, true);
            settings.Ssl.SslCertificate = _Certificate;
            settings.Protocols.EnableHttp2 = true;
            settings.Protocols.EnableHttp3 = true;
            settings.AltSvc.Enabled = true;
            settings.AltSvc.Http3Alpn = "h3";
            settings.AltSvc.MaxAgeSeconds = 3600;
            settings.IO.MaxRequests = 256;

            _Server = new Webserver(settings, DefaultRouteAsync);
            _Server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/hello", HelloRouteAsync);
            _Server.Routes.PostAuthentication.Static.Add(HttpMethod.POST, "/benchmark/echo", EchoRouteAsync);
            _Server.Routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/sse", ServerSentEventsRouteAsync);
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

            if (_Certificate != null)
            {
                _Certificate.Dispose();
            }
        }

        /// <summary>
        /// Detect curl capabilities.
        /// </summary>
        /// <returns>Capabilities.</returns>
        public CurlCapabilities DetectCapabilities()
        {
            CurlCapabilities capabilities = new CurlCapabilities();

            try
            {
                CurlCommandResult result = InvokeCurl("--version", 10000);
                capabilities.IsAvailable = result.ExitCode == 0;
                capabilities.VersionOutput = result.StandardOutput + result.StandardError;
                capabilities.SupportsHttp2 = capabilities.VersionOutput.IndexOf("HTTP2", StringComparison.OrdinalIgnoreCase) >= 0;
                capabilities.SupportsHttp3 = capabilities.VersionOutput.IndexOf("HTTP3", StringComparison.OrdinalIgnoreCase) >= 0;
                capabilities.SupportsAltSvc = capabilities.VersionOutput.IndexOf("alt-svc", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception exception)
            {
                capabilities.IsAvailable = false;
                capabilities.VersionOutput = exception.Message;
            }

            return capabilities;
        }

        /// <summary>
        /// Run a curl command.
        /// </summary>
        /// <param name="arguments">Arguments.</param>
        /// <param name="timeoutMs">Timeout.</param>
        /// <returns>Result.</returns>
        public CurlCommandResult InvokeCurl(string arguments, int timeoutMs = 30000)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = _CurlExecutable;
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();

                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("curl invocation timed out.");
                }

                CurlCommandResult result = new CurlCommandResult();
                result.ExitCode = process.ExitCode;
                result.StandardOutput = process.StandardOutput.ReadToEnd();
                result.StandardError = process.StandardError.ReadToEnd();
                return result;
            }
        }

        /// <summary>
        /// Expected hello payload.
        /// </summary>
        public string HelloPayload
        {
            get
            {
                return _HelloPayload;
            }
        }

        /// <summary>
        /// Expected echo payload.
        /// </summary>
        public string EchoPayload
        {
            get
            {
                return _EchoPayload;
            }
        }

        /// <summary>
        /// Expected server-sent event payload.
        /// </summary>
        public string ServerSentEventPayload
        {
            get
            {
                return _ServerSentEventPayload;
            }
        }

        private Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            return context.Response.Send("not-found", context.Token);
        }

        private Task HelloRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            return context.Response.Send(_HelloPayload, context.Token);
        }

        private Task EchoRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            return context.Response.Send(context.Request.DataAsString ?? string.Empty, context.Token);
        }

        private Task ServerSentEventsRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 200;
            context.Response.ServerSentEvents = true;
            return context.Response.SendEvent(
                new ServerSentEvent
                {
                    Data = "curl-interop-event"
                },
                true,
                context.Token);
        }

        private static int GetAvailablePort()
        {
            System.Net.Sockets.TcpListener listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                System.Net.IPEndPoint endpoint = listener.LocalEndpoint as System.Net.IPEndPoint;
                return endpoint.Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static X509Certificate2 CreateCertificate()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                CertificateRequest request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                SubjectAlternativeNameBuilder subjectAlternativeNames = new SubjectAlternativeNameBuilder();
                subjectAlternativeNames.AddDnsName("localhost");
                subjectAlternativeNames.AddIpAddress(System.Net.IPAddress.Loopback);
                request.CertificateExtensions.Add(subjectAlternativeNames.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                using (X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7)))
                {
                    byte[] exported = certificate.Export(X509ContentType.Pfx);
#if NET10_0_OR_GREATER
                    return X509CertificateLoader.LoadPkcs12(exported, null);
#else
#pragma warning disable SYSLIB0057
                    return new X509Certificate2(exported);
#pragma warning restore SYSLIB0057
#endif
                }
            }
        }
    }
}
