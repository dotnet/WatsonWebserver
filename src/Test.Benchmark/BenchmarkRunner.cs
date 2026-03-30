namespace Test.Benchmark
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Runs benchmark combinations.
    /// </summary>
    internal class BenchmarkRunner
    {
        private readonly BenchmarkOptions _Options;
        private readonly byte[] _RequestPayload;
        private readonly string _HelloPayload;
        private readonly byte[] _HelloPayloadBytes;
        private readonly string _JsonResponsePayload;
        private readonly string _SerializedJsonResponsePayload;
        private readonly byte[] _JsonRequestPayload;
        private readonly bool _DebugFailures;

        /// <summary>
        /// Instantiate the runner.
        /// </summary>
        /// <param name="options">Benchmark options.</param>
        public BenchmarkRunner(BenchmarkOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _Options = options;
            _RequestPayload = Encoding.UTF8.GetBytes(new string('p', _Options.PayloadBytes));
            _HelloPayload = new string('x', _Options.PayloadBytes);
            _HelloPayloadBytes = Encoding.UTF8.GetBytes(_HelloPayload);
            _JsonResponsePayload = BuildJsonPayload(_Options.PayloadBytes);
            _SerializedJsonResponsePayload = _JsonResponsePayload;
            _JsonRequestPayload = Encoding.UTF8.GetBytes(BuildJsonPayload(_Options.PayloadBytes));
            _DebugFailures = String.Equals(Environment.GetEnvironmentVariable("WATSON_BENCHMARK_DEBUG_FAILURES"), "1", StringComparison.Ordinal);
        }

        /// <summary>
        /// Run a benchmark combination.
        /// </summary>
        /// <param name="combination">Combination.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Benchmark result.</returns>
        public async Task<BenchmarkResult> RunAsync(BenchmarkCombination combination, CancellationToken token)
        {
            if (combination == null) throw new ArgumentNullException(nameof(combination));

            using (IBenchmarkHost host = BenchmarkHostFactory.Create(combination.Target, combination.Protocol, _Options))
            {
                await host.StartAsync(token).ConfigureAwait(false);
                try
                {
                    return await RunAsync(host, combination, token).ConfigureAwait(false);
                }
                finally
                {
                    await host.StopAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Run a benchmark combination against an already-started host.
        /// </summary>
        /// <param name="host">Started benchmark host.</param>
        /// <param name="combination">Combination.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Benchmark result.</returns>
        public async Task<BenchmarkResult> RunAsync(IBenchmarkHost host, BenchmarkCombination combination, CancellationToken token)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (combination == null) throw new ArgumentNullException(nameof(combination));

            if (combination.Scenario == BenchmarkScenario.WebSocketEcho)
            {
                return await RunWebSocketAsync(host, combination, token).ConfigureAwait(false);
            }

            if (combination.Protocol == BenchmarkProtocol.Http3 && combination.Target == BenchmarkTarget.Watson7)
            {
                if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    return await RunHttp3Async(host, combination, token).ConfigureAwait(false);
                }

                throw new PlatformNotSupportedException("HTTP/3 benchmarks require QUIC support on Windows, Linux, or macOS.");
            }

            using (HttpClient client = CreateHttpClient())
            {
                client.BaseAddress = host.BaseAddress;
                client.Timeout = TimeSpan.FromSeconds(_Options.RequestTimeoutSeconds);

                double handshakeMs = await MeasureHandshakeAsync(client, combination, token).ConfigureAwait(false);

                if (_Options.WarmupSeconds > 0)
                {
                    await ExecutePhaseAsync(client, combination, _Options.WarmupSeconds, false, token).ConfigureAwait(false);
                }

                long allocatedBefore = GC.GetTotalAllocatedBytes(true);
                BenchmarkPhaseResult phase = await ExecutePhaseAsync(client, combination, _Options.DurationSeconds, true, token).ConfigureAwait(false);
                long allocatedAfter = GC.GetTotalAllocatedBytes(true);

                phase.LatenciesMs.Sort();

                BenchmarkResult result = new BenchmarkResult();
                result.Combination = combination;
                result.HandshakeMs = handshakeMs;
                result.DurationMs = phase.DurationMs;
                result.SuccessCount = phase.SuccessCount;
                result.FailureCount = phase.FailureCount;
                result.RequestBytes = phase.RequestBytes;
                result.ResponseBytes = phase.ResponseBytes;
                result.RequestsPerSecond = phase.DurationMs > 0 ? (phase.SuccessCount / (phase.DurationMs / 1000.0)) : 0;
                result.ResponseBytesPerSecond = phase.DurationMs > 0 ? (phase.ResponseBytes / (phase.DurationMs / 1000.0)) : 0;
                result.TotalBytesPerSecond = phase.DurationMs > 0 ? ((phase.RequestBytes + phase.ResponseBytes) / (phase.DurationMs / 1000.0)) : 0;
                result.ManagedBytesAllocated = allocatedAfter - allocatedBefore;
                result.MeanLatencyMs = GetMean(phase.LatenciesMs);
                result.P50LatencyMs = GetPercentile(phase.LatenciesMs, 0.50);
                result.P95LatencyMs = GetPercentile(phase.LatenciesMs, 0.95);
                result.P99LatencyMs = GetPercentile(phase.LatenciesMs, 0.99);
                return result;
            }
        }

        private async Task<BenchmarkResult> RunWebSocketAsync(IBenchmarkHost host, BenchmarkCombination combination, CancellationToken token)
        {
            Stopwatch handshakeStopwatch = Stopwatch.StartNew();
            using (ClientWebSocket handshakeClient = CreateWebSocketClient(host.BaseAddress))
            {
                await handshakeClient.ConnectAsync(BuildWebSocketUri(host.BaseAddress, GetWebSocketPath(combination.Scenario)), token).ConfigureAwait(false);
                handshakeStopwatch.Stop();
                await CloseWebSocketQuietlyAsync(handshakeClient, token).ConfigureAwait(false);
            }

            if (_Options.WarmupSeconds > 0)
            {
                await ExecuteWebSocketPhaseAsync(host, combination, _Options.WarmupSeconds, false, token).ConfigureAwait(false);
            }

            long allocatedBefore = GC.GetTotalAllocatedBytes(true);
            BenchmarkPhaseResult phase = await ExecuteWebSocketPhaseAsync(host, combination, _Options.DurationSeconds, true, token).ConfigureAwait(false);
            long allocatedAfter = GC.GetTotalAllocatedBytes(true);

            phase.LatenciesMs.Sort();

            BenchmarkResult result = new BenchmarkResult();
            result.Combination = combination;
            result.HandshakeMs = handshakeStopwatch.Elapsed.TotalMilliseconds;
            result.DurationMs = phase.DurationMs;
            result.SuccessCount = phase.SuccessCount;
            result.FailureCount = phase.FailureCount;
            result.RequestBytes = phase.RequestBytes;
            result.ResponseBytes = phase.ResponseBytes;
            result.RequestsPerSecond = phase.DurationMs > 0 ? (phase.SuccessCount / (phase.DurationMs / 1000.0)) : 0;
            result.ResponseBytesPerSecond = phase.DurationMs > 0 ? (phase.ResponseBytes / (phase.DurationMs / 1000.0)) : 0;
            result.TotalBytesPerSecond = phase.DurationMs > 0 ? ((phase.RequestBytes + phase.ResponseBytes) / (phase.DurationMs / 1000.0)) : 0;
            result.ManagedBytesAllocated = allocatedAfter - allocatedBefore;
            result.MeanLatencyMs = GetMean(phase.LatenciesMs);
            result.P50LatencyMs = GetPercentile(phase.LatenciesMs, 0.50);
            result.P95LatencyMs = GetPercentile(phase.LatenciesMs, 0.95);
            result.P99LatencyMs = GetPercentile(phase.LatenciesMs, 0.99);
            return result;
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task<BenchmarkResult> RunHttp3Async(IBenchmarkHost host, BenchmarkCombination combination, CancellationToken token)
        {
            if (_Options.WarmupSeconds > 0)
            {
                using (Http3BenchmarkClient warmupClient = new Http3BenchmarkClient(host.BaseAddress))
                {
                    await warmupClient.ConnectAsync(token).ConfigureAwait(false);
                    await warmupClient.SendAsync(combination.Scenario, GetRequestBody(combination.Scenario), token).ConfigureAwait(false);
                    await ExecuteHttp3PhaseAsync(warmupClient, combination, _Options.WarmupSeconds, false, token).ConfigureAwait(false);
                }
            }

            using (Http3BenchmarkClient client = new Http3BenchmarkClient(host.BaseAddress))
            {
                Stopwatch handshakeStopwatch = Stopwatch.StartNew();
                await client.ConnectAsync(token).ConfigureAwait(false);
                await client.SendAsync(combination.Scenario, GetRequestBody(combination.Scenario), token).ConfigureAwait(false);
                handshakeStopwatch.Stop();

                long allocatedBefore = GC.GetTotalAllocatedBytes(true);
                BenchmarkPhaseResult phase = await ExecuteHttp3PhaseAsync(client, combination, _Options.DurationSeconds, true, token).ConfigureAwait(false);
                long allocatedAfter = GC.GetTotalAllocatedBytes(true);

                phase.LatenciesMs.Sort();

                BenchmarkResult result = new BenchmarkResult();
                result.Combination = combination;
                result.HandshakeMs = handshakeStopwatch.Elapsed.TotalMilliseconds;
                result.DurationMs = phase.DurationMs;
                result.SuccessCount = phase.SuccessCount;
                result.FailureCount = phase.FailureCount;
                result.RequestBytes = phase.RequestBytes;
                result.ResponseBytes = phase.ResponseBytes;
                result.RequestsPerSecond = phase.DurationMs > 0 ? (phase.SuccessCount / (phase.DurationMs / 1000.0)) : 0;
                result.ResponseBytesPerSecond = phase.DurationMs > 0 ? (phase.ResponseBytes / (phase.DurationMs / 1000.0)) : 0;
                result.TotalBytesPerSecond = phase.DurationMs > 0 ? ((phase.RequestBytes + phase.ResponseBytes) / (phase.DurationMs / 1000.0)) : 0;
                result.ManagedBytesAllocated = allocatedAfter - allocatedBefore;
                result.MeanLatencyMs = GetMean(phase.LatenciesMs);
                result.P50LatencyMs = GetPercentile(phase.LatenciesMs, 0.50);
                result.P95LatencyMs = GetPercentile(phase.LatenciesMs, 0.95);
                result.P99LatencyMs = GetPercentile(phase.LatenciesMs, 0.99);
                return result;
            }
        }

        private HttpClient CreateHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            handler.AllowAutoRedirect = false;
            handler.AutomaticDecompression = DecompressionMethods.None;
            handler.MaxConnectionsPerServer = Math.Max(64, _Options.Concurrency * 4);
            return new HttpClient(handler, true);
        }

        private async Task<double> MeasureHandshakeAsync(HttpClient client, BenchmarkCombination combination, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await ExecuteRequestAsync(client, combination, false, token).ConfigureAwait(false);
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

        private async Task<BenchmarkPhaseResult> ExecutePhaseAsync(HttpClient client, BenchmarkCombination combination, int durationSeconds, bool captureLatencies, CancellationToken token)
        {
            BenchmarkPhaseResult result = new BenchmarkPhaseResult();
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (CancellationTokenSource durationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                durationTokenSource.CancelAfter(TimeSpan.FromSeconds(durationSeconds));
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < _Options.Concurrency; i++)
                {
                    tasks.Add(RunWorkerAsync(client, combination, result, captureLatencies, durationTokenSource.Token));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            stopwatch.Stop();
            result.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private async Task<BenchmarkPhaseResult> ExecuteWebSocketPhaseAsync(IBenchmarkHost host, BenchmarkCombination combination, int durationSeconds, bool captureLatencies, CancellationToken token)
        {
            BenchmarkPhaseResult result = new BenchmarkPhaseResult();
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (CancellationTokenSource durationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                durationTokenSource.CancelAfter(TimeSpan.FromSeconds(durationSeconds));
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < _Options.Concurrency; i++)
                {
                    tasks.Add(RunWebSocketWorkerAsync(host, combination, result, captureLatencies, durationTokenSource.Token));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            stopwatch.Stop();
            result.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task<BenchmarkPhaseResult> ExecuteHttp3PhaseAsync(Http3BenchmarkClient client, BenchmarkCombination combination, int durationSeconds, bool captureLatencies, CancellationToken token)
        {
            BenchmarkPhaseResult result = new BenchmarkPhaseResult();
            Stopwatch stopwatch = Stopwatch.StartNew();

            using (CancellationTokenSource durationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                durationTokenSource.CancelAfter(TimeSpan.FromSeconds(durationSeconds));
                List<Task> tasks = new List<Task>();

                for (int i = 0; i < _Options.Concurrency; i++)
                {
                    tasks.Add(RunHttp3WorkerAsync(client, combination, result, captureLatencies, durationTokenSource.Token));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            stopwatch.Stop();
            result.DurationMs = stopwatch.Elapsed.TotalMilliseconds;
            return result;
        }

        private async Task RunWorkerAsync(HttpClient client, BenchmarkCombination combination, BenchmarkPhaseResult result, bool captureLatencies, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                long startTimestamp = Stopwatch.GetTimestamp();

                try
                {
                    RequestExecutionResult executionResult = await ExecuteRequestAsync(client, combination, captureLatencies, token).ConfigureAwait(false);
                    double latencyMs = GetElapsedMilliseconds(startTimestamp, Stopwatch.GetTimestamp());

                    lock (result.SyncRoot)
                    {
                        result.SuccessCount++;
                        result.RequestBytes += executionResult.RequestBytes;
                        result.ResponseBytes += executionResult.ResponseBytes;
                        if (captureLatencies)
                        {
                            result.LatenciesMs.Add(latencyMs);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
                catch (Exception exception)
                {
                    if (IsCancellationRelatedFailure(exception, token))
                    {
                        break;
                    }

                    DebugFailure(combination, exception);
                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
            }
        }

        private async Task RunWebSocketWorkerAsync(IBenchmarkHost host, BenchmarkCombination combination, BenchmarkPhaseResult result, bool captureLatencies, CancellationToken token)
        {
            Uri uri = BuildWebSocketUri(host.BaseAddress, GetWebSocketPath(combination.Scenario));

            if (combination.Scenario == BenchmarkScenario.WebSocketConnectClose)
            {
                await RunWebSocketConnectCloseWorkerAsync(host.BaseAddress, uri, combination, result, captureLatencies, token).ConfigureAwait(false);
                return;
            }

            using (ClientWebSocket socket = CreateWebSocketClient(host.BaseAddress))
            {
                await socket.ConnectAsync(uri, token).ConfigureAwait(false);

                while (!token.IsCancellationRequested)
                {
                    long startTimestamp = Stopwatch.GetTimestamp();

                    try
                    {
                        RequestExecutionResult executionResult = await ExecuteWebSocketOperationAsync(socket, combination, token).ConfigureAwait(false);

                        double latencyMs = GetElapsedMilliseconds(startTimestamp, Stopwatch.GetTimestamp());
                        lock (result.SyncRoot)
                        {
                            result.SuccessCount++;
                            result.RequestBytes += executionResult.RequestBytes;
                            result.ResponseBytes += executionResult.ResponseBytes;
                            if (captureLatencies)
                            {
                                result.LatenciesMs.Add(latencyMs);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }

                        lock (result.SyncRoot)
                        {
                            result.FailureCount++;
                        }
                    }
                    catch (Exception exception)
                    {
                        if (IsCancellationRelatedFailure(exception, token))
                        {
                            break;
                        }

                        DebugFailure(combination, exception);
                        lock (result.SyncRoot)
                        {
                            result.FailureCount++;
                        }
                        break;
                    }
                }

                try
                {
                    if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                    {
                        await CloseWebSocketQuietlyAsync(socket, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                    try { socket.Abort(); } catch (Exception) { }
                }
            }
        }

        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private async Task RunHttp3WorkerAsync(Http3BenchmarkClient client, BenchmarkCombination combination, BenchmarkPhaseResult result, bool captureLatencies, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                long startTimestamp = Stopwatch.GetTimestamp();

                try
                {
                    byte[] requestBody = GetRequestBody(combination.Scenario);
                    int responseBytes;

                    if (RequiresByteValidation(combination.Scenario))
                    {
                        byte[] expectedResponseBytes = GetExpectedResponseBytes(combination.Scenario);
                        responseBytes = await client.SendAndValidateBytesAsync(combination.Scenario, requestBody, expectedResponseBytes, token).ConfigureAwait(false);
                    }
                    else
                    {
                        byte[] body = await client.SendAsync(combination.Scenario, requestBody, token).ConfigureAwait(false);
                        if (captureLatencies)
                        {
                            ValidateHttp3Response(combination, body);
                        }

                        responseBytes = body != null ? body.Length : 0;
                    }

                    double latencyMs = GetElapsedMilliseconds(startTimestamp, Stopwatch.GetTimestamp());

                    lock (result.SyncRoot)
                    {
                        result.SuccessCount++;
                        result.RequestBytes += requestBody != null ? requestBody.Length : 0;
                        result.ResponseBytes += responseBytes;
                        if (captureLatencies)
                        {
                            result.LatenciesMs.Add(latencyMs);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
                catch (Exception exception)
                {
                    if (IsCancellationRelatedFailure(exception, token))
                    {
                        break;
                    }

                    DebugFailure(combination, exception);
                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
            }
        }

        private async Task<RequestExecutionResult> ExecuteRequestAsync(HttpClient client, BenchmarkCombination combination, bool validateBody, CancellationToken token)
        {
            using (HttpRequestMessage request = CreateRequest(combination))
            {
                HttpCompletionOption completionOption = RequiresByteValidation(combination.Scenario)
                    ? HttpCompletionOption.ResponseHeadersRead
                    : HttpCompletionOption.ResponseContentRead;

                using (HttpResponseMessage response = await client.SendAsync(request, completionOption, token).ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        throw new IOException("Unexpected status code: " + ((int)response.StatusCode).ToString());
                    }

                    if (RequiresByteValidation(combination.Scenario))
                    {
                        int responseBytes = await ReadAndValidateResponseBytesAsync(response, combination, validateBody, token).ConfigureAwait(false);
                        RequestExecutionResult byteResult = new RequestExecutionResult();
                        byteResult.RequestBytes = GetRequestBodyLength(combination.Scenario);
                        byteResult.ResponseBytes = responseBytes;
                        return byteResult;
                    }

                    string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (validateBody)
                    {
                        ValidateResponseBody(combination, body);
                    }

                    RequestExecutionResult result = new RequestExecutionResult();
                    result.RequestBytes = GetRequestBodyLength(combination.Scenario);
                    result.ResponseBytes = Encoding.UTF8.GetByteCount(body ?? String.Empty);
                    return result;
                }
            }
        }

        private HttpRequestMessage CreateRequest(BenchmarkCombination combination)
        {
            HttpRequestMessage request;

            if (combination.Scenario == BenchmarkScenario.Hello)
            {
                request = new HttpRequestMessage(HttpMethod.Get, "/benchmark/hello");
            }
            else if (combination.Scenario == BenchmarkScenario.Echo)
            {
                request = new HttpRequestMessage(HttpMethod.Post, "/benchmark/echo");
                ByteArrayContent content = new ByteArrayContent(_RequestPayload);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                request.Content = content;
            }
            else if (combination.Scenario == BenchmarkScenario.ChunkedEcho)
            {
                request = new HttpRequestMessage(HttpMethod.Post, "/benchmark/echo");
                StreamingChunkedContent content = new StreamingChunkedContent(_RequestPayload, "text/plain", 32);
                request.Content = content;
            }
            else if (combination.Scenario == BenchmarkScenario.ChunkedResponse)
            {
                request = new HttpRequestMessage(HttpMethod.Get, "/benchmark/chunked-response");
            }
            else if (combination.Scenario == BenchmarkScenario.Json)
            {
                request = new HttpRequestMessage(HttpMethod.Get, "/benchmark/json");
            }
            else if (combination.Scenario == BenchmarkScenario.SerializeJson)
            {
                request = new HttpRequestMessage(HttpMethod.Get, "/benchmark/serialize-json");
            }
            else if (combination.Scenario == BenchmarkScenario.JsonEcho)
            {
                request = new HttpRequestMessage(HttpMethod.Post, "/benchmark/json-echo");
                ByteArrayContent content = new ByteArrayContent(_JsonRequestPayload);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Content = content;
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Get, "/benchmark/sse");
            }

            if (combination.Protocol == BenchmarkProtocol.Http11)
            {
                request.Version = HttpVersion.Version11;
            }
            else if (combination.Protocol == BenchmarkProtocol.Http2)
            {
                request.Version = HttpVersion.Version20;
            }
            else
            {
                request.Version = HttpVersion.Version30;
            }

            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return request;
        }

        private async Task RunWebSocketConnectCloseWorkerAsync(Uri baseAddress, Uri uri, BenchmarkCombination combination, BenchmarkPhaseResult result, bool captureLatencies, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                long startTimestamp = Stopwatch.GetTimestamp();

                try
                {
                    using (ClientWebSocket socket = CreateWebSocketClient(baseAddress))
                    {
                        await socket.ConnectAsync(uri, token).ConfigureAwait(false);
                        await CloseWebSocketQuietlyAsync(socket, token).ConfigureAwait(false);
                    }

                    double latencyMs = GetElapsedMilliseconds(startTimestamp, Stopwatch.GetTimestamp());
                    lock (result.SyncRoot)
                    {
                        result.SuccessCount++;
                        if (captureLatencies)
                        {
                            result.LatenciesMs.Add(latencyMs);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
                catch (Exception exception)
                {
                    if (IsCancellationRelatedFailure(exception, token))
                    {
                        break;
                    }

                    DebugFailure(combination, exception);
                    lock (result.SyncRoot)
                    {
                        result.FailureCount++;
                    }
                }
            }
        }

        private async Task<RequestExecutionResult> ExecuteWebSocketOperationAsync(ClientWebSocket socket, BenchmarkCombination combination, CancellationToken token)
        {
            if (combination == null) throw new ArgumentNullException(nameof(combination));

            if (combination.Scenario == BenchmarkScenario.WebSocketEcho)
            {
                byte[] payload = _RequestPayload;
                string expected = Encoding.UTF8.GetString(payload);
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                string response = await ReceiveWebSocketTextAsync(socket, token).ConfigureAwait(false);
                if (!String.Equals(response, expected, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid websocket echo response payload.");
                }

                return new RequestExecutionResult
                {
                    RequestBytes = payload.Length,
                    ResponseBytes = Encoding.UTF8.GetByteCount(response ?? String.Empty)
                };
            }

            if (combination.Scenario == BenchmarkScenario.WebSocketClientText)
            {
                byte[] payload = _RequestPayload;
                await socket.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                string response = await ReceiveWebSocketTextAsync(socket, token).ConfigureAwait(false);
                if (!String.Equals(response, "ok", StringComparison.Ordinal))
                {
                    throw new IOException("Invalid websocket client-text acknowledgement payload.");
                }

                return new RequestExecutionResult
                {
                    RequestBytes = payload.Length,
                    ResponseBytes = Encoding.UTF8.GetByteCount(response)
                };
            }

            if (combination.Scenario == BenchmarkScenario.WebSocketServerText)
            {
                byte[] triggerPayload = Encoding.UTF8.GetBytes("go");
                await socket.SendAsync(new ArraySegment<byte>(triggerPayload), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
                string response = await ReceiveWebSocketTextAsync(socket, token).ConfigureAwait(false);
                if (!String.Equals(response, _HelloPayload, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid websocket server-text response payload.");
                }

                return new RequestExecutionResult
                {
                    RequestBytes = triggerPayload.Length,
                    ResponseBytes = Encoding.UTF8.GetByteCount(response)
                };
            }

            throw new NotSupportedException("Unsupported websocket benchmark scenario.");
        }

        private ClientWebSocket CreateWebSocketClient(Uri baseAddress)
        {
            ClientWebSocket socket = new ClientWebSocket();
            if (baseAddress != null && String.Equals(baseAddress.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                socket.Options.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            }

            return socket;
        }

        private string GetWebSocketPath(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.WebSocketConnectClose) return "/benchmark/ws-connect-close";
            if (scenario == BenchmarkScenario.WebSocketClientText) return "/benchmark/ws-client-text";
            if (scenario == BenchmarkScenario.WebSocketServerText) return "/benchmark/ws-server-text";
            return "/benchmark/ws-echo";
        }

        private static Uri BuildWebSocketUri(Uri baseAddress, string path)
        {
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));
            string scheme = String.Equals(baseAddress.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            return new UriBuilder(baseAddress) { Scheme = scheme, Path = path, Query = String.Empty }.Uri;
        }

        private static async Task<string> ReceiveWebSocketTextAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                int offset = 0;
                while (true)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    offset += result.Count;
                    if (result.EndOfMessage)
                    {
                        return Encoding.UTF8.GetString(buffer, 0, offset);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task CloseWebSocketQuietlyAsync(ClientWebSocket socket, CancellationToken token)
        {
            if (socket == null) return;

            try
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                try { socket.Abort(); } catch (Exception) { }
            }
        }

        private void DebugFailure(BenchmarkCombination combination, Exception exception)
        {
            if (!_DebugFailures) return;
            if (combination == null) return;
            if (exception == null) return;

            Console.Error.WriteLine(
                "[benchmark-failure] "
                + combination.Target.ToString() + " / "
                + combination.Protocol.ToString() + " / "
                + combination.Scenario.ToString() + " :: "
                + exception.GetType().Name + " :: "
                + exception.Message);

            Exception innerException = exception.InnerException;
            while (innerException != null)
            {
                Console.Error.WriteLine(
                    "[benchmark-failure-inner] "
                    + combination.Target.ToString() + " / "
                    + combination.Protocol.ToString() + " / "
                    + combination.Scenario.ToString() + " :: "
                    + innerException.GetType().Name + " :: "
                    + innerException.Message);
                innerException = innerException.InnerException;
            }
        }

        private void ValidateResponseBody(BenchmarkCombination combination, string body)
        {
            if (combination.Scenario == BenchmarkScenario.Hello)
            {
                if (!String.Equals(body, _HelloPayload, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid hello response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.Echo)
            {
                if (!String.Equals(body, Encoding.UTF8.GetString(_RequestPayload), StringComparison.Ordinal))
                {
                    throw new IOException("Invalid echo response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.ChunkedEcho)
            {
                if (!String.Equals(body, Encoding.UTF8.GetString(_RequestPayload), StringComparison.Ordinal))
                {
                    throw new IOException("Invalid chunked echo response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.ChunkedResponse)
            {
                if (!String.Equals(body, _HelloPayload, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid chunked response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.Json)
            {
                if (!String.Equals(body, _JsonResponsePayload, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid JSON response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.SerializeJson)
            {
                if (!String.Equals(body, _SerializedJsonResponsePayload, StringComparison.Ordinal))
                {
                    throw new IOException("Invalid serialized JSON response payload.");
                }
            }
            else if (combination.Scenario == BenchmarkScenario.JsonEcho)
            {
                if (!String.Equals(body, Encoding.UTF8.GetString(_JsonRequestPayload), StringComparison.Ordinal))
                {
                    throw new IOException("Invalid JSON echo response payload.");
                }
            }
            else
            {
                int dataEventCount = body.Split("data: ", StringSplitOptions.None).Length - 1;
                if (dataEventCount != _Options.ServerSentEventCount)
                {
                    throw new IOException("Invalid server-sent event count.");
                }
            }
        }

        private void ValidateHttp3Response(BenchmarkCombination combination, byte[] body)
        {
            if (RequiresByteValidation(combination.Scenario))
            {
                byte[] expectedBytes = GetExpectedResponseBytes(combination.Scenario);
                ValidateResponseChunk(expectedBytes, 0, body ?? Array.Empty<byte>(), body != null ? body.Length : 0);
                return;
            }

            string decodedBody = body != null ? Encoding.UTF8.GetString(body) : String.Empty;
            ValidateResponseBody(combination, decodedBody);
        }

        private bool RequiresByteValidation(BenchmarkScenario scenario)
        {
            return scenario == BenchmarkScenario.Hello
                || scenario == BenchmarkScenario.Echo
                || scenario == BenchmarkScenario.ChunkedEcho
                || scenario == BenchmarkScenario.ChunkedResponse;
        }

        private async Task<int> ReadAndValidateResponseBytesAsync(HttpResponseMessage response, BenchmarkCombination combination, bool validateBody, CancellationToken token)
        {
            Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            byte[] expectedBytes = GetExpectedResponseBytes(combination.Scenario);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(4096, _Options.PayloadBytes));
            int expectedOffset = 0;
            int responseBytes = 0;

            try
            {
                while (true)
                {
                    int bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead < 1) break;

                    if (validateBody)
                    {
                        ValidateResponseChunk(expectedBytes, expectedOffset, buffer, bytesRead);
                    }

                    expectedOffset += bytesRead;
                    responseBytes += bytesRead;
                }

                if (validateBody && expectedOffset != expectedBytes.Length)
                {
                    throw new IOException("Invalid response length.");
                }

                return responseBytes;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                responseStream.Dispose();
            }
        }

        private byte[] GetExpectedResponseBytes(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.Hello) return _HelloPayloadBytes;
            if (scenario == BenchmarkScenario.Echo) return _RequestPayload;
            if (scenario == BenchmarkScenario.ChunkedEcho) return _RequestPayload;
            if (scenario == BenchmarkScenario.ChunkedResponse) return _HelloPayloadBytes;
            return Array.Empty<byte>();
        }

        private void ValidateResponseChunk(byte[] expectedBytes, int expectedOffset, byte[] actualBuffer, int actualCount)
        {
            if (expectedBytes == null) throw new ArgumentNullException(nameof(expectedBytes));
            if (actualBuffer == null) throw new ArgumentNullException(nameof(actualBuffer));
            if (expectedOffset < 0) throw new ArgumentOutOfRangeException(nameof(expectedOffset));
            if (actualCount < 0) throw new ArgumentOutOfRangeException(nameof(actualCount));
            if ((expectedOffset + actualCount) > expectedBytes.Length)
            {
                throw new IOException("Invalid response length.");
            }

            for (int i = 0; i < actualCount; i++)
            {
                if (actualBuffer[i] != expectedBytes[expectedOffset + i])
                {
                    throw new IOException("Invalid response payload.");
                }
            }
        }

        private byte[] GetRequestBody(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.Echo) return _RequestPayload;
            if (scenario == BenchmarkScenario.ChunkedEcho) return _RequestPayload;
            if (scenario == BenchmarkScenario.JsonEcho) return _JsonRequestPayload;
            return null;
        }

        private static string BuildJsonPayload(int payloadBytes)
        {
            int clampedPayloadBytes = payloadBytes;
            if (clampedPayloadBytes < 128) clampedPayloadBytes = 128;

            BenchmarkJsonPayload payload = new BenchmarkJsonPayload();
            payload.Message = "benchmark";
            payload.Category = "json";
            payload.Sequence = 1;
            payload.Content = String.Empty;

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions();
            serializerOptions.WriteIndented = false;

            string json = JsonSerializer.Serialize(payload, serializerOptions);
            if (Encoding.UTF8.GetByteCount(json) >= clampedPayloadBytes) return json;

            int remainingBytes = clampedPayloadBytes - Encoding.UTF8.GetByteCount(json);
            payload.Content = new string('j', remainingBytes);

            json = JsonSerializer.Serialize(payload, serializerOptions);
            while (Encoding.UTF8.GetByteCount(json) > clampedPayloadBytes && payload.Content.Length > 0)
            {
                payload.Content = payload.Content.Substring(0, payload.Content.Length - 1);
                json = JsonSerializer.Serialize(payload, serializerOptions);
            }

            return json;
        }

        private int GetRequestBodyLength(BenchmarkScenario scenario)
        {
            byte[] body = GetRequestBody(scenario);
            return body != null ? body.Length : 0;
        }

        private sealed class StreamingChunkedContent : HttpContent
        {
            private readonly byte[] _Payload;
            private readonly int _ChunkSize;

            public StreamingChunkedContent(byte[] payload, string contentType, int chunkSize)
            {
                _Payload = payload ?? Array.Empty<byte>();
                _ChunkSize = chunkSize < 1 ? 1 : chunkSize;
                Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                for (int offset = 0; offset < _Payload.Length; offset += _ChunkSize)
                {
                    int count = Math.Min(_ChunkSize, _Payload.Length - offset);
                    await stream.WriteAsync(_Payload, offset, count).ConfigureAwait(false);
                }
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }
        }

        private double GetMean(List<double> values)
        {
            if (values == null || values.Count < 1) return 0;

            double total = 0;
            for (int i = 0; i < values.Count; i++)
            {
                total += values[i];
            }

            return total / values.Count;
        }

        private double GetPercentile(List<double> values, double percentile)
        {
            if (values == null || values.Count < 1) return 0;
            if (percentile <= 0) return values[0];
            if (percentile >= 1) return values[values.Count - 1];

            double rawIndex = (values.Count - 1) * percentile;
            int index = (int)Math.Round(rawIndex, MidpointRounding.AwayFromZero);
            if (index < 0) index = 0;
            if (index >= values.Count) index = values.Count - 1;
            return values[index];
        }

        private double GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            return (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;
        }

        private bool IsCancellationRelatedFailure(Exception exception, CancellationToken token)
        {
            if (!token.IsCancellationRequested) return false;

            Exception current = exception;
            while (current != null)
            {
                if (current is OperationCanceledException || current is TaskCanceledException)
                {
                    return true;
                }

                current = current.InnerException;
            }

            if (exception is HttpRequestException)
            {
                return true;
            }

            return false;
        }

        private sealed class BenchmarkPhaseResult
        {
            public object SyncRoot { get; } = new object();

            public long SuccessCount { get; set; }

            public long FailureCount { get; set; }

            public long RequestBytes { get; set; }

            public long ResponseBytes { get; set; }

            public double DurationMs { get; set; }

            public List<double> LatenciesMs { get; } = new List<double>();
        }

        private sealed class RequestExecutionResult
        {
            public int RequestBytes { get; set; }

            public int ResponseBytes { get; set; }
        }

    }
}
