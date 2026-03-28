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
            await ExecuteTestAsync("HTTP/1.1 :: Basic GET Request", SharedLegacySmokeTests.TestHttp11BasicGetAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Basic POST Request", SharedLegacySmokeTests.TestHttp11BasicPostAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Body Echo Request", SharedLegacySmokeTests.TestHttp11BodyEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Basic PUT Request", SharedLegacySmokeTests.TestHttp11BasicPutAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Basic DELETE Request", SharedLegacySmokeTests.TestHttp11BasicDeleteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Parameter Route Request", SharedLegacySmokeTests.TestHttp11ParameterRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Query-String Route Request", SharedLegacySmokeTests.TestHttp11QueryStringRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Static Content Route", SharedLegacySmokeTests.TestHttp11StaticContentRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Header Echo Request", SharedLegacySmokeTests.TestHttp11HeaderEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Transfer Response", SharedLegacySmokeTests.TestHttp11ChunkedTransferEncodingAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Edge-Case Response", SharedLegacySmokeTests.TestHttp11ChunkedEdgeCasesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Request Body Via DataAsBytes", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Request Body Via DataAsString", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Request Body Via ReadBodyAsync", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyReadBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Chunked Request Body Via Manual Chunk Read", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyManualReadChunkAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Large Chunked Request Body", SharedLegacySmokeTests.TestHttp11LargeChunkedRequestBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Preservation Hello", SharedLegacySmokeTests.TestHttp11DataPreservationHelloAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Preservation Hello CRLF", SharedLegacySmokeTests.TestHttp11DataPreservationHelloCrLfAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Server-Sent Events", SharedLegacySmokeTests.TestHttp11ServerSentEventsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Server-Sent Events Edge Cases", SharedLegacySmokeTests.TestHttp11ServerSentEventsEdgeCasesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Double-Send Response Handling", SharedLegacySmokeTests.TestHttp11DoubleSendResponseAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Exception In Route Handler Returns 500", SharedLegacySmokeTests.TestHttp11ExceptionInRouteHandlerAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Empty POST Body", SharedLegacySmokeTests.TestHttp11EmptyPostBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: OPTIONS Preflight", SharedLegacySmokeTests.TestHttp11OptionsPreflightAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Request With Many Headers", SharedLegacySmokeTests.TestHttp11RequestWithManyHeadersAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Unmatched Route Returns 404", SharedLegacySmokeTests.TestHttp11NotFoundRouteAsync).ConfigureAwait(false);
            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = String.Empty;
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
