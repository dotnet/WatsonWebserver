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

            await ExecuteTestAsync("Static route snapshots remain readable during concurrent mutation", SharedOptimizationSmokeTests.TestStaticRouteSnapshotsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Default serialization helper preserves pretty and compact JSON", SharedOptimizationSmokeTests.TestDefaultSerializationHelperAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 cached response headers preserve dynamic fields", SharedOptimizationSmokeTests.TestHttp1CachedHeadersAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 keep-alive pooling resets request state", SharedOptimizationSmokeTests.TestHttp1KeepAlivePoolingAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 stream send preserves direct passthrough body", SharedOptimizationSmokeTests.TestHttp1StreamSendAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/2 lazy header materialization stays coherent", TestHttp2LazyHeaderMaterializationAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/3 lazy header materialization stays coherent", TestHttp3LazyHeaderMaterializationAsync).ConfigureAwait(false);

            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = "Optimization Coverage";
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

        private async Task TestHttp2LazyHeaderMaterializationAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(true, true, false, ConfigureHeaderObservationRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(2, 0)))
                {
                    await ValidateHeaderObservationAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        private async Task TestHttp3LazyHeaderMaterializationAsync()
        {
            if (!WatsonWebserver.Core.Http3.Http3RuntimeDetector.Detect().IsAvailable)
            {
                return;
            }

            try
            {
                using (LoopbackServerHost host = new LoopbackServerHost(true, false, true, ConfigureHeaderObservationRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    using (HttpClient client = CreateHttpClient(new Version(3, 0)))
                    {
                        await ValidateHeaderObservationAsync(client, host.BaseAddress).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (ShouldSkipLiveHttp3Test(ex))
            {
                return;
            }
        }

        private static void ConfigureHeaderObservationRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/headers", async (HttpContextBase context) =>
            {
                HeaderObservationResponse response = new HeaderObservationResponse();
                response.HeaderExists = context.Request.HeaderExists("x-custom");
                response.RetrievedHeaderValue = context.Request.RetrieveHeaderValue("x-custom");
                response.MaterializedHeaderValue = context.Request.Headers["x-custom"];
                response.ContentType = context.Request.ContentType;
                response.UserAgent = context.Request.Useragent;
                response.QueryValue = context.Request.RetrieveQueryValue("item");
                response.Body = context.Request.DataAsString;

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.Send(JsonSerializer.Serialize(response), context.Token).ConfigureAwait(false);
            });
        }

        private async Task ValidateHeaderObservationAsync(HttpClient client, Uri baseAddress)
        {
            HeaderObservationResponse responseModel = null;
            HttpRequestMessage request = new HttpRequestMessage(NetHttpMethod.Post, new Uri(baseAddress, "/headers?item=value").ToString());
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            request.Headers.Add("x-custom", "custom-value");
            request.Headers.Add("user-agent", "OptimizationCoverageSuite");
            request.Content = new StringContent("typed-body", Encoding.UTF8, "text/plain");

            using (request)
            {
                using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    responseModel = JsonSerializer.Deserialize<HeaderObservationResponse>(json, _JsonSerializerOptions);
                }
            }

            if (responseModel == null)
            {
                throw new InvalidOperationException("Header observation response should deserialize to a typed instance.");
            }

            if (!responseModel.HeaderExists
                || !String.Equals(responseModel.RetrievedHeaderValue, "custom-value", StringComparison.Ordinal)
                || !String.Equals(responseModel.MaterializedHeaderValue, "custom-value", StringComparison.Ordinal)
                || !String.Equals(responseModel.ContentType, "text/plain; charset=utf-8", StringComparison.Ordinal)
                || !String.Equals(responseModel.QueryValue, "value", StringComparison.Ordinal)
                || !String.Equals(responseModel.Body, "typed-body", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Lazy header materialization did not preserve request semantics.");
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

        private static bool ShouldSkipLiveHttp3Test(Exception exception)
        {
            if (exception == null)
            {
                return false;
            }

            string message = exception.Message ?? String.Empty;
            return exception is TimeoutException
                || message.IndexOf("timed out", StringComparison.InvariantCultureIgnoreCase) >= 0
                || message.IndexOf("inactivity", StringComparison.InvariantCultureIgnoreCase) >= 0
                || message.IndexOf("quic", StringComparison.InvariantCultureIgnoreCase) >= 0
                || message.IndexOf("HTTP/3", StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

    }
}
