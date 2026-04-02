namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Shared Data stream coverage executed in the console smoke runner.
    /// </summary>
    public class SharedDataStreamSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared Data stream coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();
            await ExecuteTestAsync("HTTP/1.1 :: Data Stream Read Returns EOF", SharedDataStreamTests.TestDataStreamReadReturnsEofAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Stream ReadAsync Returns EOF", SharedDataStreamTests.TestDataStreamReadAsyncReturnsEofAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Stream Large Body", SharedDataStreamTests.TestDataStreamLargeBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: DataAsBytes Still Works", SharedDataStreamTests.TestDataAsBytesStillWorksAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Stream Empty Body", SharedDataStreamTests.TestDataStreamEmptyBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Data Stream Keep-Alive Multiple Requests", SharedDataStreamTests.TestDataStreamKeepAliveMultipleRequestsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: ReadBodyAsync Through ContentLengthStream", SharedDataStreamTests.TestReadBodyAsyncThroughContentLengthStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: DataAsString Through ContentLengthStream", SharedDataStreamTests.TestDataAsStringThroughContentLengthStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: WebSocket Upgrade With ContentLengthStream", SharedDataStreamTests.TestWebSocketUpgradeWithContentLengthStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: HTTP Body Then WebSocket On Same Server", SharedDataStreamTests.TestHttpBodyThenWebSocketOnSameServerAsync).ConfigureAwait(false);
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
