namespace Test.CurlInterop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// curl interoperability entry point.
    /// </summary>
    internal static class Program
    {
        private const string CurlWindowsDownloadUrl = "https://curl.se/windows/";

        private static async Task<int> Main(string[] args)
        {
            List<InteropTestResult> results = new List<InteropTestResult>();
            string curlExecutable = GetCurlExecutable(args);

            using (CurlInteropHarness harness = new CurlInteropHarness(curlExecutable))
            {
                CurlCapabilities capabilities = harness.DetectCapabilities();
                Console.WriteLine("curl available: " + capabilities.IsAvailable.ToString());
                Console.WriteLine(capabilities.VersionOutput.Trim());
                Console.WriteLine();

                if (!capabilities.IsAvailable)
                {
                    Console.WriteLine("curl.exe was not available.");
                    Console.WriteLine("Install curl with HTTP/2 and HTTP/3 support if you want full interoperability coverage.");
                    Console.WriteLine("Windows download: " + CurlWindowsDownloadUrl);
                    return 1;
                }

                if (!capabilities.SupportsHttp2 || !capabilities.SupportsHttp3)
                {
                    Console.WriteLine("This interoperability harness requires curl builds with HTTP/2 and HTTP/3 support for full coverage.");
                    Console.WriteLine("Current support: HTTP/2=" + capabilities.SupportsHttp2.ToString() + " HTTP/3=" + capabilities.SupportsHttp3.ToString());
                    Console.WriteLine("Recommended Windows download: " + CurlWindowsDownloadUrl);
                    Console.WriteLine();
                }

                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                {
                    await harness.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);

                    try
                    {
                        results.Add(TestHttp11Get(harness));
                        results.Add(TestHttp11PostEcho(harness));
                        results.Add(TestAltSvcHeader(harness));
                        results.Add(TestAltSvcCacheFile(harness, capabilities));
                        results.Add(TestHttp2Get(harness, capabilities));
                        results.Add(TestHttp2PostEcho(harness, capabilities));
                        results.Add(TestHttp2ServerSentEvents(harness, capabilities));
                        results.Add(TestHttp3ExplicitGet(harness, capabilities));
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
                InteropTestResult result = results[i];
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

        private static string GetCurlExecutable(string[] args)
        {
            if (args == null || args.Length < 1) return null;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--curl", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static InteropTestResult TestHttp11Get(CurlInteropHarness harness)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/1.1 GET";

            CurlCommandResult command = harness.InvokeCurl("-k --http1.1 -sS \"" + harness.BaseUrl + "/benchmark/hello\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.HelloPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:1.1", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "body/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static InteropTestResult TestHttp11PostEcho(CurlInteropHarness harness)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/1.1 POST Echo";

            CurlCommandResult command = harness.InvokeCurl("-k --http1.1 -sS -X POST --data \"" + harness.EchoPayload + "\" \"" + harness.BaseUrl + "/benchmark/echo\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.EchoPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:1.1", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "echo/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static InteropTestResult TestAltSvcHeader(CurlInteropHarness harness)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl Alt-Svc Header";

            string tempOutput = Path.GetTempFileName();
            try
            {
                CurlCommandResult command = harness.InvokeCurl("-k --http1.1 -sS -D - -o \"" + tempOutput + "\" \"" + harness.BaseUrl + "/benchmark/hello\"");
                bool ok = command.ExitCode == 0
                    && command.StandardOutput.IndexOf("alt-svc:", StringComparison.OrdinalIgnoreCase) >= 0
                    && command.StandardOutput.IndexOf("h3=", StringComparison.OrdinalIgnoreCase) >= 0;

                result.Passed = ok;
                result.Detail = ok ? "Alt-Svc header present" : (command.StandardOutput + command.StandardError).Trim();
                return result;
            }
            finally
            {
                DeleteIfExists(tempOutput);
            }
        }

        private static InteropTestResult TestAltSvcCacheFile(CurlInteropHarness harness, CurlCapabilities capabilities)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl Alt-Svc Cache";

            if (!capabilities.SupportsAltSvc)
            {
                result.Skipped = true;
                result.Passed = true;
                result.Detail = "curl binary does not report alt-svc support";
                return result;
            }

            string cacheFile = Path.GetTempFileName();
            string outputFile = Path.GetTempFileName();

            try
            {
                CurlCommandResult command = harness.InvokeCurl("-k --http1.1 --alt-svc \"" + cacheFile + "\" -sS -o \"" + outputFile + "\" \"" + harness.BaseUrl + "/benchmark/hello\"");
                string cacheContents = File.Exists(cacheFile) ? File.ReadAllText(cacheFile) : string.Empty;
                bool ok = command.ExitCode == 0
                    && cacheContents.IndexOf("h3", StringComparison.OrdinalIgnoreCase) >= 0;

                result.Passed = ok;
                result.Detail = ok ? "Alt-Svc cache file recorded h3 advertisement" : (command.StandardOutput + command.StandardError + Environment.NewLine + cacheContents).Trim();
                return result;
            }
            finally
            {
                DeleteIfExists(cacheFile);
                DeleteIfExists(outputFile);
            }
        }

        private static InteropTestResult TestHttp2Get(CurlInteropHarness harness, CurlCapabilities capabilities)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/2 GET";

            if (!capabilities.SupportsHttp2)
            {
                result.Skipped = true;
                result.Passed = true;
                result.Detail = "installed curl binary does not report HTTP/2 support";
                return result;
            }

            CurlCommandResult command = harness.InvokeCurl("-k --http2 -sS \"" + harness.BaseUrl + "/benchmark/hello\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.HelloPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:2", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "body/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static InteropTestResult TestHttp2PostEcho(CurlInteropHarness harness, CurlCapabilities capabilities)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/2 POST Echo";

            if (!capabilities.SupportsHttp2)
            {
                result.Skipped = true;
                result.Passed = true;
                result.Detail = "installed curl binary does not report HTTP/2 support";
                return result;
            }

            CurlCommandResult command = harness.InvokeCurl("-k --http2 -sS -X POST --data \"" + harness.EchoPayload + "\" \"" + harness.BaseUrl + "/benchmark/echo\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.EchoPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:2", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "echo/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static InteropTestResult TestHttp2ServerSentEvents(CurlInteropHarness harness, CurlCapabilities capabilities)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/2 SSE";

            if (!capabilities.SupportsHttp2)
            {
                result.Skipped = true;
                result.Passed = true;
                result.Detail = "installed curl binary does not report HTTP/2 support";
                return result;
            }

            CurlCommandResult command = harness.InvokeCurl("-k --http2 -N -sS \"" + harness.BaseUrl + "/benchmark/sse\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.ServerSentEventPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:2", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "event payload/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static InteropTestResult TestHttp3ExplicitGet(CurlInteropHarness harness, CurlCapabilities capabilities)
        {
            InteropTestResult result = new InteropTestResult();
            result.Name = "curl HTTP/3 GET";

            if (!capabilities.SupportsHttp3)
            {
                result.Skipped = true;
                result.Passed = true;
                result.Detail = "installed curl binary does not report HTTP/3 support";
                return result;
            }

            CurlCommandResult command = harness.InvokeCurl("-k --http3 -sS \"" + harness.BaseUrl + "/benchmark/hello\" -w \"\\nSTATUS:%{http_code}\\nVERSION:%{http_version}\\n\"");
            bool ok = command.ExitCode == 0
                && command.StandardOutput.Contains(harness.HelloPayload)
                && command.StandardOutput.Contains("STATUS:200")
                && command.StandardOutput.IndexOf("VERSION:3", StringComparison.OrdinalIgnoreCase) >= 0;

            result.Passed = ok;
            result.Detail = ok ? "body/status/version matched" : (command.StandardOutput + command.StandardError).Trim();
            return result;
        }

        private static void DeleteIfExists(string filename)
        {
            if (String.IsNullOrEmpty(filename)) return;
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
        }
    }
}
