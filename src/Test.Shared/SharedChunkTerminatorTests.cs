namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http2;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Wire-level coverage for the end of a chunked response: the terminating chunk is written exactly once
    /// on HTTP/1.1, whether the final call carries data or is empty, and HTTP/2 ends the stream cleanly.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class SharedChunkTerminatorTests
    {
        private const int _ReadTimeoutMs = 2000;
        private const int _LeftoverWaitMs = 300;

        private const string _TwoChunksThenEmptyFinalBody = "7\r\npart 1\n\r\n7\r\npart 2\n\r\n0\r\n\r\n";
        private const string _EmptyFinalOnlyBody = "0\r\n\r\n";
        private const string _NonEmptyFinalBody = "7\r\npart 1\n\r\n7\r\npart 2\n\r\n0\r\n\r\n";

        /// <summary>
        /// Get the chunk terminator tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateAsync("HTTP/1.1 :: Empty final chunk after data chunks writes the terminator once", TestHttp1EmptyFinalAfterDataChunksAsync));
            tests.Add(CreateAsync("HTTP/1.1 :: Single empty final chunk on a 202 writes only the terminator", TestHttp1EmptyFinalOnlyAsync));
            tests.Add(CreateAsync("HTTP/1.1 :: Non-empty final chunk bytes are unchanged", TestHttp1NonEmptyFinalAsync));
            tests.Add(CreateAsync("HTTP/1.1 :: Keep-alive connection reuse after an empty final chunk", TestHttp1KeepAliveReuseAfterEmptyFinalAsync));
            tests.Add(CreateAsync("HTTP/2 :: Empty final chunk after data chunks ends the stream cleanly", TestHttp2EmptyFinalAfterDataChunksAsync));
            tests.Add(CreateAsync("HTTP/2 :: Single empty final chunk ends the stream cleanly", TestHttp2EmptyFinalOnlyAsync));

            return tests.ToArray();
        }

        #region HTTP/1.1

        private static async Task TestHttp1EmptyFinalAfterDataChunksAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                byte[] raw = await SendAndReadToEndAsync(host.Port, "/chunk/empty-final").ConfigureAwait(false);
                string body = GetBodyAfterHeaders(raw);

                AssertStatusLine(raw, "HTTP/1.1 200", "The empty-final route");
                AssertEquals(_TwoChunksThenEmptyFinalBody, body, "The body should end with exactly one terminating chunk and no bytes after it.");
            }
        }

        private static async Task TestHttp1EmptyFinalOnlyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                byte[] raw = await SendAndReadToEndAsync(host.Port, "/chunk/accepted").ConfigureAwait(false);
                string body = GetBodyAfterHeaders(raw);

                AssertStatusLine(raw, "HTTP/1.1 202", "The bodyless 202 route");
                AssertEquals(_EmptyFinalOnlyBody, body, "A single empty final chunk should write only the terminator.");
            }
        }

        private static async Task TestHttp1NonEmptyFinalAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                byte[] raw = await SendAndReadToEndAsync(host.Port, "/chunk/data-final").ConfigureAwait(false);
                string body = GetBodyAfterHeaders(raw);

                AssertStatusLine(raw, "HTTP/1.1 200", "The data-final route");
                AssertEquals(_NonEmptyFinalBody, body, "A non-empty final chunk should be followed by exactly one terminating chunk.");
            }
        }

        private static async Task TestHttp1KeepAliveReuseAfterEmptyFinalAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes, settings => settings.IO.EnableKeepAlive = true))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);
                client.ReceiveTimeout = _ReadTimeoutMs;

                using (NetworkStream stream = client.GetStream())
                {
                    List<byte> buffer = new List<byte>();

                    await WriteRequestAsync(stream, host.Port, "/chunk/empty-final-keepalive", true).ConfigureAwait(false);
                    string firstStatus = ReadChunkedResponse(stream, buffer, out string firstBody);
                    AssertTrue(firstStatus.StartsWith("HTTP/1.1 200", StringComparison.Ordinal), "The first response should be 200. Actual: " + firstStatus);
                    AssertEquals("part 1\npart 2\n", firstBody, "The first response body should decode from its chunked framing.");

                    // Anything the server wrote past the end of the first message would be parsed by a
                    // client as the start of the next response.
                    await Task.Delay(_LeftoverWaitMs).ConfigureAwait(false);
                    while (stream.DataAvailable)
                    {
                        int value = stream.ReadByte();
                        if (value < 0) break;
                        buffer.Add((byte)value);
                    }

                    AssertEquals(0, buffer.Count, "No bytes should follow the first response on a kept-alive connection.");

                    await WriteRequestAsync(stream, host.Port, "/chunk/empty-final-keepalive", true).ConfigureAwait(false);
                    string secondStatus = ReadChunkedResponse(stream, buffer, out string secondBody);
                    AssertTrue(secondStatus.StartsWith("HTTP/1.1 200", StringComparison.Ordinal), "The second response on the reused connection should be 200. Actual: " + secondStatus);
                    AssertEquals("part 1\npart 2\n", secondBody, "The second response body should decode from its chunked framing.");
                }
            }
        }

        #endregion

        #region HTTP/2

        private static async Task TestHttp2EmptyFinalAfterDataChunksAsync()
        {
            Http2ResponseEnvelope response = await SendHttp2RequestAsync("/chunk/empty-final").ConfigureAwait(false);
            AssertEquals("200", response.Headers.Get(":status"), "The HTTP/2 empty-final route status.");
            AssertEquals("part 1\npart 2\n", response.BodyString, "The HTTP/2 empty-final route body.");
        }

        private static async Task TestHttp2EmptyFinalOnlyAsync()
        {
            Http2ResponseEnvelope response = await SendHttp2RequestAsync("/chunk/accepted").ConfigureAwait(false);
            AssertEquals("202", response.Headers.Get(":status"), "The HTTP/2 bodyless 202 route status.");
            AssertEquals(String.Empty, response.BodyString, "The HTTP/2 bodyless 202 route body.");
        }

        private static async Task<Http2ResponseEnvelope> SendHttp2RequestAsync(string path)
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureRoutes))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    AssertTrue(
                        serverSettings.Header.Type == Http2FrameType.Settings && serverAcknowledgement.Header.Type == Http2FrameType.Settings,
                        "Expected HTTP/2 handshake frames.");

                    byte[] requestHeaderBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), path);
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

                    // ReadResponseAsync returns at END_STREAM, so returning at all proves the stream ended.
                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);

                    // Nothing further may arrive on the closed stream. A PING round trip shows the next
                    // frame the server writes is the PING acknowledgement, not a stray DATA frame.
                    byte[] pingPayload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
                    Http2RawFrame pingFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = pingPayload.Length,
                            Type = Http2FrameType.Ping,
                            Flags = (byte)Http2FrameFlags.None,
                            StreamIdentifier = 0
                        },
                        pingPayload);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(pingFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    while (true)
                    {
                        Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                        if (frame.Header.Type == Http2FrameType.WindowUpdate || frame.Header.Type == Http2FrameType.Settings) continue;

                        AssertEquals(Http2FrameType.Ping, frame.Header.Type, "The first frame after the response should be the PING acknowledgement.");
                        break;
                    }

                    return response;
                }
            }
        }

        #endregion

        #region Routes

        private static void ConfigureRoutes(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/chunk/empty-final", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.ChunkedTransfer = true;
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 1\n"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 2\n"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Array.Empty<byte>(), true, ctx.Token).ConfigureAwait(false);
            });

            // Chunked responses inherit the default "Connection: close" header, so this route opts in to
            // keep-alive explicitly, which is how an application keeps a chunked response's connection open.
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/chunk/empty-final-keepalive", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.Headers.Add(WebserverConstants.HeaderConnection, "keep-alive");
                ctx.Response.ChunkedTransfer = true;
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 1\n"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 2\n"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Array.Empty<byte>(), true, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/chunk/accepted", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 202;
                ctx.Response.ChunkedTransfer = true;
                await ctx.Response.SendChunk(Array.Empty<byte>(), true, ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/chunk/data-final", async (HttpContextBase ctx) =>
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/plain";
                ctx.Response.ChunkedTransfer = true;
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 1\n"), false, ctx.Token).ConfigureAwait(false);
                await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("part 2\n"), true, ctx.Token).ConfigureAwait(false);
            });
        }

        #endregion

        #region Raw-HTTP/1.1-Helpers

        private static async Task WriteRequestAsync(NetworkStream stream, int port, string path, bool keepAlive)
        {
            string request =
                "GET " + path + " HTTP/1.1\r\n" +
                "Host: 127.0.0.1:" + port.ToString() + "\r\n" +
                "Connection: " + (keepAlive ? "keep-alive" : "close") + "\r\n" +
                "\r\n";

            byte[] requestBytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        private static async Task<byte[]> SendAndReadToEndAsync(int port, string path)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);
                client.ReceiveTimeout = _ReadTimeoutMs;

                using (NetworkStream stream = client.GetStream())
                using (MemoryStream received = new MemoryStream())
                {
                    await WriteRequestAsync(stream, port, path, false).ConfigureAwait(false);

                    // Read until the server closes the connection, or until the read timeout passes,
                    // so any bytes written after the end of the message are captured.
                    byte[] readBuffer = new byte[4096];
                    while (true)
                    {
                        int read;
                        try
                        {
                            read = stream.Read(readBuffer, 0, readBuffer.Length);
                        }
                        catch (IOException)
                        {
                            break;
                        }

                        if (read < 1) break;
                        received.Write(readBuffer, 0, read);
                    }

                    return received.ToArray();
                }
            }
        }

        private static string GetBodyAfterHeaders(byte[] raw)
        {
            string text = Encoding.ASCII.GetString(raw);
            int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            AssertTrue(headerEnd >= 0, "The response should contain a complete header block. Received: " + Escape(text));
            return text.Substring(headerEnd + 4);
        }

        private static void AssertStatusLine(byte[] raw, string expectedPrefix, string description)
        {
            string text = Encoding.ASCII.GetString(raw);
            AssertTrue(text.StartsWith(expectedPrefix, StringComparison.Ordinal), description + " should start with " + expectedPrefix + ". Received: " + Escape(text));
            AssertTrue(text.IndexOf("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase) >= 0, description + " should use chunked transfer-encoding.");
        }

        /// <summary>
        /// Read exactly one chunked response from the stream using its framing, leaving any bytes past the
        /// end of the message in <paramref name="buffer"/>.
        /// </summary>
        private static string ReadChunkedResponse(NetworkStream stream, List<byte> buffer, out string body)
        {
            string statusLine = ReadLine(stream, buffer);
            while (true)
            {
                string headerLine = ReadLine(stream, buffer);
                if (headerLine.Length == 0) break;
            }

            StringBuilder decoded = new StringBuilder();
            while (true)
            {
                string sizeLine = ReadLine(stream, buffer);
                int extensionIndex = sizeLine.IndexOf(';');
                if (extensionIndex >= 0) sizeLine = sizeLine.Substring(0, extensionIndex);
                int size = Convert.ToInt32(sizeLine.Trim(), 16);

                if (size == 0)
                {
                    // Trailer section, ended by an empty line.
                    while (true)
                    {
                        string trailerLine = ReadLine(stream, buffer);
                        if (trailerLine.Length == 0) break;
                    }

                    break;
                }

                byte[] data = ReadExact(stream, buffer, size);
                decoded.Append(Encoding.UTF8.GetString(data));
                string dataEnd = ReadLine(stream, buffer);
                AssertEquals(String.Empty, dataEnd, "Chunk data should be followed by CRLF.");
            }

            body = decoded.ToString();
            return statusLine;
        }

        private static string ReadLine(NetworkStream stream, List<byte> buffer)
        {
            List<byte> line = new List<byte>();
            while (true)
            {
                byte value = TakeByte(stream, buffer);
                if (value == (byte)'\n')
                {
                    if (line.Count > 0 && line[line.Count - 1] == (byte)'\r') line.RemoveAt(line.Count - 1);
                    return Encoding.ASCII.GetString(line.ToArray());
                }

                line.Add(value);
            }
        }

        private static byte[] ReadExact(NetworkStream stream, List<byte> buffer, int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = TakeByte(stream, buffer);
            }

            return result;
        }

        private static byte TakeByte(NetworkStream stream, List<byte> buffer)
        {
            if (buffer.Count > 0)
            {
                byte buffered = buffer[0];
                buffer.RemoveAt(0);
                return buffered;
            }

            int value = stream.ReadByte();
            if (value < 0) throw new IOException("The connection closed before the response was complete.");
            return (byte)value;
        }

        private static string Escape(string value)
        {
            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }

        #endregion

        #region Assertions

        private static SharedNamedTestCase CreateAsync(string name, Func<Task> func)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return new SharedNamedTestCase(name, func);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                string expectedText = expected == null ? "null" : Escape(expected.ToString());
                string actualText = actual == null ? "null" : Escape(actual.ToString());
                throw new InvalidOperationException(message + " Expected: " + expectedText + " Actual: " + actualText);
            }
        }

        #endregion
    }
}
