namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Test.Shared;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Routing;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;
    using NetHttpMethod = System.Net.Http.HttpMethod;

    /// <summary>
    /// Additional automated coverage for kept optimization work.
    /// </summary>
    public class OptimizationCoverageSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();
        private readonly JsonSerializerOptions _JsonSerializerOptions = new JsonSerializerOptions();

        /// <summary>
        /// Execute optimization-focused automated tests.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();

            await ExecuteTestAsync("Routing :: Static Route Snapshots Remain Readable During Concurrent Mutation", SharedOptimizationSmokeTests.TestStaticRouteSnapshotsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Serialization :: Default Helper Preserves Pretty And Compact JSON", SharedOptimizationSmokeTests.TestDefaultSerializationHelperAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Cached Response Headers Preserve Dynamic Fields", SharedOptimizationSmokeTests.TestHttp1CachedHeadersAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Context Timing Starts At Request Entry", SharedOptimizationSmokeTests.TestContextTimestampStartsAtRequestEntryAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Keep-Alive Pooling Resets Request State", SharedOptimizationSmokeTests.TestHttp1KeepAlivePoolingAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 :: Stream Send Preserves Direct Passthrough Body", SharedOptimizationSmokeTests.TestHttp1StreamSendAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 :: Lazy Header Materialization Stays Coherent", SharedOptimizationSmokeTests.TestHttp2LazyHeaderMaterializationAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/3 :: Lazy Header Materialization Stays Coherent", SharedOptimizationSmokeTests.TestHttp3LazyHeaderMaterializationAsync).ConfigureAwait(false);

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

        private HttpClient CreateHttpClient(Version version)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }

    }
}
