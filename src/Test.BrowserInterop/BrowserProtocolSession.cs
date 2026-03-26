namespace Test.BrowserInterop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Playwright;

    /// <summary>
    /// Playwright browser session with Chromium CDP protocol observation.
    /// </summary>
    internal sealed class BrowserProtocolSession : IAsyncDisposable
    {
        private readonly string _ExecutablePath;
        private readonly string _HostName;
        private readonly string _CertificatePin;
        private readonly string _ForcedQuicOrigin;
        private readonly string _UserDataDirectory;
        private readonly bool _OwnsUserDataDirectory;
        private IPlaywright _Playwright = null;
        private IBrowserContext _Context = null;
        private IPage _Page = null;
        private ICDPSession _CdpSession = null;

        /// <summary>
        /// Instantiate the session.
        /// </summary>
        /// <param name="executablePath">Browser executable path.</param>
        /// <param name="hostName">Optional host name to map to loopback.</param>
        /// <param name="certificatePin">Optional browser SPKI certificate pin.</param>
        /// <param name="forcedQuicOrigin">Optional host:port origin to force onto QUIC.</param>
        public BrowserProtocolSession(string executablePath, string hostName = null, string certificatePin = null, string forcedQuicOrigin = null, string userDataDirectory = null)
        {
            if (String.IsNullOrEmpty(executablePath)) throw new ArgumentNullException(nameof(executablePath));
            _ExecutablePath = executablePath;
            _HostName = hostName;
            _CertificatePin = certificatePin;
            _ForcedQuicOrigin = forcedQuicOrigin;
            if (!String.IsNullOrEmpty(userDataDirectory))
            {
                _UserDataDirectory = userDataDirectory;
                _OwnsUserDataDirectory = false;
            }
            else
            {
                _UserDataDirectory = Path.Combine(Path.GetTempPath(), "watson-browser-interop-" + Guid.NewGuid().ToString("N"));
                _OwnsUserDataDirectory = true;
            }
        }

        /// <summary>
        /// Start the session.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task StartAsync(CancellationToken token)
        {
            _Playwright = await Playwright.CreateAsync().ConfigureAwait(false);

            BrowserTypeLaunchPersistentContextOptions launchOptions = new BrowserTypeLaunchPersistentContextOptions();
            launchOptions.ExecutablePath = _ExecutablePath;
            launchOptions.Headless = true;
            List<string> args = new List<string>
            {
                "--enable-quic",
                "--ignore-certificate-errors"
            };

            if (!String.IsNullOrEmpty(_HostName))
            {
                args.Add("--host-resolver-rules=MAP " + _HostName + " 127.0.0.1");
            }

            if (!String.IsNullOrEmpty(_CertificatePin))
            {
                args.Add("--ignore-certificate-errors-spki-list=" + _CertificatePin);
            }

            if (!String.IsNullOrEmpty(_ForcedQuicOrigin))
            {
                args.Add("--origin-to-force-quic-on=" + _ForcedQuicOrigin);
            }

            launchOptions.Args = args;
            launchOptions.IgnoreHTTPSErrors = true;
            _Context = await _Playwright.Chromium.LaunchPersistentContextAsync(_UserDataDirectory, launchOptions).ConfigureAwait(false);
            _Page = await _Context.NewPageAsync().ConfigureAwait(false);
            _CdpSession = await _Context.NewCDPSessionAsync(_Page).ConfigureAwait(false);
            await _CdpSession.SendAsync("Network.enable").ConfigureAwait(false);
        }

        /// <summary>
        /// Navigate and capture the main-document network protocol.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Navigation observation.</returns>
        public async Task<BrowserNavigationObservation> NavigateAsync(string url, CancellationToken token)
        {
            if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
            if (_Page == null || _CdpSession == null) throw new InvalidOperationException("Browser session is not started.");

            BrowserNavigationWaiter waiter = new BrowserNavigationWaiter(url);
            EventHandler<JsonElement?> handler = (sender, args) =>
            {
                if (!args.HasValue) return;

                BrowserNavigationObservation observation = BrowserNavigationResponseParser.Parse(args.Value);
                if (observation == null) return;
                waiter.TrySetObservation(observation);
            };

            _CdpSession.Event("Network.responseReceived").OnEvent += handler;

            try
            {
                PageGotoOptions gotoOptions = new PageGotoOptions();
                gotoOptions.WaitUntil = WaitUntilState.Load;
                gotoOptions.Timeout = 30000;
                await _Page.GotoAsync(url, gotoOptions).ConfigureAwait(false);
                BrowserNavigationObservation observation = await waiter.WaitAsync(token).ConfigureAwait(false);
                observation.BodyText = await _Page.TextContentAsync("body").ConfigureAwait(false) ?? String.Empty;
                return observation;
            }
            finally
            {
                _CdpSession.Event("Network.responseReceived").OnEvent -= handler;
            }
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_Page != null)
            {
                await _Page.CloseAsync().ConfigureAwait(false);
                _Page = null;
            }

            if (_Context != null)
            {
                await _Context.CloseAsync().ConfigureAwait(false);
                _Context = null;
            }

            _Playwright?.Dispose();
            _Playwright = null;

            if (_OwnsUserDataDirectory && Directory.Exists(_UserDataDirectory))
            {
                try
                {
                    Directory.Delete(_UserDataDirectory, true);
                }
                catch (Exception)
                {
                }
            }
        }

        private sealed class BrowserNavigationWaiter
        {
            private readonly string _Url;
            private readonly TaskCompletionSource<BrowserNavigationObservation> _TaskCompletionSource = new TaskCompletionSource<BrowserNavigationObservation>(TaskCreationOptions.RunContinuationsAsynchronously);

            public BrowserNavigationWaiter(string url)
            {
                if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
                _Url = url;
            }

            public void TrySetObservation(BrowserNavigationObservation observation)
            {
                if (observation == null) return;
                if (!String.Equals(observation.Url, _Url, StringComparison.OrdinalIgnoreCase)
                    && !observation.Url.StartsWith(_Url, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _TaskCompletionSource.TrySetResult(observation);
            }

            public Task<BrowserNavigationObservation> WaitAsync(CancellationToken token)
            {
                if (!token.CanBeCanceled) return _TaskCompletionSource.Task;
                return _TaskCompletionSource.Task.WaitAsync(token);
            }
        }
    }
}
