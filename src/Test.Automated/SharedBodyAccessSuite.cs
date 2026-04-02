namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Comprehensive body access coverage across all protocols.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class SharedBodyAccessSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared body access coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();

            // HTTP/1.1 body access methods
            await ExecuteTestAsync("HTTP/1.1 Body :: POST via Data.Read", SharedBodyAccessTests.TestHttp1PostDataStreamReadAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: POST via Data.ReadAsync", SharedBodyAccessTests.TestHttp1PostDataStreamReadAsyncAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: POST via DataAsBytes", SharedBodyAccessTests.TestHttp1PostDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: POST via DataAsString", SharedBodyAccessTests.TestHttp1PostDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: POST via ReadBodyAsync", SharedBodyAccessTests.TestHttp1PostReadBodyAsyncAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PUT via Data.Read", SharedBodyAccessTests.TestHttp1PutDataStreamReadAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PUT via DataAsBytes", SharedBodyAccessTests.TestHttp1PutDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PUT via DataAsString", SharedBodyAccessTests.TestHttp1PutDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PUT via ReadBodyAsync", SharedBodyAccessTests.TestHttp1PutReadBodyAsyncAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PATCH via DataAsBytes", SharedBodyAccessTests.TestHttp1PatchDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: PATCH via Data.Read", SharedBodyAccessTests.TestHttp1PatchDataStreamReadAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: DELETE with body via DataAsBytes", SharedBodyAccessTests.TestHttp1DeleteWithBodyDataAsBytesAsync).ConfigureAwait(false);

            // HTTP/1.1 body sizes
            await ExecuteTestAsync("HTTP/1.1 Body :: Empty body via Data.Read", SharedBodyAccessTests.TestHttp1EmptyBodyStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: Single byte via Data.Read", SharedBodyAccessTests.TestHttp1SingleByteBodyStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: 128KB via Data.ReadAsync", SharedBodyAccessTests.TestHttp1LargeBodyStreamAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: 128KB via DataAsBytes", SharedBodyAccessTests.TestHttp1LargeBodyDataAsBytesAsync).ConfigureAwait(false);

            // HTTP/1.1 keep-alive
            await ExecuteTestAsync("HTTP/1.1 Body :: Keep-alive 10x stream reads", SharedBodyAccessTests.TestHttp1KeepAliveStreamReadsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 Body :: Keep-alive alternating access methods", SharedBodyAccessTests.TestHttp1KeepAliveAlternatingAccessAsync).ConfigureAwait(false);

            // HTTP/2 body access methods
            await ExecuteTestAsync("HTTP/2 Body :: POST via DataAsBytes", SharedBodyAccessTests.TestHttp2PostDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: POST via DataAsString", SharedBodyAccessTests.TestHttp2PostDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: POST via ReadBodyAsync", SharedBodyAccessTests.TestHttp2PostReadBodyAsyncAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: POST via Data.Read", SharedBodyAccessTests.TestHttp2PostDataStreamReadAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: POST via Data.ReadAsync", SharedBodyAccessTests.TestHttp2PostDataStreamReadAsyncAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: PUT via DataAsBytes", SharedBodyAccessTests.TestHttp2PutDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: PUT via DataAsString", SharedBodyAccessTests.TestHttp2PutDataAsStringAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: PATCH via DataAsBytes", SharedBodyAccessTests.TestHttp2PatchDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: DELETE with body via DataAsBytes", SharedBodyAccessTests.TestHttp2DeleteWithBodyDataAsBytesAsync).ConfigureAwait(false);

            // HTTP/2 body sizes
            await ExecuteTestAsync("HTTP/2 Body :: Empty body", SharedBodyAccessTests.TestHttp2EmptyBodyAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: 32KB via DataAsBytes", SharedBodyAccessTests.TestHttp2LargeBodyDataAsBytesAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: 32KB via Data.ReadAsync", SharedBodyAccessTests.TestHttp2LargeBodyStreamReadAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 Body :: 48KB multi-frame via DataAsBytes", SharedBodyAccessTests.TestHttp2MultiFrameBodyAsync).ConfigureAwait(false);

            // WebSocket body access
            await ExecuteTestAsync("WebSocket Body :: Text echo", SharedBodyAccessTests.TestWebSocketTextEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Binary echo", SharedBodyAccessTests.TestWebSocketBinaryEchoAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Medium text (2KB)", SharedBodyAccessTests.TestWebSocketMediumTextAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Medium binary (3KB)", SharedBodyAccessTests.TestWebSocketMediumBinaryAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Fragmented text assembly", SharedBodyAccessTests.TestWebSocketFragmentedTextAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Fragmented binary assembly", SharedBodyAccessTests.TestWebSocketFragmentedBinaryAsync).ConfigureAwait(false);
            await ExecuteTestAsync("WebSocket Body :: Interleaved text and binary", SharedBodyAccessTests.TestWebSocketInterleavedTextAndBinaryAsync).ConfigureAwait(false);

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
