namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Shared legacy smoke coverage executed in the console smoke runner.
    /// </summary>
    public class SharedLegacySmokeSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared legacy smoke coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();
            await ExecuteTestAsync("HTTP/1.1 basic GET request", SharedLegacySmokeTests.TestHttp11BasicGetAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 basic POST request", SharedLegacySmokeTests.TestHttp11BasicPostAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 body echo request", SharedLegacySmokeTests.TestHttp11BodyEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 basic PUT request", SharedLegacySmokeTests.TestHttp11BasicPutAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 basic DELETE request", SharedLegacySmokeTests.TestHttp11BasicDeleteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 parameter route request", SharedLegacySmokeTests.TestHttp11ParameterRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 query-string route request", SharedLegacySmokeTests.TestHttp11QueryStringRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 static content route", SharedLegacySmokeTests.TestHttp11StaticContentRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 header echo request", SharedLegacySmokeTests.TestHttp11HeaderEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked transfer response", SharedLegacySmokeTests.TestHttp11ChunkedTransferEncodingAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked edge-case response", SharedLegacySmokeTests.TestHttp11ChunkedEdgeCasesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked request body via DataAsBytes", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked request body via DataAsString", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked request body via ReadBodyAsync", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyReadBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 chunked request body via manual chunk read", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyManualReadChunkAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 large chunked request body", SharedLegacySmokeTests.TestHttp11LargeChunkedRequestBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 data preservation hello", SharedLegacySmokeTests.TestHttp11DataPreservationHelloAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 data preservation hello CRLF", SharedLegacySmokeTests.TestHttp11DataPreservationHelloCrLfAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 server-sent events", SharedLegacySmokeTests.TestHttp11ServerSentEventsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 server-sent events edge cases", SharedLegacySmokeTests.TestHttp11ServerSentEventsEdgeCasesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 unmatched route returns 404", SharedLegacySmokeTests.TestHttp11NotFoundRouteAsync).ConfigureAwait(false);
            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = "Shared Legacy Smoke Coverage";
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
