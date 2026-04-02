namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared tests verifying that Request.Data stream properly returns EOF
    /// after ContentLength bytes have been read, so the standard
    /// <c>while ((bytesRead = stream.Read(...)) > 0)</c> pattern works.
    /// </summary>
    public static class SharedDataStreamTests
    {
        /// <summary>
        /// Verify reading Request.Data with a while-loop returns EOF after ContentLength bytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataStreamReadReturnsEofAsync()
        {
            string payload = "Hello from Data stream!";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/stream-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected Data stream echo to succeed, got " + (int)response.StatusCode + ": " + body);
                        }

                        if (!String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Data stream echo mismatch. Expected: " + payload + ", Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify reading Request.Data asynchronously with ReadAsync returns EOF after ContentLength bytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataStreamReadAsyncReturnsEofAsync()
        {
            string payload = "Async Data stream test!";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/stream-echo-async"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected async Data stream echo to succeed, got " + (int)response.StatusCode + ": " + body);
                        }

                        if (!String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Async Data stream echo mismatch. Expected: " + payload + ", Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify a large body can be read from Request.Data stream without hanging.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataStreamLargeBodyAsync()
        {
            byte[] payload = new byte[128 * 1024];
            Random random = new Random(12345);
            random.NextBytes(payload);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    using (ByteArrayContent content = new ByteArrayContent(payload))
                    {
                        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/stream-echo-binary"),
                            content).ConfigureAwait(false);

                        byte[] echoed = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected large Data stream echo to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (echoed.Length != payload.Length)
                        {
                            throw new InvalidOperationException(
                                "Large Data stream echo length mismatch. Expected: " + payload.Length + ", Got: " + echoed.Length);
                        }

                        for (int i = 0; i < payload.Length; i++)
                        {
                            if (echoed[i] != payload[i])
                            {
                                throw new InvalidOperationException(
                                    "Large Data stream echo content mismatch at offset " + i + ".");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify that DataAsBytes still works correctly after the stream wrapping change.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataAsBytesStillWorksAsync()
        {
            string payload = "DataAsBytes still works!";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/databytes-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected DataAsBytes echo to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "DataAsBytes echo mismatch. Expected: " + payload + ", Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify that an empty POST body does not hang when reading from the Data stream.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataStreamEmptyBodyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(String.Empty, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/stream-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected empty body Data stream echo to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, String.Empty, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Empty body Data stream echo mismatch. Expected empty, Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify multiple sequential stream-read requests on a keep-alive connection succeed.
        /// This ensures ContentLengthStream does not corrupt the underlying connection stream
        /// between requests.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataStreamKeepAliveMultipleRequestsAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    for (int i = 0; i < 5; i++)
                    {
                        string payload = "Keep-alive request " + i.ToString();

                        using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                        {
                            HttpResponseMessage response = await client.PostAsync(
                                new Uri(host.BaseAddress, "/stream-echo"),
                                content).ConfigureAwait(false);

                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                            if (!response.IsSuccessStatusCode)
                            {
                                throw new InvalidOperationException(
                                    "Keep-alive request " + i + " failed with " + (int)response.StatusCode + ".");
                            }

                            if (!String.Equals(body, payload, StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException(
                                    "Keep-alive request " + i + " echo mismatch. Expected: " + payload + ", Got: " + body);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify ReadBodyAsync works correctly through ContentLengthStream.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestReadBodyAsyncThroughContentLengthStreamAsync()
        {
            string payload = "ReadBodyAsync through ContentLengthStream";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/readbody-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected ReadBodyAsync echo to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "ReadBodyAsync echo mismatch. Expected: " + payload + ", Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify DataAsString works correctly through ContentLengthStream.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestDataAsStringThroughContentLengthStreamAsync()
        {
            string payload = "DataAsString through ContentLengthStream";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/datastring-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                        {
                            throw new InvalidOperationException(
                                "Expected DataAsString echo to succeed, got " + (int)response.StatusCode + ".");
                        }

                        if (!String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "DataAsString echo mismatch. Expected: " + payload + ", Got: " + body);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Verify WebSocket upgrade works on a server that also has stream-reading body routes.
        /// This validates that the ContentLengthStream unwrap path in ProcessWebSocketRouteAsync
        /// correctly recovers the writable inner stream.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketUpgradeWithContentLengthStreamAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    Uri wsUri = new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws-echo");
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        await ws.ConnectAsync(wsUri, cts.Token).ConfigureAwait(false);

                        string message = "WebSocket test message";
                        byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                        await ws.SendAsync(
                            new ArraySegment<byte>(sendBuffer),
                            WebSocketMessageType.Text,
                            true,
                            cts.Token).ConfigureAwait(false);

                        byte[] receiveBuffer = new byte[4096];
                        WebSocketReceiveResult result = await ws.ReceiveAsync(
                            new ArraySegment<byte>(receiveBuffer),
                            cts.Token).ConfigureAwait(false);

                        string received = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

                        if (!String.Equals(received, message, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "WebSocket echo mismatch. Expected: " + message + ", Got: " + received);
                        }

                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Verify a POST with stream body read followed by a WebSocket upgrade on the same server
        /// both succeed. This exercises the full lifecycle of ContentLengthStream wrapping and unwrapping.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttpBodyThenWebSocketOnSameServerAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    string payload = "body before websocket";

                    using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                    {
                        HttpResponseMessage response = await client.PostAsync(
                            new Uri(host.BaseAddress, "/stream-echo"),
                            content).ConfigureAwait(false);

                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode || !String.Equals(body, payload, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "HTTP body request before WebSocket failed.");
                        }
                    }
                }

                using (ClientWebSocket ws = new ClientWebSocket())
                {
                    Uri wsUri = new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws-echo");
                    using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        await ws.ConnectAsync(wsUri, cts.Token).ConfigureAwait(false);

                        string message = "ws after http";
                        byte[] sendBuffer = Encoding.UTF8.GetBytes(message);
                        await ws.SendAsync(
                            new ArraySegment<byte>(sendBuffer),
                            WebSocketMessageType.Text,
                            true,
                            cts.Token).ConfigureAwait(false);

                        byte[] receiveBuffer = new byte[4096];
                        WebSocketReceiveResult result = await ws.ReceiveAsync(
                            new ArraySegment<byte>(receiveBuffer),
                            cts.Token).ConfigureAwait(false);

                        string received = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);

                        if (!String.Equals(received, message, StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "WebSocket echo after HTTP body request failed. Expected: " + message + ", Got: " + received);
                        }

                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                    }
                }
            }
        }

        private static void ConfigureRoutes(Webserver server)
        {
            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/stream-echo", async (HttpContextBase context) =>
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while ((bytesRead = context.Request.Data.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    await context.Response.Send(Encoding.UTF8.GetString(ms.ToArray()), context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/stream-echo-async", async (HttpContextBase context) =>
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;

                    while ((bytesRead = await context.Request.Data.ReadAsync(buffer, 0, buffer.Length, context.Token).ConfigureAwait(false)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/plain";
                    await context.Response.Send(Encoding.UTF8.GetString(ms.ToArray()), context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/stream-echo-binary", async (HttpContextBase context) =>
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await context.Request.Data.ReadAsync(buffer, 0, buffer.Length, context.Token).ConfigureAwait(false)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/octet-stream";
                    await context.Response.Send(ms.ToArray(), context.Token).ConfigureAwait(false);
                }
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/databytes-echo", async (HttpContextBase context) =>
            {
                byte[] data = context.Request.DataAsBytes;
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(data ?? Array.Empty<byte>()), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/readbody-echo", async (HttpContextBase context) =>
            {
                byte[] data = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Encoding.UTF8.GetString(data ?? Array.Empty<byte>()), context.Token).ConfigureAwait(false);
            });

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/datastring-echo", async (HttpContextBase context) =>
            {
                string data = context.Request.DataAsString;
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(data ?? String.Empty, context.Token).ConfigureAwait(false);
            });

            server.Settings.WebSockets.Enable = true;
            server.WebSocket("/ws-echo", async (HttpContextBase context, WatsonWebserver.Core.WebSockets.WebSocketSession session) =>
            {
                WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(context.Token).ConfigureAwait(false);
                if (message != null && message.Text != null)
                {
                    await session.SendTextAsync(message.Text, context.Token).ConfigureAwait(false);
                }
            });
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = new Version(1, 1);
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }
    }
}
