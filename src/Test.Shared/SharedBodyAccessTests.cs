namespace Test.Shared
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Comprehensive body access tests across all protocols, HTTP methods, body access
    /// mechanisms, and transfer encodings.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class SharedBodyAccessTests
    {
        #region HTTP/1.1-Body-Access-Methods

        /// <summary>
        /// HTTP/1.1 POST body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PostDataStreamReadAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/stream-sync", "H1 POST stream read").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST body read via Data.ReadAsync (asynchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PostDataStreamReadAsyncAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/stream-async", "H1 POST stream async").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PostDataAsBytesAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/databytes", "H1 POST DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST body read via DataAsString property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PostDataAsStringAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/datastring", "H1 POST DataAsString").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PostReadBodyAsyncAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/readbodyasync", "H1 POST ReadBodyAsync").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PutDataStreamReadAsync()
        {
            await Http1BodyAccessAsync("PUT", "/body/stream-sync", "H1 PUT stream read").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PutDataAsBytesAsync()
        {
            await Http1BodyAccessAsync("PUT", "/body/databytes", "H1 PUT DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via DataAsString property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PutDataAsStringAsync()
        {
            await Http1BodyAccessAsync("PUT", "/body/datastring", "H1 PUT DataAsString").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PUT body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PutReadBodyAsyncAsync()
        {
            await Http1BodyAccessAsync("PUT", "/body/readbodyasync", "H1 PUT ReadBodyAsync").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PATCH body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PatchDataAsBytesAsync()
        {
            await Http1BodyAccessAsync("PATCH", "/body/databytes", "H1 PATCH DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 PATCH body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1PatchDataStreamReadAsync()
        {
            await Http1BodyAccessAsync("PATCH", "/body/stream-sync", "H1 PATCH stream read").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 DELETE with body read via DataAsBytes property.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1DeleteWithBodyDataAsBytesAsync()
        {
            await Http1BodyAccessAsync("DELETE", "/body/databytes", "H1 DELETE DataAsBytes").ConfigureAwait(false);
        }

        #endregion

        #region HTTP/1.1-Body-Sizes

        /// <summary>
        /// HTTP/1.1 POST empty body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1EmptyBodyStreamAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/stream-sync", String.Empty).ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST single-byte body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1SingleByteBodyStreamAsync()
        {
            await Http1BodyAccessAsync("POST", "/body/stream-sync", "X").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST 128KB body via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1LargeBodyStreamAsync()
        {
            await Http1BinaryBodyAccessAsync("POST", "/body/stream-async-raw", 128 * 1024).ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/1.1 POST 128KB body via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1LargeBodyDataAsBytesAsync()
        {
            await Http1BinaryBodyAccessAsync("POST", "/body/databytes-raw", 128 * 1024).ConfigureAwait(false);
        }

        #endregion

        #region HTTP/1.1-Keep-Alive

        /// <summary>
        /// HTTP/1.1 keep-alive: multiple POST requests with stream-read bodies on same connection.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1KeepAliveStreamReadsAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBodyRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttp11Client())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    for (int i = 0; i < 10; i++)
                    {
                        string payload = "keep-alive-stream-" + i.ToString();
                        using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                        {
                            HttpResponseMessage response = await client.PostAsync(
                                new Uri(host.BaseAddress, "/body/stream-sync"),
                                content).ConfigureAwait(false);

                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            AssertSuccess(response, body, payload, "Keep-alive stream read iteration " + i.ToString());
                        }
                    }
                }
            }
        }

        /// <summary>
        /// HTTP/1.1 keep-alive: alternating between stream read and DataAsBytes on same connection.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1KeepAliveAlternatingAccessAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBodyRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttp11Client())
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    string[] routes = new string[] { "/body/stream-sync", "/body/databytes", "/body/datastring", "/body/readbodyasync", "/body/stream-async" };

                    for (int i = 0; i < routes.Length * 2; i++)
                    {
                        string route = routes[i % routes.Length];
                        string payload = "alternating-" + i.ToString();
                        using (StringContent content = new StringContent(payload, Encoding.UTF8, "text/plain"))
                        {
                            HttpResponseMessage response = await client.PostAsync(
                                new Uri(host.BaseAddress, route),
                                content).ConfigureAwait(false);

                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            AssertSuccess(response, body, payload, "Keep-alive alternating route " + route + " iteration " + i.ToString());
                        }
                    }
                }
            }
        }

        #endregion

        #region HTTP/2-Body-Access-Methods

        /// <summary>
        /// HTTP/2 POST body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PostDataAsBytesAsync()
        {
            await Http2PostBodyAccessAsync("/body/databytes", "H2 POST DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST body read via DataAsString.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PostDataAsStringAsync()
        {
            await Http2PostBodyAccessAsync("/body/datastring", "H2 POST DataAsString").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST body read via ReadBodyAsync.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PostReadBodyAsyncAsync()
        {
            await Http2PostBodyAccessAsync("/body/readbodyasync", "H2 POST ReadBodyAsync").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST body read via Data.Read (synchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PostDataStreamReadAsync()
        {
            await Http2PostBodyAccessAsync("/body/stream-sync", "H2 POST stream sync").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST body read via Data.ReadAsync (asynchronous stream).
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PostDataStreamReadAsyncAsync()
        {
            await Http2PostBodyAccessAsync("/body/stream-async", "H2 POST stream async").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 PUT body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PutDataAsBytesAsync()
        {
            await Http2MethodBodyAccessAsync("PUT", "/body/databytes", "H2 PUT DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 PUT body read via DataAsString.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PutDataAsStringAsync()
        {
            await Http2MethodBodyAccessAsync("PUT", "/body/datastring", "H2 PUT DataAsString").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 PATCH body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PatchDataAsBytesAsync()
        {
            await Http2MethodBodyAccessAsync("PATCH", "/body/databytes", "H2 PATCH DataAsBytes").ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 DELETE with body read via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2DeleteWithBodyDataAsBytesAsync()
        {
            await Http2MethodBodyAccessAsync("DELETE", "/body/databytes", "H2 DELETE DataAsBytes").ConfigureAwait(false);
        }

        #endregion

        #region HTTP/2-Body-Sizes

        /// <summary>
        /// HTTP/2 POST empty body via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2EmptyBodyAsync()
        {
            await Http2PostBodyAccessAsync("/body/databytes", String.Empty).ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST large body (32KB, exceeds default frame size) via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2LargeBodyDataAsBytesAsync()
        {
            await Http2LargeBodyAccessAsync("/body/databytes-binary", 32 * 1024).ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST large body (32KB) via Data stream read.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2LargeBodyStreamReadAsync()
        {
            await Http2LargeBodyAccessAsync("/body/stream-async-binary", 32 * 1024).ConfigureAwait(false);
        }

        /// <summary>
        /// HTTP/2 POST body spanning multiple DATA frames via DataAsBytes.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2MultiFrameBodyAsync()
        {
            byte[] payload = new byte[12 * 1024];
            Random random = new Random(99);
            random.NextBytes(payload);

            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureBodyRoutes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    await PerformHttp2Handshake(stream).ConfigureAwait(false);

                    byte[] headerBytes = BuildHttp2PostHeaders(host.Port, "/body/databytes-binary", payload.Length);
                    Http2RawFrame headersFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = headerBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)Http2FrameFlags.EndHeaders,
                            StreamIdentifier = 1
                        },
                        headerBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(headersFrame)).ConfigureAwait(false);

                    int offset = 0;
                    int frameSize = 4096;
                    while (offset < payload.Length)
                    {
                        int chunkSize = Math.Min(frameSize, payload.Length - offset);
                        byte[] chunk = new byte[chunkSize];
                        Buffer.BlockCopy(payload, offset, chunk, 0, chunkSize);
                        offset += chunkSize;
                        bool isFinal = offset >= payload.Length;

                        Http2RawFrame dataFrame = new Http2RawFrame(
                            new Http2FrameHeader
                            {
                                Length = chunkSize,
                                Type = Http2FrameType.Data,
                                Flags = isFinal ? (byte)Http2FrameFlags.EndStreamOrAck : (byte)0,
                                StreamIdentifier = 1
                            },
                            chunk);

                        await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(dataFrame)).ConfigureAwait(false);
                    }

                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    byte[] echoed = Convert.FromBase64String(response.BodyString);

                    if (echoed.Length != payload.Length)
                    {
                        throw new InvalidOperationException("HTTP/2 multi-frame body length mismatch. Expected: " + payload.Length + ", Got: " + echoed.Length);
                    }

                    for (int i = 0; i < payload.Length; i++)
                    {
                        if (echoed[i] != payload[i])
                        {
                            throw new InvalidOperationException("HTTP/2 multi-frame body content mismatch at offset " + i + ".");
                        }
                    }
                }
            }
        }

        #endregion

        #region WebSocket-Body-Access

        /// <summary>
        /// WebSocket text message echo with small payload.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketTextEchoAsync()
        {
            await WebSocketTextRoundtripAsync("Hello WebSocket text!").ConfigureAwait(false);
        }

        /// <summary>
        /// WebSocket binary message echo with small payload.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketBinaryEchoAsync()
        {
            byte[] payload = new byte[] { 0, 1, 2, 127, 128, 254, 255 };
            await WebSocketBinaryRoundtripAsync(payload).ConfigureAwait(false);
        }

        /// <summary>
        /// WebSocket text message echo with 2KB payload.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketMediumTextAsync()
        {
            string payload = new string('A', 2048);
            await WebSocketTextRoundtripAsync(payload).ConfigureAwait(false);
        }

        /// <summary>
        /// WebSocket binary message echo with 3KB payload covering all byte values.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketMediumBinaryAsync()
        {
            byte[] payload = new byte[3072];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)(i % 256);
            }

            await WebSocketBinaryRoundtripAsync(payload).ConfigureAwait(false);
        }

        /// <summary>
        /// WebSocket fragmented text message assembly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketFragmentedTextAsync()
        {
            string[] fragments = new string[] { "frag-", "ment-", "ed-", "text" };
            string expected = "frag-ment-ed-text";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureWebSocketRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo-text"), cts.Token).ConfigureAwait(false);

                    for (int i = 0; i < fragments.Length; i++)
                    {
                        bool isLast = i == fragments.Length - 1;
                        byte[] data = Encoding.UTF8.GetBytes(fragments[i]);
                        await ws.SendAsync(
                            new ArraySegment<byte>(data),
                            WebSocketMessageType.Text,
                            isLast,
                            cts.Token).ConfigureAwait(false);
                    }

                    string received = await ReceiveTextAsync(ws, cts.Token).ConfigureAwait(false);
                    if (!String.Equals(received, expected, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("WebSocket fragmented text mismatch. Expected: " + expected + ", Got: " + received);
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// WebSocket fragmented binary message assembly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketFragmentedBinaryAsync()
        {
            byte[] full = new byte[512];
            Random random = new Random(77);
            random.NextBytes(full);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureWebSocketRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo-binary"), cts.Token).ConfigureAwait(false);

                    int fragmentSize = 128;
                    int offset = 0;
                    while (offset < full.Length)
                    {
                        int size = Math.Min(fragmentSize, full.Length - offset);
                        bool isLast = (offset + size) >= full.Length;
                        await ws.SendAsync(
                            new ArraySegment<byte>(full, offset, size),
                            WebSocketMessageType.Binary,
                            isLast,
                            cts.Token).ConfigureAwait(false);
                        offset += size;
                    }

                    byte[] received = await ReceiveBinaryAsync(ws, cts.Token).ConfigureAwait(false);
                    if (received.Length != full.Length)
                    {
                        throw new InvalidOperationException("WebSocket fragmented binary length mismatch. Expected: " + full.Length + ", Got: " + received.Length);
                    }

                    for (int i = 0; i < full.Length; i++)
                    {
                        if (received[i] != full[i])
                        {
                            throw new InvalidOperationException("WebSocket fragmented binary content mismatch at offset " + i + ".");
                        }
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// WebSocket interleaved text and binary messages on a single session.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestWebSocketInterleavedTextAndBinaryAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureWebSocketRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo-any"), cts.Token).ConfigureAwait(false);

                    string textPayload = "interleaved-text";
                    byte[] binaryPayload = new byte[] { 10, 20, 30, 40 };

                    await ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(textPayload)),
                        WebSocketMessageType.Text,
                        true,
                        cts.Token).ConfigureAwait(false);

                    string textReceived = await ReceiveTextAsync(ws, cts.Token).ConfigureAwait(false);
                    if (!String.Equals(textReceived, textPayload, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("WebSocket interleaved text mismatch.");
                    }

                    await ws.SendAsync(
                        new ArraySegment<byte>(binaryPayload),
                        WebSocketMessageType.Binary,
                        true,
                        cts.Token).ConfigureAwait(false);

                    byte[] binaryReceived = await ReceiveBinaryAsync(ws, cts.Token).ConfigureAwait(false);
                    if (binaryReceived.Length != binaryPayload.Length)
                    {
                        throw new InvalidOperationException("WebSocket interleaved binary length mismatch.");
                    }

                    for (int i = 0; i < binaryPayload.Length; i++)
                    {
                        if (binaryReceived[i] != binaryPayload[i])
                        {
                            throw new InvalidOperationException("WebSocket interleaved binary content mismatch at offset " + i + ".");
                        }
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region HTTP/1.1-Private-Helpers

        private static async Task Http1BodyAccessAsync(string method, string route, string payload)
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBodyRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttp11Client())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);

                    using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method), new Uri(host.BaseAddress, route)))
                    {
                        request.Content = new StringContent(payload, Encoding.UTF8, "text/plain");

                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        AssertSuccess(response, body, payload, "HTTP/1.1 " + method + " " + route);
                    }
                }
            }
        }

        private static async Task Http1BinaryBodyAccessAsync(string method, string route, int size)
        {
            byte[] payload = new byte[size];
            Random random = new Random(42);
            random.NextBytes(payload);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureBodyRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttp11Client())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);

                    using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method), new Uri(host.BaseAddress, route)))
                    {
                        request.Content = new ByteArrayContent(payload);
                        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

                        HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                        byte[] echoed = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        AssertBinarySuccess(response, echoed, payload, "HTTP/1.1 " + method + " " + route + " binary " + size);
                    }
                }
            }
        }

        private static HttpClient CreateHttp11Client()
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = new Version(1, 1);
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }

        #endregion

        #region HTTP/2-Private-Helpers

        private static async Task Http2PostBodyAccessAsync(string route, string payload)
        {
            await Http2MethodBodyAccessAsync("POST", route, payload).ConfigureAwait(false);
        }

        private static async Task Http2MethodBodyAccessAsync(string method, string route, string payload)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload);

            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureBodyRoutes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    await PerformHttp2Handshake(stream).ConfigureAwait(false);

                    byte[] headerBytes = BuildHttp2Headers(method, host.Port, route, bodyBytes.Length);

                    byte endHeadersAndStream = bodyBytes.Length == 0
                        ? (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck)
                        : (byte)Http2FrameFlags.EndHeaders;

                    Http2RawFrame headersFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = headerBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = endHeadersAndStream,
                            StreamIdentifier = 1
                        },
                        headerBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(headersFrame)).ConfigureAwait(false);

                    if (bodyBytes.Length > 0)
                    {
                        Http2RawFrame dataFrame = new Http2RawFrame(
                            new Http2FrameHeader
                            {
                                Length = bodyBytes.Length,
                                Type = Http2FrameType.Data,
                                Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                                StreamIdentifier = 1
                            },
                            bodyBytes);

                        await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(dataFrame)).ConfigureAwait(false);
                    }

                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200" || response.BodyString != payload)
                    {
                        throw new InvalidOperationException("HTTP/2 " + method + " " + route + " mismatch. Expected: " + payload + ", Got: " + response.BodyString);
                    }
                }
            }
        }

        private static async Task Http2LargeBodyAccessAsync(string route, int size)
        {
            byte[] payload = new byte[size];
            Random random = new Random(42);
            random.NextBytes(payload);

            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureBodyRoutes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    await PerformHttp2Handshake(stream).ConfigureAwait(false);

                    byte[] headerBytes = BuildHttp2PostHeaders(host.Port, route, payload.Length);
                    Http2RawFrame headersFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = headerBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)Http2FrameFlags.EndHeaders,
                            StreamIdentifier = 1
                        },
                        headerBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(headersFrame)).ConfigureAwait(false);

                    Http2RawFrame dataFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = payload.Length,
                            Type = Http2FrameType.Data,
                            Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                            StreamIdentifier = 1
                        },
                        payload);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(dataFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    byte[] echoed = Convert.FromBase64String(response.BodyString);
                    AssertBinaryMatch(echoed, payload, "HTTP/2 large body " + route + " " + size);
                }
            }
        }

        private static async Task PerformHttp2Handshake(NetworkStream stream)
        {
            Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
            Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
            if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
            {
                throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
            }
        }

        private static byte[] BuildHttp2PostHeaders(int port, string path, int contentLength)
        {
            return BuildHttp2Headers("POST", port, path, contentLength);
        }

        private static byte[] BuildHttp2Headers(string method, int port, string path, int contentLength)
        {
            List<HpackHeaderField> headers = new List<HpackHeaderField>();
            headers.Add(new HpackHeaderField { Name = ":method", Value = method });
            headers.Add(new HpackHeaderField { Name = ":scheme", Value = "http" });
            headers.Add(new HpackHeaderField { Name = ":authority", Value = "127.0.0.1:" + port.ToString() });
            headers.Add(new HpackHeaderField { Name = ":path", Value = path });
            if (contentLength >= 0)
            {
                headers.Add(new HpackHeaderField { Name = "content-length", Value = contentLength.ToString() });
            }

            return HpackCodec.Encode(headers);
        }

        #endregion

        #region WebSocket-Private-Helpers

        private static async Task WebSocketTextRoundtripAsync(string payload)
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureWebSocketRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo-text"), cts.Token).ConfigureAwait(false);

                    byte[] sendBuffer = Encoding.UTF8.GetBytes(payload);
                    await ws.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, cts.Token).ConfigureAwait(false);

                    string received = await ReceiveTextAsync(ws, cts.Token).ConfigureAwait(false);
                    if (!String.Equals(received, payload, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("WebSocket text echo mismatch. Expected: " + payload + ", Got: " + received);
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task WebSocketBinaryRoundtripAsync(byte[] payload)
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureWebSocketRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket ws = new ClientWebSocket())
                using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo-binary"), cts.Token).ConfigureAwait(false);

                    await ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, cts.Token).ConfigureAwait(false);

                    byte[] received = await ReceiveBinaryAsync(ws, cts.Token).ConfigureAwait(false);
                    if (received.Length != payload.Length)
                    {
                        throw new InvalidOperationException("WebSocket binary echo length mismatch. Expected: " + payload.Length + ", Got: " + received.Length);
                    }

                    for (int i = 0; i < payload.Length; i++)
                    {
                        if (received[i] != payload[i])
                        {
                            throw new InvalidOperationException("WebSocket binary echo content mismatch at offset " + i + ".");
                        }
                    }

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket client, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                int offset = 0;

                while (true)
                {
                    WebSocketReceiveResult result = await client.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token).ConfigureAwait(false);
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

        private static async Task<byte[]> ReceiveBinaryAsync(ClientWebSocket client, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                int offset = 0;

                while (true)
                {
                    WebSocketReceiveResult result = await client.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return Array.Empty<byte>();
                    }

                    offset += result.Count;
                    if (result.EndOfMessage)
                    {
                        byte[] ret = new byte[offset];
                        Buffer.BlockCopy(buffer, 0, ret, 0, offset);
                        return ret;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region Assertion-Helpers

        private static void AssertSuccess(HttpResponseMessage response, string actual, string expected, string context)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(context + " failed with status " + (int)response.StatusCode + ": " + actual);
            }

            if (!String.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(context + " body mismatch. Expected: " + expected + ", Got: " + actual);
            }
        }

        private static void AssertBinarySuccess(HttpResponseMessage response, byte[] actual, byte[] expected, string context)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(context + " failed with status " + (int)response.StatusCode + ".");
            }

            AssertBinaryMatch(actual, expected, context);
        }

        private static void AssertBinaryMatch(byte[] actual, byte[] expected, string context)
        {
            if (actual.Length != expected.Length)
            {
                throw new InvalidOperationException(context + " length mismatch. Expected: " + expected.Length + ", Got: " + actual.Length);
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (actual[i] != expected[i])
                {
                    throw new InvalidOperationException(context + " content mismatch at offset " + i + ".");
                }
            }
        }

        #endregion

        #region Route-Configuration

        private static void ConfigureBodyRoutes(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/stream-sync", StreamSyncEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/body/stream-sync", StreamSyncEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PATCH, "/body/stream-sync", StreamSyncEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/stream-async", StreamAsyncEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/body/stream-async", StreamAsyncEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/stream-async-binary", StreamAsyncBinaryEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/databytes", DataBytesEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/body/databytes", DataBytesEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PATCH, "/body/databytes", DataBytesEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.DELETE, "/body/databytes", DataBytesEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/databytes-binary", DataBytesBinaryEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/stream-async-raw", StreamAsyncRawBinaryEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/databytes-raw", DataBytesRawBinaryEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/datastring", DataStringEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/body/datastring", DataStringEchoHandler);

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/body/readbodyasync", ReadBodyAsyncEchoHandler);
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.PUT, "/body/readbodyasync", ReadBodyAsyncEchoHandler);
        }

        private static async Task StreamSyncEchoHandler(HttpContextBase context)
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
        }

        private static async Task StreamAsyncEchoHandler(HttpContextBase context)
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
        }

        private static async Task StreamAsyncBinaryEchoHandler(HttpContextBase context)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await context.Request.Data.ReadAsync(buffer, 0, buffer.Length, context.Token).ConfigureAwait(false)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }

                byte[] result = ms.ToArray();
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(Convert.ToBase64String(result), context.Token).ConfigureAwait(false);
            }
        }

        private static async Task StreamAsyncRawBinaryEchoHandler(HttpContextBase context)
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
        }

        private static async Task DataBytesRawBinaryEchoHandler(HttpContextBase context)
        {
            byte[] data = context.Request.DataAsBytes;
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/octet-stream";
            await context.Response.Send(data ?? Array.Empty<byte>(), context.Token).ConfigureAwait(false);
        }

        private static async Task DataBytesEchoHandler(HttpContextBase context)
        {
            byte[] data = context.Request.DataAsBytes;
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.Send(Encoding.UTF8.GetString(data ?? Array.Empty<byte>()), context.Token).ConfigureAwait(false);
        }

        private static async Task DataBytesBinaryEchoHandler(HttpContextBase context)
        {
            byte[] data = context.Request.DataAsBytes;
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.Send(Convert.ToBase64String(data ?? Array.Empty<byte>()), context.Token).ConfigureAwait(false);
        }

        private static async Task DataStringEchoHandler(HttpContextBase context)
        {
            string data = context.Request.DataAsString;
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.Send(data ?? String.Empty, context.Token).ConfigureAwait(false);
        }

        private static async Task ReadBodyAsyncEchoHandler(HttpContextBase context)
        {
            byte[] data = await context.Request.ReadBodyAsync(context.Token).ConfigureAwait(false);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            await context.Response.Send(Encoding.UTF8.GetString(data ?? Array.Empty<byte>()), context.Token).ConfigureAwait(false);
        }

        private static void ConfigureWebSocketRoutes(Webserver server)
        {
            server.Settings.WebSockets.Enable = true;

            server.WebSocket("/ws/echo-text", async (HttpContextBase ctx, WatsonWebserver.Core.WebSockets.WebSocketSession session) =>
            {
                while (session.IsConnected)
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message == null) break;
                    if (message.Text != null)
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                    }
                }
            });

            server.WebSocket("/ws/echo-binary", async (HttpContextBase ctx, WatsonWebserver.Core.WebSockets.WebSocketSession session) =>
            {
                while (session.IsConnected)
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message == null) break;
                    if (message.Data != null)
                    {
                        await session.SendBinaryAsync(message.Data, ctx.Token).ConfigureAwait(false);
                    }
                }
            });

            server.WebSocket("/ws/echo-any", async (HttpContextBase ctx, WatsonWebserver.Core.WebSockets.WebSocketSession session) =>
            {
                while (session.IsConnected)
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message == null) break;

                    if (message.MessageType == WebSocketMessageType.Text && message.Text != null)
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                    }
                    else if (message.Data != null)
                    {
                        await session.SendBinaryAsync(message.Data, ctx.Token).ConfigureAwait(false);
                    }
                }
            });
        }

        #endregion
    }
}
