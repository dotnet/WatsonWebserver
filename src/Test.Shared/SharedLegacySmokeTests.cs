namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Net.Http;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared legacy-style smoke tests executed by both runners.
    /// </summary>
    public static class SharedLegacySmokeTests
    {
        private const string Default500BodyFragment = "There's a problem here, but it's on me, not you.";

        /// <summary>
        /// Verify a basic HTTP/1.1 GET request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicGetAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/get")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 GET request to succeed.");
                    }

                    if (!String.Equals(body, "GET response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 GET response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 POST request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicPostAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("test data", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/post"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 POST request to succeed.");
                    }

                    if (!String.Equals(body, "POST response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 POST response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 POST body can be read and echoed by the route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BodyEchoAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("echo-body", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo-body"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 body echo request to succeed.");
                    }

                    if (!String.Equals(body, "echo-body", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 body echo response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 PUT request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicPutAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent("put-data", Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PutAsync(new Uri(host.BaseAddress, "/test/put"), content).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 PUT request to succeed.");
                    }

                    if (!String.Equals(body, "PUT response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 PUT response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 DELETE request succeeds against a low-level route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11BasicDeleteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Delete, new Uri(host.BaseAddress, "/test/delete"));

                    using (request)
                    {
                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected HTTP/1.1 DELETE request to succeed.");
                        }

                        if (!String.Equals(body, "DELETE response", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 DELETE response body.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 parameter route resolves and returns the parameterized value.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ParameterRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/users/42")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 parameter route request to succeed.");
                    }

                    if (!String.Equals(body, "User 42", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 parameter route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a basic HTTP/1.1 query-string route resolves and returns the query value.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11QueryStringRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/query?name=alpha")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 query-string route request to succeed.");
                    }

                    if (!String.Equals(body, "Query alpha", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 query-string route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an unmatched HTTP/1.1 route returns the default 404 response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11NotFoundRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/does-not-exist")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if ((int)response.StatusCode != 404)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 unmatched route to return 404.");
                    }

                    if (!String.Equals(body, "not-found", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 unmatched route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a static content route returns the expected content.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11StaticContentRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/static/test")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 static content route to succeed.");
                    }

                    if (!String.Equals(body, "Static route response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 static content route response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify request headers are echoed back with values preserved.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11HeaderEchoAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/test/header-echo"));
                    request.Headers.TryAddWithoutValidation("X-Test-Colon", "value1:value2:value3");

                    using (request)
                    {
                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected HTTP/1.1 header echo request to succeed.");
                        }

                        if (!body.Contains("X-Test-Colon: value1:value2:value3", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 header echo response body.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 response delivers all chunks and advertises chunked transfer encoding.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedTransferEncodingAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/chunked")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked transfer response to succeed.");
                    }

                    for (int i = 1; i <= 5; i++)
                    {
                        if (!body.Contains("Chunk " + i.ToString(), StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Missing expected chunk content in HTTP/1.1 chunked response.");
                        }
                    }

                    if (response.Headers.TransferEncodingChunked != true)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked response header to advertise chunked transfer encoding.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify chunked edge-case responses succeed.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedEdgeCasesAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/chunked-edge")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked edge-case response to succeed.");
                    }

                    if (!body.Contains("single-byte", StringComparison.Ordinal)
                        || !body.Contains("large-chunk", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 chunked edge-case response body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify successful HTTP/1.1 chunked responses still emit the expected observability signals exactly once.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedResponseObservabilityAsync()
        {
            const string requestPath = "/test/chunked-observable";

            TaskCompletionSource<bool> firstSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> finalSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)> postRoutingStateSource = new TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            int responseStartingCount = 0;
            int responseSentCount = 0;
            int responseCompletedCount = 0;
            int requestAbortedCount = 0;
            int requestorDisconnectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);

                server.Routes.PostRouting = async (HttpContextBase context) =>
                {
                    if (String.Equals(context.Request.Url.RawWithoutQuery, requestPath, StringComparison.Ordinal))
                    {
                        postRoutingStateSource.TrySetResult((context.RequestAborted, context.Token.IsCancellationRequested));
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                };

                server.Events.RequestAborted += (sender, args) =>
                {
                    Interlocked.Increment(ref requestAbortedCount);
                };

                server.Events.RequestorDisconnected += (sender, args) =>
                {
                    Interlocked.Increment(ref requestorDisconnectedCount);
                };

                server.Events.ResponseStarting += (sender, args) =>
                {
                    Interlocked.Increment(ref responseStartingCount);
                };

                server.Events.ResponseSent += (sender, args) =>
                {
                    Interlocked.Increment(ref responseSentCount);
                };

                server.Events.ResponseCompleted += (sender, args) =>
                {
                    Interlocked.Increment(ref responseCompletedCount);
                };

                server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, requestPath, async (HttpContextBase context) =>
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    context.Response.ChunkedTransfer = true;

                    bool firstSendResult = await context.Response.SendChunk(Encoding.UTF8.GetBytes("chunk observable one\n"), false, context.Token).ConfigureAwait(false);
                    firstSendResultSource.TrySetResult(firstSendResult);

                    bool finalSendResult = await context.Response.SendChunk(Encoding.UTF8.GetBytes("chunk observable two\n"), true, context.Token).ConfigureAwait(false);
                    finalSendResultSource.TrySetResult(finalSendResult);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, requestPath)).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected observable HTTP/1.1 chunked response to succeed.");
                    }

                    if (!body.Contains("chunk observable one", StringComparison.Ordinal)
                        || !body.Contains("chunk observable two", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected observable HTTP/1.1 chunked response body.");
                    }

                    if (response.Headers.TransferEncodingChunked != true)
                    {
                        throw new InvalidOperationException("Expected observable HTTP/1.1 chunked response to advertise chunked transfer encoding.");
                    }
                }

                bool firstSendResult = await WaitForTaskAsync(firstSendResultSource.Task, 10000, "Observable chunked response did not complete the first send.").ConfigureAwait(false);
                bool finalSendResult = await WaitForTaskAsync(finalSendResultSource.Task, 10000, "Observable chunked response did not complete the final send.").ConfigureAwait(false);
                (bool RequestAborted, bool TokenCancelled) postRoutingState = await WaitForTaskAsync(postRoutingStateSource.Task, 10000, "Observable chunked response did not reach PostRouting.").ConfigureAwait(false);

                if (!firstSendResult || !finalSendResult)
                {
                    throw new InvalidOperationException("Expected observable HTTP/1.1 chunked sends to succeed.");
                }

                if (postRoutingState.RequestAborted || postRoutingState.TokenCancelled)
                {
                    throw new InvalidOperationException("Successful observable HTTP/1.1 chunked response should not be marked aborted or cancelled.");
                }

                if (Volatile.Read(ref requestAbortedCount) != 0)
                {
                    throw new InvalidOperationException("RequestAborted should not fire for a successful observable HTTP/1.1 chunked response.");
                }

                if (Volatile.Read(ref requestorDisconnectedCount) != 0)
                {
                    throw new InvalidOperationException("RequestorDisconnected should not fire for a successful observable HTTP/1.1 chunked response.");
                }

                if (Volatile.Read(ref responseStartingCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseStarting to fire exactly once for a successful observable HTTP/1.1 chunked response.");
                }

                if (Volatile.Read(ref responseSentCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseSent to fire exactly once for a successful observable HTTP/1.1 chunked response.");
                }

                if (Volatile.Read(ref responseCompletedCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseCompleted to fire exactly once for a successful observable HTTP/1.1 chunked response.");
                }

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/1.1 chunked response disconnect on the final chunk emits abort/disconnect telemetry without false success signals.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedDisconnectDuringFinalChunkAsync()
        {
            const string requestPath = "/test/chunked-disconnect-final";
            byte[] finalChunkPayload = Encoding.UTF8.GetBytes(new string('z', 512 * 1024));

            TaskCompletionSource<bool> firstSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> finalSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)> postRoutingStateSource = new TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestAbortedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestorDisconnectedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int responseStartingCount = 0;
            int responseSentCount = 0;
            int responseCompletedCount = 0;
            int requestAbortedCount = 0;
            int requestorDisconnectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);

                server.Routes.PostRouting = async (HttpContextBase context) =>
                {
                    if (String.Equals(context.Request.Url.RawWithoutQuery, requestPath, StringComparison.Ordinal))
                    {
                        postRoutingStateSource.TrySetResult((context.RequestAborted, context.Token.IsCancellationRequested));
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                };

                server.Events.RequestAborted += (sender, args) =>
                {
                    Interlocked.Increment(ref requestAbortedCount);
                    requestAbortedObserved.TrySetResult(true);
                };

                server.Events.RequestorDisconnected += (sender, args) =>
                {
                    Interlocked.Increment(ref requestorDisconnectedCount);
                    requestorDisconnectedObserved.TrySetResult(true);
                };

                server.Events.ResponseStarting += (sender, args) =>
                {
                    Interlocked.Increment(ref responseStartingCount);
                };

                server.Events.ResponseSent += (sender, args) =>
                {
                    Interlocked.Increment(ref responseSentCount);
                };

                server.Events.ResponseCompleted += (sender, args) =>
                {
                    Interlocked.Increment(ref responseCompletedCount);
                };

                server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, requestPath, async (HttpContextBase context) =>
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    context.Response.ChunkedTransfer = true;

                    bool firstSendResult = await context.Response.SendChunk(Encoding.UTF8.GetBytes("chunk before disconnect\n"), false, context.Token).ConfigureAwait(false);
                    firstSendResultSource.TrySetResult(firstSendResult);

                    await Task.Delay(200).ConfigureAwait(false);

                    bool finalSendResult = await context.Response.SendChunk(finalChunkPayload, true, context.Token).ConfigureAwait(false);
                    finalSendResultSource.TrySetResult(finalSendResult);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);
                    client.NoDelay = true;

                    NetworkStream stream = client.GetStream();
                    byte[] requestBytes = Encoding.ASCII.GetBytes(
                        "GET " + requestPath + " HTTP/1.1\r\n" +
                        "Host: 127.0.0.1\r\n" +
                        "Connection: close\r\n" +
                        "\r\n");

                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    string partialResponse = await ReadUntilContainsAsync(stream, "chunk before disconnect", TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if (!partialResponse.Contains("chunk before disconnect", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected to receive the first chunk before aborting the client transport.");
                    }

                    client.Client.LingerState = new LingerOption(true, 0);
                }

                bool firstSendResult = await WaitForTaskAsync(firstSendResultSource.Task, 10000, "Chunked disconnect scenario did not complete the first send.").ConfigureAwait(false);
                bool finalSendResult = await WaitForTaskAsync(finalSendResultSource.Task, 10000, "Chunked disconnect scenario did not complete the final send.").ConfigureAwait(false);
                (bool RequestAborted, bool TokenCancelled) postRoutingState = await WaitForTaskAsync(postRoutingStateSource.Task, 10000, "Chunked disconnect scenario did not reach PostRouting.").ConfigureAwait(false);

                if (!firstSendResult)
                {
                    throw new InvalidOperationException("Expected the first chunk send to succeed before the client disconnected.");
                }

                if (finalSendResult)
                {
                    throw new InvalidOperationException("Expected the final chunk send to fail after the client reset the socket.");
                }

                if (!postRoutingState.RequestAborted || !postRoutingState.TokenCancelled)
                {
                    throw new InvalidOperationException("Expected the chunked disconnect request to be marked aborted and have its request token cancelled before PostRouting.");
                }

                await WaitForTaskAsync(requestAbortedObserved.Task, 10000, "Chunked disconnect scenario did not emit RequestAborted after the final chunk send failed.").ConfigureAwait(false);
                await WaitForTaskAsync(requestorDisconnectedObserved.Task, 10000, "Chunked disconnect scenario did not emit RequestorDisconnected after the final chunk send failed.").ConfigureAwait(false);

                if (Volatile.Read(ref requestAbortedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestAborted to fire exactly once for the chunked disconnect response.");
                }

                if (Volatile.Read(ref requestorDisconnectedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestorDisconnected to fire exactly once for the chunked disconnect response.");
                }

                if (Volatile.Read(ref responseStartingCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseStarting to fire exactly once for the chunked disconnect response.");
                }

                if (Volatile.Read(ref responseSentCount) != 0)
                {
                    throw new InvalidOperationException("ResponseSent should not fire when the client disconnects before the final chunk is written.");
                }

                if (Volatile.Read(ref responseCompletedCount) != 0)
                {
                    throw new InvalidOperationException("ResponseCompleted should not fire when the client disconnects before the final chunk is written.");
                }

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsBytes</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyDataAsBytesAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo", "Hello, chunked world!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>DataAsString</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyDataAsStringAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo-string", "Hello, chunked string!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through <c>ReadBodyAsync</c>.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyReadBodyAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-echo-async", "Hello, async chunked!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a chunked HTTP/1.1 request body is read correctly through manual chunk reads.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ChunkedRequestBodyManualReadChunkAsync()
        {
            await TestChunkedRequestBodyAsync("/test/chunked-manual", "Hello, manual chunks!", "text/plain").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a large binary chunked HTTP/1.1 request body round-trips successfully.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11LargeChunkedRequestBodyAsync()
        {
            byte[] body = new byte[65536 + 1024];
            Random random = new Random(42);
            random.NextBytes(body);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri(host.BaseAddress, "/test/chunked-echo")))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    request.Content = new StreamContent(new System.IO.MemoryStream(body));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                    request.Headers.TransferEncodingChunked = true;

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    byte[] echoed = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 large chunked request body to succeed.");
                    }

                    if (echoed.Length != body.Length)
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 large chunked request body length.");
                    }

                    for (int i = 0; i < body.Length; i++)
                    {
                        if (echoed[i] != body[i])
                        {
                            throw new InvalidOperationException("Unexpected HTTP/1.1 large chunked request body content.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a simple HTTP/1.1 echo request preserves a plain-text payload exactly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DataPreservationHelloAsync()
        {
            await TestBodyEchoAsync("hello").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify a simple HTTP/1.1 echo request preserves a payload containing CRLF exactly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DataPreservationHelloCrLfAsync()
        {
            await TestBodyEchoAsync("hello\r\n").ConfigureAwait(false);
        }

        /// <summary>
        /// Verify HTTP/1.1 server-sent events stream the expected events with the correct content type.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/sse")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string contentType = response.Content.Headers.ContentType?.ToString() ?? String.Empty;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events response to succeed.");
                    }

                    if (!body.Contains("data: Event 1", StringComparison.Ordinal)
                        || !body.Contains("data: Event 5", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 server-sent events body.");
                    }

                    if (!contentType.Contains("text/event-stream", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events content type.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify HTTP/1.1 server-sent event edge cases preserve multi-line, special-character, and unicode content.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsEdgeCasesAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(30);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/sse-edge")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 server-sent events edge-case response to succeed.");
                    }

                    if (!body.Contains("data: Line1", StringComparison.Ordinal)
                        || !body.Contains("data: Line2", StringComparison.Ordinal)
                        || !body.Contains("data: Special: <>&\"'", StringComparison.Ordinal)
                        || !body.Contains("data: Unicode: 世界", StringComparison.Ordinal)
                        || !body.Contains("data: done", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 server-sent events edge-case body.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify successful HTTP/1.1 server-sent events still emit the expected observability signals exactly once.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsObservabilityAsync()
        {
            const string requestPath = "/test/sse-observable";

            TaskCompletionSource<bool> firstSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> finalSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)> postRoutingStateSource = new TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            int responseStartingCount = 0;
            int responseSentCount = 0;
            int responseCompletedCount = 0;
            int requestAbortedCount = 0;
            int requestorDisconnectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);

                server.Routes.PostRouting = async (HttpContextBase context) =>
                {
                    if (String.Equals(context.Request.Url.RawWithoutQuery, requestPath, StringComparison.Ordinal))
                    {
                        postRoutingStateSource.TrySetResult((context.RequestAborted, context.Token.IsCancellationRequested));
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                };

                server.Events.RequestAborted += (sender, args) =>
                {
                    Interlocked.Increment(ref requestAbortedCount);
                };

                server.Events.RequestorDisconnected += (sender, args) =>
                {
                    Interlocked.Increment(ref requestorDisconnectedCount);
                };

                server.Events.ResponseStarting += (sender, args) =>
                {
                    Interlocked.Increment(ref responseStartingCount);
                };

                server.Events.ResponseSent += (sender, args) =>
                {
                    Interlocked.Increment(ref responseSentCount);
                };

                server.Events.ResponseCompleted += (sender, args) =>
                {
                    Interlocked.Increment(ref responseCompletedCount);
                };

                server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, requestPath, async (HttpContextBase context) =>
                {
                    context.Response.ServerSentEvents = true;
                    context.Response.StatusCode = 200;

                    ServerSentEvent firstEvent = new ServerSentEvent();
                    firstEvent.Id = "1";
                    firstEvent.Data = "observable event one";
                    bool firstSendResult = await context.Response.SendEvent(firstEvent, false, context.Token).ConfigureAwait(false);
                    firstSendResultSource.TrySetResult(firstSendResult);

                    ServerSentEvent finalEvent = new ServerSentEvent();
                    finalEvent.Id = "2";
                    finalEvent.Data = "observable event two";
                    bool finalSendResult = await context.Response.SendEvent(finalEvent, true, context.Token).ConfigureAwait(false);
                    finalSendResultSource.TrySetResult(finalSendResult);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, requestPath)).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string contentType = response.Content.Headers.ContentType?.ToString() ?? String.Empty;

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected observable HTTP/1.1 server-sent events response to succeed.");
                    }

                    if (!body.Contains("data: observable event one", StringComparison.Ordinal)
                        || !body.Contains("data: observable event two", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected observable HTTP/1.1 server-sent events body.");
                    }

                    if (!contentType.Contains("text/event-stream", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected observable HTTP/1.1 server-sent events content type.");
                    }
                }

                bool firstSendResult = await WaitForTaskAsync(firstSendResultSource.Task, 10000, "Observable server-sent events response did not complete the first send.").ConfigureAwait(false);
                bool finalSendResult = await WaitForTaskAsync(finalSendResultSource.Task, 10000, "Observable server-sent events response did not complete the final send.").ConfigureAwait(false);
                (bool RequestAborted, bool TokenCancelled) postRoutingState = await WaitForTaskAsync(postRoutingStateSource.Task, 10000, "Observable server-sent events response did not reach PostRouting.").ConfigureAwait(false);

                if (!firstSendResult || !finalSendResult)
                {
                    throw new InvalidOperationException("Expected observable HTTP/1.1 server-sent events sends to succeed.");
                }

                if (postRoutingState.RequestAborted || postRoutingState.TokenCancelled)
                {
                    throw new InvalidOperationException("Successful observable HTTP/1.1 server-sent events response should not be marked aborted or cancelled.");
                }

                if (Volatile.Read(ref requestAbortedCount) != 0)
                {
                    throw new InvalidOperationException("RequestAborted should not fire for a successful observable HTTP/1.1 server-sent events response.");
                }

                if (Volatile.Read(ref requestorDisconnectedCount) != 0)
                {
                    throw new InvalidOperationException("RequestorDisconnected should not fire for a successful observable HTTP/1.1 server-sent events response.");
                }

                if (Volatile.Read(ref responseStartingCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseStarting to fire exactly once for a successful observable HTTP/1.1 server-sent events response.");
                }

                if (Volatile.Read(ref responseSentCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseSent to fire exactly once for a successful observable HTTP/1.1 server-sent events response.");
                }

                if (Volatile.Read(ref responseCompletedCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseCompleted to fire exactly once for a successful observable HTTP/1.1 server-sent events response.");
                }

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/1.1 server-sent events disconnect on the final event emits abort/disconnect telemetry without false success signals.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ServerSentEventsDisconnectDuringFinalEventAsync()
        {
            const string requestPath = "/test/sse-disconnect-final";
            string finalEventData = new string('z', 512 * 1024);

            TaskCompletionSource<bool> firstSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> finalSendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)> postRoutingStateSource = new TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestAbortedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestorDisconnectedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int responseStartingCount = 0;
            int responseSentCount = 0;
            int responseCompletedCount = 0;
            int requestAbortedCount = 0;
            int requestorDisconnectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);

                server.Routes.PostRouting = async (HttpContextBase context) =>
                {
                    if (String.Equals(context.Request.Url.RawWithoutQuery, requestPath, StringComparison.Ordinal))
                    {
                        postRoutingStateSource.TrySetResult((context.RequestAborted, context.Token.IsCancellationRequested));
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                };

                server.Events.RequestAborted += (sender, args) =>
                {
                    Interlocked.Increment(ref requestAbortedCount);
                    requestAbortedObserved.TrySetResult(true);
                };

                server.Events.RequestorDisconnected += (sender, args) =>
                {
                    Interlocked.Increment(ref requestorDisconnectedCount);
                    requestorDisconnectedObserved.TrySetResult(true);
                };

                server.Events.ResponseStarting += (sender, args) =>
                {
                    Interlocked.Increment(ref responseStartingCount);
                };

                server.Events.ResponseSent += (sender, args) =>
                {
                    Interlocked.Increment(ref responseSentCount);
                };

                server.Events.ResponseCompleted += (sender, args) =>
                {
                    Interlocked.Increment(ref responseCompletedCount);
                };

                server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, requestPath, async (HttpContextBase context) =>
                {
                    context.Response.ServerSentEvents = true;
                    context.Response.StatusCode = 200;

                    ServerSentEvent firstEvent = new ServerSentEvent();
                    firstEvent.Id = "1";
                    firstEvent.Data = "disconnect event one";
                    bool firstSendResult = await context.Response.SendEvent(firstEvent, false, context.Token).ConfigureAwait(false);
                    firstSendResultSource.TrySetResult(firstSendResult);

                    await Task.Delay(200).ConfigureAwait(false);

                    ServerSentEvent finalEvent = new ServerSentEvent();
                    finalEvent.Id = "2";
                    finalEvent.Data = finalEventData;
                    bool finalSendResult = await context.Response.SendEvent(finalEvent, true, context.Token).ConfigureAwait(false);
                    finalSendResultSource.TrySetResult(finalSendResult);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);
                    client.NoDelay = true;

                    NetworkStream stream = client.GetStream();
                    byte[] requestBytes = Encoding.ASCII.GetBytes(
                        "GET " + requestPath + " HTTP/1.1\r\n" +
                        "Host: 127.0.0.1\r\n" +
                        "Connection: close\r\n" +
                        "\r\n");

                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    string partialResponse = await ReadUntilContainsAsync(stream, "data: disconnect event one", TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if (!partialResponse.Contains("data: disconnect event one", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Expected to receive the first server-sent event before aborting the client transport.");
                    }

                    client.Client.LingerState = new LingerOption(true, 0);
                }

                bool firstSendResult = await WaitForTaskAsync(firstSendResultSource.Task, 10000, "Server-sent events disconnect scenario did not complete the first send.").ConfigureAwait(false);
                bool finalSendResult = await WaitForTaskAsync(finalSendResultSource.Task, 10000, "Server-sent events disconnect scenario did not complete the final send.").ConfigureAwait(false);
                (bool RequestAborted, bool TokenCancelled) postRoutingState = await WaitForTaskAsync(postRoutingStateSource.Task, 10000, "Server-sent events disconnect scenario did not reach PostRouting.").ConfigureAwait(false);

                if (!firstSendResult)
                {
                    throw new InvalidOperationException("Expected the first server-sent event send to succeed before the client disconnected.");
                }

                if (finalSendResult)
                {
                    throw new InvalidOperationException("Expected the final server-sent event send to fail after the client reset the socket.");
                }

                if (!postRoutingState.RequestAborted || !postRoutingState.TokenCancelled)
                {
                    throw new InvalidOperationException("Expected the server-sent events disconnect request to be marked aborted and have its request token cancelled before PostRouting.");
                }

                await WaitForTaskAsync(requestAbortedObserved.Task, 10000, "Server-sent events disconnect scenario did not emit RequestAborted after the final event send failed.").ConfigureAwait(false);
                await WaitForTaskAsync(requestorDisconnectedObserved.Task, 10000, "Server-sent events disconnect scenario did not emit RequestorDisconnected after the final event send failed.").ConfigureAwait(false);

                if (Volatile.Read(ref requestAbortedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestAborted to fire exactly once for the server-sent events disconnect response.");
                }

                if (Volatile.Read(ref requestorDisconnectedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestorDisconnected to fire exactly once for the server-sent events disconnect response.");
                }

                if (Volatile.Read(ref responseStartingCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseStarting to fire exactly once for the server-sent events disconnect response.");
                }

                if (Volatile.Read(ref responseSentCount) != 0)
                {
                    throw new InvalidOperationException("ResponseSent should not fire when the client disconnects before the final server-sent event is written.");
                }

                if (Volatile.Read(ref responseCompletedCount) != 0)
                {
                    throw new InvalidOperationException("ResponseCompleted should not fire when the client disconnects before the final server-sent event is written.");
                }

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Verify a client disconnect during a large HTTP/1.1 response is classified as an abort/disconnect without emitting a false success signal.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DisconnectDuringLargeResponseAsync()
        {
            const string requestPath = "/test/disconnect-large";
            const int responseLength = 512 * 1024;

            TaskCompletionSource<bool> sendResultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)> postRoutingStateSource = new TaskCompletionSource<(bool RequestAborted, bool TokenCancelled)>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestAbortedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> requestorDisconnectedObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int responseStartingCount = 0;
            int responseSentCount = 0;
            int responseCompletedCount = 0;
            int requestAbortedCount = 0;
            int requestorDisconnectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);

                server.Routes.PostRouting = async (HttpContextBase context) =>
                {
                    if (String.Equals(context.Request.Url.RawWithoutQuery, requestPath, StringComparison.Ordinal))
                    {
                        postRoutingStateSource.TrySetResult((context.RequestAborted, context.Token.IsCancellationRequested));
                    }

                    await Task.CompletedTask.ConfigureAwait(false);
                };

                server.Events.RequestAborted += (sender, args) =>
                {
                    Interlocked.Increment(ref requestAbortedCount);
                    requestAbortedObserved.TrySetResult(true);
                };

                server.Events.RequestorDisconnected += (sender, args) =>
                {
                    Interlocked.Increment(ref requestorDisconnectedCount);
                    requestorDisconnectedObserved.TrySetResult(true);
                };

                server.Events.ResponseStarting += (sender, args) =>
                {
                    Interlocked.Increment(ref responseStartingCount);
                };

                server.Events.ResponseSent += (sender, args) =>
                {
                    Interlocked.Increment(ref responseSentCount);
                };

                server.Events.ResponseCompleted += (sender, args) =>
                {
                    Interlocked.Increment(ref responseCompletedCount);
                };

                server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, requestPath, async (HttpContextBase context) =>
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/octet-stream";

                    using (Stream payload = new ThrottledPayloadStream(responseLength, 8192, 25))
                    {
                        bool sendResult = await context.Response.Send(responseLength, payload, context.Token).ConfigureAwait(false);
                        sendResultSource.TrySetResult(sendResult);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);
                    client.NoDelay = true;

                    NetworkStream stream = client.GetStream();
                    byte[] requestBytes = Encoding.ASCII.GetBytes(
                        "GET " + requestPath + " HTTP/1.1\r\n" +
                        "Host: 127.0.0.1\r\n" +
                        "Connection: close\r\n" +
                        "\r\n");

                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    int bytesRead = await ReadAtLeastAsync(stream, 4096, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                    if (bytesRead < 4096)
                    {
                        throw new InvalidOperationException("Expected to receive a partial HTTP/1.1 response before aborting the client transport.");
                    }

                    client.Client.LingerState = new LingerOption(true, 0);
                }

                bool sendResult = await WaitForTaskAsync(sendResultSource.Task, 10000, "Large-response disconnect scenario did not complete the server send path.").ConfigureAwait(false);
                (bool RequestAborted, bool TokenCancelled) postRoutingState = await WaitForTaskAsync(postRoutingStateSource.Task, 10000, "Disconnect scenario did not reach PostRouting with the final context state.").ConfigureAwait(false);
                await WaitForTaskAsync(requestAbortedObserved.Task, 10000, "Disconnect scenario did not emit RequestAborted.").ConfigureAwait(false);
                await WaitForTaskAsync(requestorDisconnectedObserved.Task, 10000, "Disconnect scenario did not emit RequestorDisconnected.").ConfigureAwait(false);

                if (sendResult)
                {
                    throw new InvalidOperationException("Expected the large HTTP/1.1 response send to fail after the client reset the socket.");
                }

                if (!postRoutingState.RequestAborted || !postRoutingState.TokenCancelled)
                {
                    throw new InvalidOperationException("Expected the disconnected request context to be marked RequestAborted and have its request token cancelled before PostRouting.");
                }

                if (Volatile.Read(ref requestAbortedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestAborted to fire exactly once for the disconnected response.");
                }

                if (Volatile.Read(ref requestorDisconnectedCount) != 1)
                {
                    throw new InvalidOperationException("Expected RequestorDisconnected to fire exactly once for the disconnected response.");
                }

                if (Volatile.Read(ref responseStartingCount) != 1)
                {
                    throw new InvalidOperationException("Expected ResponseStarting to fire exactly once for the partially-sent response.");
                }

                if (Volatile.Read(ref responseSentCount) != 0)
                {
                    throw new InvalidOperationException("ResponseSent should not fire when the client disconnects before the response finishes sending.");
                }

                if (Volatile.Read(ref responseCompletedCount) != 0)
                {
                    throw new InvalidOperationException("ResponseCompleted should not fire when the client disconnects before the response finishes sending.");
                }

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Verify a route that attempts to send twice still returns the first response without crashing the server.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11DoubleSendResponseAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/test/double-send")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode || !String.Equals(body, "First response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 double-send response behavior.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an exception thrown from a route handler produces an HTTP 500 response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ExceptionInRouteHandlerAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/error/test")).ConfigureAwait(false);

                    if (response.StatusCode != System.Net.HttpStatusCode.InternalServerError)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 exception route to return 500.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a custom exception route can override the default response shape when a route throws.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11CustomExceptionRouteSendsResponseAsync()
        {
            await TestExceptionRouteScenarioAsync(
                "/error/test",
                server =>
                {
                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        context.Response.StatusCode = 503;
                        context.Response.ContentType = WebserverConstants.ContentTypeJson;
                        await context.Response.Send("{\"error\":\"custom-exception-route\"}", context.Token).ConfigureAwait(false);
                    };
                },
                503,
                WebserverConstants.ContentTypeJson,
                "custom-exception-route",
                false).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify Watson falls back to the stock HTML 500 page when a custom exception route returns without sending.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11CustomExceptionRouteNoSendFallsBackToDefault500Async()
        {
            await TestExceptionRouteScenarioAsync(
                "/error/test",
                server =>
                {
                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        await Task.CompletedTask.ConfigureAwait(false);
                    };
                },
                500,
                WebserverConstants.ContentTypeHtml,
                Default500BodyFragment,
                true).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify Watson falls back to the stock HTML 500 page when a custom exception route throws.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11CustomExceptionRouteThrowFallsBackToDefault500Async()
        {
            await TestExceptionRouteScenarioAsync(
                "/error/test",
                server =>
                {
                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        await Task.Yield();
                        throw new InvalidOperationException("Exception route failure.");
                    };
                },
                500,
                WebserverConstants.ContentTypeHtml,
                Default500BodyFragment,
                true).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify exceptions from <c>PreRouting</c> flow through the configured exception route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11PreRoutingExceptionUsesCustomExceptionRouteAsync()
        {
            await TestExceptionRouteScenarioAsync(
                "/throw/prerouting",
                server =>
                {
                    server.Routes.PreRouting = async (HttpContextBase context) =>
                    {
                        if (String.Equals(context.Request.Url.RawWithoutQuery, "/throw/prerouting", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("PreRouting failure.");
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    };

                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        context.Response.StatusCode = 502;
                        context.Response.ContentType = WebserverConstants.ContentTypeJson;
                        await context.Response.Send("{\"phase\":\"prerouting\"}", context.Token).ConfigureAwait(false);
                    };
                },
                502,
                WebserverConstants.ContentTypeJson,
                "prerouting",
                false).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify exceptions from <c>AuthenticateRequest</c> flow through the configured exception route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11AuthenticateRequestExceptionUsesCustomExceptionRouteAsync()
        {
            await TestExceptionRouteScenarioAsync(
                "/throw/authenticate-request",
                server =>
                {
                    server.Routes.AuthenticateRequest = async (HttpContextBase context) =>
                    {
                        if (String.Equals(context.Request.Url.RawWithoutQuery, "/throw/authenticate-request", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("AuthenticateRequest failure.");
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    };

                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        context.Response.StatusCode = 504;
                        context.Response.ContentType = WebserverConstants.ContentTypeJson;
                        await context.Response.Send("{\"phase\":\"authenticate-request\"}", context.Token).ConfigureAwait(false);
                    };
                },
                504,
                WebserverConstants.ContentTypeJson,
                "authenticate-request",
                false).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify exceptions from <c>AuthenticateApiRequest</c> flow through the configured exception route.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11AuthenticateApiRequestExceptionUsesCustomExceptionRouteAsync()
        {
            await TestExceptionRouteScenarioAsync(
                "/throw/authenticate-api-request",
                server =>
                {
                    server.Routes.AuthenticateApiRequest = (HttpContextBase context) =>
                    {
                        if (String.Equals(context.Request.Url.RawWithoutQuery, "/throw/authenticate-api-request", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("AuthenticateApiRequest failure.");
                        }

                        return Task.FromResult(new AuthResult
                        {
                            AuthenticationResult = AuthenticationResultEnum.Success,
                            AuthorizationResult = AuthorizationResultEnum.Permitted
                        });
                    };

                    server.Routes.Exception = async (HttpContextBase context, Exception exception) =>
                    {
                        context.Response.StatusCode = 505;
                        context.Response.ContentType = WebserverConstants.ContentTypeJson;
                        await context.Response.Send("{\"phase\":\"authenticate-api-request\"}", context.Token).ConfigureAwait(false);
                    };
                },
                505,
                WebserverConstants.ContentTypeJson,
                "authenticate-api-request",
                false).ConfigureAwait(false);
        }

        /// <summary>
        /// Verify an empty HTTP/1.1 POST body is handled without failing.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11EmptyPostBodyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (ByteArrayContent content = new ByteArrayContent(Array.Empty<byte>()))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo"), content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 empty POST body to succeed.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/1.1 OPTIONS preflight request succeeds and emits CORS headers.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11OptionsPreflightAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod("OPTIONS"), new Uri(host.BaseAddress, "/hello")))
                {
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 OPTIONS preflight request to succeed.");
                    }

                    if (!response.Headers.Contains("Access-Control-Allow-Origin")
                        || !response.Headers.Contains("Access-Control-Allow-Methods")
                        || !response.Headers.Contains("Access-Control-Allow-Headers"))
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 OPTIONS preflight response to include CORS headers.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a request with many headers is handled and echoed correctly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11RequestWithManyHeadersAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/test/header-echo")))
                {
                    for (int i = 0; i < 50; i++)
                    {
                        request.Headers.TryAddWithoutValidation("X-Custom-Header-" + i.ToString(), "value-" + i.ToString());
                    }

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode
                        || !body.Contains("X-Custom-Header-0: value-0", StringComparison.Ordinal)
                        || !body.Contains("X-Custom-Header-49: value-49", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 many-headers response behavior.");
                    }
                }
            }
        }

        private static void ConfigureBasicRoutes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/get", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("GET response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/post", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("POST response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/echo-body", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/echo", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.PUT, "/test/put", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("PUT response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.PUT, "/test/echo-body-put", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.DELETE, "/test/delete", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("DELETE response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Parameter.Add(CoreHttpMethod.GET, "/users/{id}", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("User " + context.Request.Url.Parameters["id"], context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/query", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Query " + context.Request.RetrieveQueryValue("name"), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/static/test", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Static route response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/hello", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send("Hello world", context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/header-echo", async (HttpContextBase context) =>
            {
                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < context.Request.Headers.Count; i++)
                {
                    string key = context.Request.Headers.GetKey(i);
                    string[] values = context.Request.Headers.GetValues(i);

                    if (values == null) continue;

                    for (int j = 0; j < values.Length; j++)
                    {
                        builder.Append(key);
                        builder.Append(": ");
                        builder.Append(values[j]);
                        builder.Append('\n');
                    }
                }

                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(builder.ToString(), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/octet-stream";
                await context.Response.Send(context.Request.DataAsBytes, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo-string", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo-async", async (HttpContextBase context) =>
            {
                byte[] body = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(body), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-manual", async (HttpContextBase context) =>
            {
                byte[] body = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(body), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/chunked", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ChunkedTransfer = true;

                for (int i = 1; i <= 5; i++)
                {
                    byte[] chunk = Encoding.UTF8.GetBytes("Chunk " + i.ToString() + "\n");
                    bool isFinal = i == 5;
                    await context.Response.SendChunk(chunk, isFinal, context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/chunked-edge", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ChunkedTransfer = true;
                await context.Response.SendChunk(Array.Empty<byte>(), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes("single-byte\n"), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes(new string('x', 1024) + "\nlarge-chunk"), true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/sse", async (HttpContextBase context) =>
            {
                context.Response.ServerSentEvents = true;
                context.Response.StatusCode = 200;

                for (int i = 1; i <= 5; i++)
                {
                    bool isFinal = i == 5;
                    ServerSentEvent sse = new ServerSentEvent();
                    sse.Id = i.ToString();
                    sse.Data = "Event " + i.ToString();
                    await context.Response.SendEvent(sse, isFinal, context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/sse-edge", async (HttpContextBase context) =>
            {
                context.Response.ServerSentEvents = true;
                context.Response.StatusCode = 200;

                ServerSentEvent multiLineEvent = new ServerSentEvent();
                multiLineEvent.Data = "Line1\nLine2\nLine3";
                await context.Response.SendEvent(multiLineEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent specialCharactersEvent = new ServerSentEvent();
                specialCharactersEvent.Data = "Special: <>&\"'";
                await context.Response.SendEvent(specialCharactersEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent unicodeEvent = new ServerSentEvent();
                unicodeEvent.Data = "Unicode: 世界";
                await context.Response.SendEvent(unicodeEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent finalEvent = new ServerSentEvent();
                finalEvent.Data = "done";
                await context.Response.SendEvent(finalEvent, true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/test/double-send", async (HttpContextBase context) =>
            {
                await context.Response.Send("First response", context.Token).ConfigureAwait(false);

                try
                {
                    await context.Response.Send("Second response", context.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/error/test", async (HttpContextBase context) =>
            {
                throw new Exception("Test exception");
            });

            server.Routes.Preflight = async (HttpContextBase context) =>
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                context.Response.StatusCode = 200;
                await context.Response.Send(String.Empty, context.Token).ConfigureAwait(false);
            };
        }

        private static HttpClient CreateHttpClient(Version version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }

        private static async Task TestExceptionRouteScenarioAsync(
            string relativePath,
            Action<Webserver> configureScenario,
            int expectedStatusCode,
            string expectedMediaType,
            string expectedBodyFragment,
            bool expectDefault500Body)
        {
            if (String.IsNullOrEmpty(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            if (configureScenario == null) throw new ArgumentNullException(nameof(configureScenario));
            if (String.IsNullOrEmpty(expectedMediaType)) throw new ArgumentNullException(nameof(expectedMediaType));
            if (String.IsNullOrEmpty(expectedBodyFragment)) throw new ArgumentNullException(nameof(expectedBodyFragment));

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                ConfigureBasicRoutes(server);
                configureScenario(server);
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    await AssertExceptionResponseAsync(
                        client,
                        host.BaseAddress,
                        relativePath,
                        expectedStatusCode,
                        expectedMediaType,
                        expectedBodyFragment,
                        expectDefault500Body).ConfigureAwait(false);

                    await AssertServerHealthyAsync(client, host.BaseAddress).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestChunkedRequestBodyAsync(string relativePath, string body, string contentType)
        {
            if (String.IsNullOrEmpty(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            if (body == null) throw new ArgumentNullException(nameof(body));
            if (String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, new Uri(host.BaseAddress, relativePath)))
                {
                    request.Content = new StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(body)));
                    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                    request.Headers.TransferEncodingChunked = true;

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string echoed = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 chunked request body to succeed.");
                    }

                    if (!String.Equals(echoed, body, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 chunked request body response.");
                    }
                }
            }
        }

        private static async Task AssertExceptionResponseAsync(
            HttpClient client,
            Uri baseAddress,
            string relativePath,
            int expectedStatusCode,
            string expectedMediaType,
            string expectedBodyFragment,
            bool expectDefault500Body)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));
            if (String.IsNullOrEmpty(relativePath)) throw new ArgumentNullException(nameof(relativePath));
            if (String.IsNullOrEmpty(expectedMediaType)) throw new ArgumentNullException(nameof(expectedMediaType));
            if (String.IsNullOrEmpty(expectedBodyFragment)) throw new ArgumentNullException(nameof(expectedBodyFragment));

            HttpResponseMessage response = await client.GetAsync(new Uri(baseAddress, relativePath)).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            string mediaType = response.Content.Headers.ContentType?.MediaType;
            bool hasDefault500Body = body.Contains(Default500BodyFragment, StringComparison.Ordinal);

            if ((int)response.StatusCode != expectedStatusCode)
            {
                throw new InvalidOperationException(
                    "Expected exception-route scenario to return "
                    + expectedStatusCode.ToString()
                    + ", got "
                    + ((int)response.StatusCode).ToString()
                    + ".");
            }

            if (!String.Equals(mediaType, expectedMediaType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Expected exception-route scenario content type "
                    + expectedMediaType
                    + ", got "
                    + (mediaType ?? "<null>")
                    + ".");
            }

            if (!body.Contains(expectedBodyFragment, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected exception-route scenario response body fragment was not present.");
            }

            if (expectDefault500Body != hasDefault500Body)
            {
                throw new InvalidOperationException("Unexpected stock Watson 500 page body behavior.");
            }
        }

        /// <summary>
        /// Verify that a PUT request with Expect: 100-continue succeeds and the body is received.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11ExpectContinueAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Put, new Uri(host.BaseAddress, "/test/echo-body-put")))
                    {
                        request.Headers.ExpectContinue = true;
                        request.Content = new StringContent("expect-continue-body", Encoding.UTF8, "text/plain");

                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected HTTP/1.1 Expect: 100-continue PUT request to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, "expect-continue-body", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected Expect: 100-continue response body: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify that a PUT request with x-amz-content-sha256 containing streaming and Content-Length is accepted.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp11AwsChunkedContentEncodingNotRejectedAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Put, new Uri(host.BaseAddress, "/test/echo-body-put")))
                    {
                        request.Content = new StringContent("aws-chunked-body", Encoding.UTF8, "text/plain");
                        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", "STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER");
                        request.Headers.TryAddWithoutValidation("x-amz-decoded-content-length", "16");

                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException("Expected PUT with x-amz-content-sha256 streaming header and Content-Length to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, "aws-chunked-body", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected response body for aws-chunked request: " + body);
                        }
                    }
                }
            }
        }

        private static async Task AssertServerHealthyAsync(HttpClient client, Uri baseAddress)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (baseAddress == null) throw new ArgumentNullException(nameof(baseAddress));

            HttpResponseMessage response = await client.GetAsync(new Uri(baseAddress, "/test/get")).ConfigureAwait(false);
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode || !String.Equals(body, "GET response", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Server did not remain healthy for the next request.");
            }
        }

        private static async Task<T> WaitForTaskAsync<T>(Task<T> task, int timeoutMs, string timeoutMessage)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (timeoutMs < 1) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            if (String.IsNullOrEmpty(timeoutMessage)) throw new ArgumentNullException(nameof(timeoutMessage));

            Task completedTask = await Task.WhenAny(task, Task.Delay(timeoutMs)).ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, task))
            {
                throw new InvalidOperationException(timeoutMessage);
            }

            return await task.ConfigureAwait(false);
        }

        private static async Task<int> ReadAtLeastAsync(NetworkStream stream, int minimumBytes, TimeSpan timeout)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (minimumBytes < 1) throw new ArgumentOutOfRangeException(nameof(minimumBytes));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            byte[] buffer = new byte[Math.Min(8192, minimumBytes)];
            int totalRead = 0;

            using (CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout))
            {
                while (totalRead < minimumBytes)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, timeoutSource.Token).ConfigureAwait(false);
                    if (bytesRead < 1)
                    {
                        break;
                    }

                    totalRead += bytesRead;
                }
            }

            return totalRead;
        }

        private static async Task<string> ReadUntilContainsAsync(NetworkStream stream, string expectedFragment, TimeSpan timeout)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (String.IsNullOrEmpty(expectedFragment)) throw new ArgumentNullException(nameof(expectedFragment));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            byte[] buffer = new byte[1024];
            using (MemoryStream capture = new MemoryStream())
            using (CancellationTokenSource timeoutSource = new CancellationTokenSource(timeout))
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, timeoutSource.Token).ConfigureAwait(false);
                    if (bytesRead < 1)
                    {
                        break;
                    }

                    capture.Write(buffer, 0, bytesRead);
                    string currentText = Encoding.UTF8.GetString(capture.GetBuffer(), 0, checked((int)capture.Length));
                    if (currentText.Contains(expectedFragment, StringComparison.Ordinal))
                    {
                        return currentText;
                    }
                }

                return Encoding.UTF8.GetString(capture.ToArray());
            }
        }

        private static async Task TestBodyEchoAsync(string body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBasicRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                using (StringContent content = new StringContent(body, Encoding.UTF8, "text/plain"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/test/echo"), content).ConfigureAwait(false);
                    string echoed = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new InvalidOperationException("Expected HTTP/1.1 echo request to succeed.");
                    }

                    if (!String.Equals(echoed, body, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/1.1 echo response body.");
                    }
                }
            }
        }

        private sealed class ThrottledPayloadStream : Stream
        {
            private readonly long _Length;
            private readonly int _MaxChunkSize;
            private readonly int _DelayPerReadMs;
            private long _Position = 0;

            public ThrottledPayloadStream(long length, int maxChunkSize, int delayPerReadMs)
            {
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
                if (maxChunkSize < 1) throw new ArgumentOutOfRangeException(nameof(maxChunkSize));
                if (delayPerReadMs < 0) throw new ArgumentOutOfRangeException(nameof(delayPerReadMs));

                _Length = length;
                _MaxChunkSize = maxChunkSize;
                _DelayPerReadMs = delayPerReadMs;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _Length;

            public override long Position
            {
                get
                {
                    return _Position;
                }
                set
                {
                    throw new NotSupportedException();
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return ReadCore(buffer, offset, count);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_DelayPerReadMs > 0)
                {
                    await Task.Delay(_DelayPerReadMs, cancellationToken).ConfigureAwait(false);
                }

                return ReadCore(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            private int ReadCore(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
                if ((buffer.Length - offset) < count) throw new ArgumentException("The supplied buffer is too small.");
                if (_Position >= _Length) return 0;

                int bytesToRead = (int)Math.Min(Math.Min(count, _MaxChunkSize), _Length - _Position);
                for (int i = 0; i < bytesToRead; i++)
                {
                    buffer[offset + i] = (byte)('a' + (int)((_Position + i) % 26));
                }

                _Position += bytesToRead;
                return bytesToRead;
            }
        }
    }
}
