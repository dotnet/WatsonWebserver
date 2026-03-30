namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared HTTP/2 smoke tests executed by both runners.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class SharedHttp2SmokeTests
    {
        /// <summary>
        /// Verify a basic cleartext HTTP/2 GET request routes and returns a normal response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2BasicGetAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), "/test/get");
                    Http2RawFrame requestFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = requestHeaderBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                            StreamIdentifier = 1
                        },
                        requestHeaderBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(requestFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200" || response.BodyString != "GET response")
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 basic GET response.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify a continuation header block routes and returns the expected response.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2ContinuationHeaderBlockAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    List<HpackHeaderField> requestHeaders = new List<HpackHeaderField>();
                    requestHeaders.Add(new HpackHeaderField { Name = ":method", Value = "GET" });
                    requestHeaders.Add(new HpackHeaderField { Name = ":scheme", Value = "http" });
                    requestHeaders.Add(new HpackHeaderField { Name = ":authority", Value = "127.0.0.1:" + host.Port.ToString() });
                    requestHeaders.Add(new HpackHeaderField { Name = ":path", Value = "/test/get" });
                    requestHeaders.Add(new HpackHeaderField { Name = "accept", Value = "*/*" });

                    byte[] requestHeaderBytes = HpackCodec.Encode(requestHeaders);
                    int splitOffset = Math.Max(1, requestHeaderBytes.Length / 2);
                    byte[] firstFragment = new byte[splitOffset];
                    byte[] secondFragment = new byte[requestHeaderBytes.Length - splitOffset];
                    Buffer.BlockCopy(requestHeaderBytes, 0, firstFragment, 0, firstFragment.Length);
                    Buffer.BlockCopy(requestHeaderBytes, splitOffset, secondFragment, 0, secondFragment.Length);

                    Http2RawFrame headersFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = firstFragment.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                            StreamIdentifier = 1
                        },
                        firstFragment);

                    Http2RawFrame continuationFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = secondFragment.Length,
                            Type = Http2FrameType.Continuation,
                            Flags = (byte)Http2FrameFlags.EndHeaders,
                            StreamIdentifier = 1
                        },
                        secondFragment);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(headersFrame)).ConfigureAwait(false);
                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(continuationFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200" || response.BodyString != "GET response")
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 continuation response.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify padded priority headers and padded DATA frames are accepted and routed correctly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2PaddedPriorityHeadersAndDataAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "127.0.0.1:" + host.Port.ToString(), "/test/chunked-echo");
                    byte[] paddedHeaderPayload = Http2SharedTestUtilities.BuildPaddedPriorityHeadersPayload(requestHeaderBytes, 2, 0, 15);
                    byte[] requestBody = Encoding.UTF8.GetBytes("padded-data");
                    byte[] paddedDataPayload = Http2SharedTestUtilities.BuildPaddedDataPayload(requestBody, 3);

                    Http2RawFrame headersFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = paddedHeaderPayload.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.Padded | (byte)Http2FrameFlags.Priority),
                            StreamIdentifier = 1
                        },
                        paddedHeaderPayload);

                    Http2RawFrame dataFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = paddedDataPayload.Length,
                            Type = Http2FrameType.Data,
                            Flags = (byte)((byte)Http2FrameFlags.EndStreamOrAck | (byte)Http2FrameFlags.Padded),
                            StreamIdentifier = 1
                        },
                        paddedDataPayload);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(headersFrame)).ConfigureAwait(false);
                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(dataFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200" || response.BodyString != "padded-data")
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 padded/prioritized route response.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify HTTP/2 response trailers are emitted and decoded correctly.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2ResponseTrailersAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), "/test/http2-trailers");
                    Http2RawFrame requestFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = requestHeaderBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                            StreamIdentifier = 1
                        },
                        requestHeaderBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(requestFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200"
                        || response.BodyString != "trailers-body"
                        || response.Trailers.Get("x-checksum") != "abc123"
                        || response.Trailers.Get("x-finished") != "true"
                        || response.Trailers.Get("content-length") != null)
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 trailer response.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/2 chunked-style API response is surfaced as a normal streamed body.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2ChunkedApiResponseAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), "/test/chunked-wire");
                    Http2RawFrame requestFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = requestHeaderBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                            StreamIdentifier = 1
                        },
                        requestHeaderBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(requestFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get(":status") != "200" || response.BodyString != "first\nsecond\nthird\n")
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 chunked API response.");
                    }
                }
            }
        }

        /// <summary>
        /// Verify an HTTP/2 SSE API response is surfaced with the correct content type and event payload.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp2ServerSentEventsResponseAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2Routes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    if (serverSettings.Header.Type != Http2FrameType.Settings || serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                    {
                        throw new InvalidOperationException("Expected HTTP/2 handshake frames.");
                    }

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), "/test/sse-wire");
                    Http2RawFrame requestFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = requestHeaderBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                            StreamIdentifier = 1
                        },
                        requestHeaderBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(requestFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    if (response.Headers.Get("content-type") != "text/event-stream; charset=utf-8"
                        || !response.BodyString.Contains("id: evt-1\n", StringComparison.Ordinal)
                        || !response.BodyString.Contains("event: update\n", StringComparison.Ordinal)
                        || !response.BodyString.Contains("data: Line1\n", StringComparison.Ordinal)
                        || !response.BodyString.Contains("data: Line2\n", StringComparison.Ordinal)
                        || !response.BodyString.Contains("data: done\n", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected HTTP/2 SSE API response.");
                    }
                }
            }
        }

        private static void ConfigureHttp2Routes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/get", async (HttpContextBase context) =>
            {
                await context.Response.Send("GET response", context.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/test/chunked-echo", async (HttpContextBase context) =>
            {
                await context.Response.Send(context.Request.DataAsString, context.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/chunked-wire", async (HttpContextBase context) =>
            {
                context.Response.ChunkedTransfer = true;
                await context.Response.SendChunk(Encoding.UTF8.GetBytes("first\n"), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes("second\n"), false, context.Token).ConfigureAwait(false);
                await context.Response.SendChunk(Encoding.UTF8.GetBytes("third\n"), true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/sse-wire", async (HttpContextBase context) =>
            {
                context.Response.ServerSentEvents = true;

                ServerSentEvent firstEvent = new ServerSentEvent();
                firstEvent.Id = "evt-1";
                firstEvent.Event = "update";
                firstEvent.Data = "Line1\nLine2";
                firstEvent.Retry = "1500";
                await context.Response.SendEvent(firstEvent, false, context.Token).ConfigureAwait(false);

                ServerSentEvent secondEvent = new ServerSentEvent();
                secondEvent.Data = "done";
                await context.Response.SendEvent(secondEvent, true, context.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/http2-trailers", async (HttpContextBase context) =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.Trailers.Add("x-checksum", "abc123");
                context.Response.Trailers.Add("x-finished", "true");
                context.Response.Trailers.Add("content-length", "999");
                await context.Response.Send("trailers-body", context.Token).ConfigureAwait(false);
            });
        }
    }
}
