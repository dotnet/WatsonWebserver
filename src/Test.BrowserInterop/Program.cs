namespace Test.BrowserInterop
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Browser interoperability entry point.
    /// </summary>
    internal static class Program
    {
        private static readonly string _Http3DebugPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "watson-http3-browser-debug.log");

        private static async Task<int> Main(string[] args)
        {
            Environment.SetEnvironmentVariable("WATSON_HTTP3_DEBUG_PATH", _Http3DebugPath);
            System.IO.File.WriteAllText(_Http3DebugPath, String.Empty);
            List<BrowserTestResult> results = new List<BrowserTestResult>();
            BrowserCapabilities capabilities = BrowserCapabilitiesDetector.Detect(args);

            Console.WriteLine("browser available: " + capabilities.IsAvailable.ToString());
            Console.WriteLine("browser name: " + capabilities.BrowserName);
            Console.WriteLine("browser detail: " + capabilities.Detail);

            if (!capabilities.IsAvailable)
            {
                Console.WriteLine("A Chromium browser is required for browser interoperability coverage.");
                Console.WriteLine("Install Microsoft Edge or Google Chrome, or pass --browser <path>.");
                return 1;
            }

            using (BrowserInteropHarness harness = new BrowserInteropHarness())
            {
                Console.WriteLine("browser origin: " + harness.BaseUrl);
                Console.WriteLine();
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                using (BrowserUserDataDirectory userDataDirectory = new BrowserUserDataDirectory())
                {
                    await harness.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);

                    try
                    {
                        await using (BrowserProtocolSession session = new BrowserProtocolSession(capabilities.ExecutablePath, null, harness.CertificatePin, null, userDataDirectory.Path))
                        {
                            await session.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                            results.Add(await TestAltSvcHeaderAsync(harness, session, cancellationTokenSource.Token).ConfigureAwait(false));
                            results.Add(await TestAltSvcHttp3DiscoveryAsync(harness, session, cancellationTokenSource.Token).ConfigureAwait(false));
                        }

                        await using (BrowserProtocolSession session = new BrowserProtocolSession(capabilities.ExecutablePath, null, harness.CertificatePin, harness.OriginAuthority, userDataDirectory.Path))
                        {
                            await session.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                            results.Add(await TestForcedHttp3Async(harness, session, cancellationTokenSource.Token).ConfigureAwait(false));
                        }
                    }
                    finally
                    {
                        harness.Stop();
                    }
                }
            }

            int passed = 0;
            int failed = 0;
            int skipped = 0;

            for (int i = 0; i < results.Count; i++)
            {
                BrowserTestResult result = results[i];
                string status = result.Skipped ? "SKIP" : (result.Passed ? "PASS" : "FAIL");
                Console.WriteLine(status + " " + result.Name + " - " + result.Detail);

                if (result.Skipped) skipped++;
                else if (result.Passed) passed++;
                else failed++;
            }

            Console.WriteLine();
            Console.WriteLine("Summary: passed=" + passed.ToString() + " failed=" + failed.ToString() + " skipped=" + skipped.ToString());
            return failed > 0 ? 1 : 0;
        }

        private static async Task<BrowserTestResult> TestAltSvcHeaderAsync(BrowserInteropHarness harness, BrowserProtocolSession session, CancellationToken token)
        {
            BrowserTestResult result = new BrowserTestResult();
            result.Name = "browser Alt-Svc Header";

            try
            {
                string url = harness.BaseUrl + "/benchmark/browser?step=altsvc-header";
                BrowserNavigationObservation observation = await session.NavigateAsync(url, token).ConfigureAwait(false);
                bool ok = observation.StatusCode == 200
                    && !String.IsNullOrEmpty(observation.AltSvcHeader)
                    && observation.AltSvcHeader.IndexOf("h3=", StringComparison.OrdinalIgnoreCase) >= 0
                    && observation.BodyText.IndexOf("browser-interop-ok", StringComparison.OrdinalIgnoreCase) >= 0;

                result.Passed = ok;
                result.Detail = ok
                    ? "Alt-Svc header present on browser navigation"
                    : ("status=" + observation.StatusCode.ToString() + " protocol=" + observation.Protocol + " alt-svc=" + observation.AltSvcHeader + " body=" + observation.BodyText);
            }
            catch (Exception e)
            {
                result.Passed = false;
                result.Detail = e.GetType().Name + ": " + e.Message;
            }

            return result;
        }

        private static async Task<BrowserTestResult> TestAltSvcHttp3DiscoveryAsync(BrowserInteropHarness harness, BrowserProtocolSession session, CancellationToken token)
        {
            BrowserTestResult result = new BrowserTestResult();
            result.Name = "browser Alt-Svc HTTP/3 Discovery";

            try
            {
                BrowserNavigationObservation finalObservation = null;

                for (int i = 0; i < 5; i++)
                {
                    finalObservation = await session.NavigateAsync(harness.BaseUrl + "/benchmark/browser?step=retry" + i.ToString(), token).ConfigureAwait(false);

                    if (finalObservation.Protocol.StartsWith("h3", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    await Task.Delay(500, token).ConfigureAwait(false);
                }

                bool ok = finalObservation.StatusCode == 200
                    && finalObservation.Protocol.StartsWith("h3", StringComparison.OrdinalIgnoreCase)
                    && finalObservation.BodyText.IndexOf("browser-interop-ok", StringComparison.OrdinalIgnoreCase) >= 0;

                result.Passed = ok;
                result.Detail = ok
                    ? ("final=" + finalObservation.Protocol)
                    : ("final=" + finalObservation.Protocol + " status=" + finalObservation.StatusCode.ToString());
            }
            catch (Exception e)
            {
                result.Passed = false;
                result.Detail = e.GetType().Name + ": " + e.Message;
            }

            return result;
        }

        private static async Task<BrowserTestResult> TestForcedHttp3Async(BrowserInteropHarness harness, BrowserProtocolSession session, CancellationToken token)
        {
            BrowserTestResult result = new BrowserTestResult();
            result.Name = "browser Forced HTTP/3";

            try
            {
                BrowserNavigationObservation observation = await session.NavigateAsync(harness.BaseUrl + "/benchmark/browser?step=forced-h3", token).ConfigureAwait(false);
                bool ok = observation.StatusCode == 200
                    && observation.Protocol.StartsWith("h3", StringComparison.OrdinalIgnoreCase)
                    && observation.BodyText.IndexOf("browser-interop-ok", StringComparison.OrdinalIgnoreCase) >= 0;

                result.Passed = ok;
                result.Detail = ok
                    ? ("forced=" + observation.Protocol)
                    : ("forced=" + observation.Protocol + " status=" + observation.StatusCode.ToString());
            }
            catch (Exception e)
            {
                result.Passed = false;
                result.Detail = e.GetType().Name + ": " + e.Message;
            }

            return result;
        }
    }
}
