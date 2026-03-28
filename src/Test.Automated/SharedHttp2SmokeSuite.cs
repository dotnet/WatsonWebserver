namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Shared HTTP/2 smoke coverage executed in the console smoke runner.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class SharedHttp2SmokeSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared HTTP/2 smoke coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();
            await ExecuteTestAsync("HTTP/2 h2c basic GET request", SharedHttp2SmokeTests.TestHttp2BasicGetAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 h2c continuation header block request", SharedHttp2SmokeTests.TestHttp2ContinuationHeaderBlockAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 h2c padded priority headers and data request", SharedHttp2SmokeTests.TestHttp2PaddedPriorityHeadersAndDataAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 h2c response trailers", SharedHttp2SmokeTests.TestHttp2ResponseTrailersAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 h2c chunked API response", SharedHttp2SmokeTests.TestHttp2ChunkedApiResponseAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 h2c SSE API response", SharedHttp2SmokeTests.TestHttp2ServerSentEventsResponseAsync).ConfigureAwait(false);
            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = "Shared HTTP/2 Smoke Coverage";
            result.TestName = testName;

            try
            {
                await test().ConfigureAwait(false);
                result.Passed = true;
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                _Results.Add(result);
                AutomatedTestReporter.ResultRecorded?.Invoke(result);
            }
        }
    }
}
