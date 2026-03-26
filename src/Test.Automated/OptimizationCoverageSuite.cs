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

            await ExecuteTestAsync("Static route snapshots remain readable during concurrent mutation", TestStaticRouteSnapshotsAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Default serialization helper preserves pretty and compact JSON", TestDefaultSerializationHelperAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 cached response headers preserve dynamic fields", TestHttp1CachedHeadersAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 keep-alive pooling resets request state", TestHttp1KeepAlivePoolingAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 stream send preserves direct passthrough body", TestHttp1StreamSendAsync).ConfigureAwait(false);
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
            }
        }

        private static Task TestStaticRouteSnapshotsAsync()
        {
            StaticRouteManager routeManager = new StaticRouteManager();
            Func<HttpContextBase, Task> handler = NoOpRouteAsync;
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 25; i++)
            {
                int pathIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int iteration = 0; iteration < 100; iteration++)
                    {
                        string path = "/snapshot/" + pathIndex.ToString() + "/" + iteration.ToString();
                        routeManager.Add(CoreHttpMethod.GET, path, handler);
                        routeManager.Exists(CoreHttpMethod.GET, path);
                        routeManager.GetAll();
                        routeManager.Match(CoreHttpMethod.GET, path, out StaticRoute route);

                        if (route == null)
                        {
                            throw new InvalidOperationException("Expected route lookup to succeed during mutation.");
                        }

                        routeManager.Remove(CoreHttpMethod.GET, path);
                    }
                }));
            }

            return Task.WhenAll(tasks);
        }

        private Task TestDefaultSerializationHelperAsync()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper();
            StateObservationResponse payload = new StateObservationResponse();
            payload.TraceHeader = "abc";
            payload.Body = "hello";
            payload.ContentLength = 5;
            payload.ChunkedTransfer = false;

            string compact = serializer.SerializeJson(payload, false);
            string pretty = serializer.SerializeJson(payload, true);

            if (String.IsNullOrEmpty(compact) || String.IsNullOrEmpty(pretty))
            {
                throw new InvalidOperationException("Serialized JSON should not be empty.");
            }

            if (compact.Contains(Environment.NewLine, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Compact JSON should not be indented.");
            }

            if (!pretty.Contains(Environment.NewLine, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Pretty JSON should be indented.");
            }

            StateObservationResponse compactRoundTrip = serializer.DeserializeJson<StateObservationResponse>(compact);
            StateObservationResponse prettyRoundTrip = serializer.DeserializeJson<StateObservationResponse>(pretty);

            if (compactRoundTrip == null || prettyRoundTrip == null)
            {
                throw new InvalidOperationException("Serialized JSON should round-trip to typed instances.");
            }

            if (!String.Equals(compactRoundTrip.TraceHeader, payload.TraceHeader, StringComparison.Ordinal)
                || !String.Equals(prettyRoundTrip.Body, payload.Body, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Round-tripped JSON payload does not match the source instance.");
            }

            return Task.CompletedTask;
        }

        private async Task TestHttp1CachedHeadersAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureCachedHeaderRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage firstResponse = await client.GetAsync(new Uri(host.BaseAddress, "/cache?case=first")).ConfigureAwait(false);
                    HttpResponseMessage secondResponse = await client.GetAsync(new Uri(host.BaseAddress, "/cache?case=second")).ConfigureAwait(false);
                    string firstBody = await firstResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string secondBody = await secondResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!String.Equals(firstBody, "alpha", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected first response body.");
                    }

                    if (!String.Equals(secondBody, "beta-beta", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected second response body.");
                    }

                    if (!firstResponse.Headers.Contains("X-Test-Only"))
                    {
                        throw new InvalidOperationException("Expected first response custom header.");
                    }

                    if (secondResponse.Headers.Contains("X-Test-Only"))
                    {
                        throw new InvalidOperationException("Second response should not inherit a cached custom header.");
                    }

                    if (firstResponse.Content.Headers.ContentLength != 5 || secondResponse.Content.Headers.ContentLength != 9)
                    {
                        throw new InvalidOperationException("Content-Length should remain dynamic when header templates are cached.");
                    }
                }
            }
        }

        private async Task TestHttp1KeepAlivePoolingAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureStateRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", host.BaseAddress.Port).ConfigureAwait(false);

                    using (NetworkStream stream = client.GetStream())
                    {
                        string firstBody = "first-body";
                        byte[] firstBodyBytes = Encoding.UTF8.GetBytes(firstBody);
                        string firstRequest =
                            "POST /state HTTP/1.1\r\n" +
                            "Host: 127.0.0.1\r\n" +
                            "Connection: keep-alive\r\n" +
                            "Content-Type: text/plain\r\n" +
                            "X-Trace: first\r\n" +
                            "Content-Length: " + firstBodyBytes.Length.ToString() + "\r\n\r\n" +
                            firstBody;
                        byte[] firstRequestBytes = Encoding.ASCII.GetBytes(firstRequest);

                        await stream.WriteAsync(firstRequestBytes, 0, firstRequestBytes.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);

                        RawHttpResponse firstResponse = await ReadRawHttpResponseAsync(stream).ConfigureAwait(false);
                        StateObservationResponse firstObservation = Deserialize<StateObservationResponse>(firstResponse.Body);

                        if (!String.Equals(firstObservation.TraceHeader, "first", StringComparison.Ordinal)
                            || !String.Equals(firstObservation.Body, firstBody, StringComparison.Ordinal)
                            || firstObservation.ContentLength != firstBodyBytes.Length)
                        {
                            throw new InvalidOperationException("First keep-alive request did not echo the expected state.");
                        }

                        string secondRequest =
                            "GET /state HTTP/1.1\r\n" +
                            "Host: 127.0.0.1\r\n" +
                            "Connection: close\r\n\r\n";
                        byte[] secondRequestBytes = Encoding.ASCII.GetBytes(secondRequest);

                        await stream.WriteAsync(secondRequestBytes, 0, secondRequestBytes.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);

                        RawHttpResponse secondResponse = await ReadRawHttpResponseAsync(stream).ConfigureAwait(false);
                        StateObservationResponse secondObservation = Deserialize<StateObservationResponse>(secondResponse.Body);

                        if (!String.IsNullOrEmpty(secondObservation.TraceHeader))
                        {
                            throw new InvalidOperationException("Pooled request state leaked a header into the next keep-alive request.");
                        }

                        if (!String.IsNullOrEmpty(secondObservation.Body) || secondObservation.ContentLength != 0 || secondObservation.ChunkedTransfer)
                        {
                            throw new InvalidOperationException("Pooled request state leaked body metadata into the next keep-alive request.");
                        }
                    }
                }
            }
        }

        private async Task TestHttp1StreamSendAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureStreamRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    string requestBody = "stream-payload";
                    StringContent content = new StringContent(requestBody, Encoding.UTF8, "text/plain");
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/stream"), content).ConfigureAwait(false);
                    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!String.Equals(responseBody, requestBody, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Stream send response body did not match the direct passthrough request body.");
                    }
                }
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

            using (LoopbackServerHost host = new LoopbackServerHost(true, false, true, ConfigureHeaderObservationRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(3, 0)))
                {
                    await ValidateHeaderObservationAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        private static void ConfigureCachedHeaderRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/cache", async (HttpContextBase context) =>
            {
                string testCase = context.Request.RetrieveQueryValue("case");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";

                if (String.Equals(testCase, "first", StringComparison.Ordinal))
                {
                    context.Response.Headers["X-Test-Only"] = "yes";
                    await context.Response.Send("alpha", context.Token).ConfigureAwait(false);
                }
                else
                {
                    await context.Response.Send("beta-beta", context.Token).ConfigureAwait(false);
                }
            });
        }

        private static void ConfigureStateRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/state", SendStateObservationAsync);
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/state", SendStateObservationAsync);
        }

        private static void ConfigureStreamRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/stream", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.ContentLength, context.Request.Data, context.Token).ConfigureAwait(false);
            });
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

        private static async Task SendStateObservationAsync(HttpContextBase context)
        {
            StateObservationResponse response = new StateObservationResponse();
            response.TraceHeader = context.Request.RetrieveHeaderValue("x-trace");
            response.Body = context.Request.DataAsString;
            response.ContentLength = context.Request.ContentLength;
            response.ChunkedTransfer = context.Request.ChunkedTransfer;

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.Send(JsonSerializer.Serialize(response), context.Token).ConfigureAwait(false);
        }

        private static Task NoOpRouteAsync(HttpContextBase context)
        {
            return Task.CompletedTask;
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
                    responseModel = Deserialize<HeaderObservationResponse>(json);
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

        private T Deserialize<T>(string json)
        {
            T model = JsonSerializer.Deserialize<T>(json, _JsonSerializerOptions);

            if (model == null)
            {
                throw new InvalidOperationException("JSON payload deserialized to null.");
            }

            return model;
        }

        private static async Task<RawHttpResponse> ReadRawHttpResponseAsync(NetworkStream stream)
        {
            List<byte> headerBytes = new List<byte>();
            byte[] oneByte = new byte[1];

            while (true)
            {
                int bytesRead = await stream.ReadAsync(oneByte, 0, 1).ConfigureAwait(false);
                if (bytesRead < 1)
                {
                    throw new IOException("Unexpected end of stream while reading HTTP response headers.");
                }

                headerBytes.Add(oneByte[0]);

                if (headerBytes.Count >= 4
                    && headerBytes[headerBytes.Count - 4] == (byte)'\r'
                    && headerBytes[headerBytes.Count - 3] == (byte)'\n'
                    && headerBytes[headerBytes.Count - 2] == (byte)'\r'
                    && headerBytes[headerBytes.Count - 1] == (byte)'\n')
                {
                    break;
                }
            }

            string headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            string[] headerLines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            RawHttpResponse response = new RawHttpResponse();
            response.StatusLine = headerLines[0];

            for (int i = 1; i < headerLines.Length; i++)
            {
                if (String.IsNullOrEmpty(headerLines[i]))
                {
                    continue;
                }

                int separator = headerLines[i].IndexOf(':');
                if (separator > 0)
                {
                    string key = headerLines[i].Substring(0, separator);
                    string value = headerLines[i].Substring(separator + 1).Trim();
                    response.Headers[key] = value;
                }
            }

            int contentLength = 0;
            string contentLengthValue = response.Headers["Content-Length"];

            if (!String.IsNullOrEmpty(contentLengthValue))
            {
                contentLength = Int32.Parse(contentLengthValue);
            }

            byte[] bodyBytes = new byte[contentLength];
            int totalRead = 0;

            while (totalRead < contentLength)
            {
                int bytesRead = await stream.ReadAsync(bodyBytes, totalRead, contentLength - totalRead).ConfigureAwait(false);
                if (bytesRead < 1)
                {
                    throw new IOException("Unexpected end of stream while reading HTTP response body.");
                }

                totalRead += bytesRead;
            }

            response.Body = Encoding.UTF8.GetString(bodyBytes);
            return response;
        }
    }
}
