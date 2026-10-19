namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Sockets;
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Linq;
    using System.Text;
    using WatsonWebserver;
    using WatsonWebserver.Http2;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http2;

    /// <summary>Raw-frame HTTP/2 resilience regressions.</summary>
    internal static class SharedHttp2ResilienceTests
    {
        internal static SharedNamedTestCase[] GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();
            for (int scenario = 1; scenario <= 14; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("T1." + scenario + " :: Frame dispatch", () => TestDispatchAsync(captured)));
            }
            for (int scenario = 1; scenario <= 5; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("T2." + scenario + " :: Writer commit", () => TestWriterAsync(captured)));
            }
            for (int scenario = 1; scenario <= 7; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("T4." + scenario + " :: Reset isolation h2c", () => TestResetAsync(captured, false)));
                tests.Add(new SharedNamedTestCase("T4." + scenario + " :: Reset isolation TLS", () => TestResetAsync(captured, true)));
            }
            for (int scenario = 1; scenario <= 4; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("T5." + scenario + " :: Default flow window", () => TestFlowAsync(captured)));
            }
            tests.Add(new SharedNamedTestCase("Connection credit survives a reset during a large response", TestCreditAsync));
            tests.Add(new SharedNamedTestCase("Source failure resets only its stream", TestSourceFailureAsync));
            tests.Add(new SharedNamedTestCase("Late DATA on reset streams returns full padded connection credit", TestLateDataAsync));
            tests.Add(new SharedNamedTestCase("Closed stream reuse is STREAM_CLOSED", TestReuseAsync));
            tests.Add(new SharedNamedTestCase("DATA on an idle stream remains PROTOCOL_ERROR", TestIdleDataAsync));
            tests.Add(new SharedNamedTestCase("Timeout still returns 408 without reset", TestTimeoutAsync));
            tests.Add(new SharedNamedTestCase("Ordinary handler exceptions and missing response retain fallback behavior", TestFallbackAsync));
            for (int scenario = 0; scenario < 6; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("Golden frame sequence " + scenario, () => TestGoldenAsync(captured)));
            }
            tests.Add(new SharedNamedTestCase("Atomic commit and terminate races preserve reservation ownership (1000 iterations)", TestReservationRaceAsync));
            tests.Add(new SharedNamedTestCase("Queued response refunds exactly once and writes nothing after termination", TestQueuedRefundAsync));
            tests.Add(new SharedNamedTestCase("Reset trailers preserve HPACK dynamic table", () => TestResetHpackAsync(false)));
            tests.Add(new SharedNamedTestCase("Reset tombstone eviction keeps late DATA harmless", TestResetEvictionAsync));
            tests.Add(new SharedNamedTestCase("Rapid reset retains the 100-handler work bound", TestRapidResetAsync));
            tests.Add(new SharedNamedTestCase("200 multiplexed requests with alternating resets preserve siblings", TestSoakAsync));
            tests.Add(new SharedNamedTestCase("API route ignoring cancellation aborts without exception callbacks", TestApiResetAsync));
            for (int closure = 0; closure < 4; closure++)
            {
                int captured = closure;
                tests.Add(new SharedNamedTestCase("Closure policy " + closure + " :: h2c", () => TestClosedDataAsync(captured, false)));
                tests.Add(new SharedNamedTestCase("Closure policy " + closure + " :: TLS", () => TestClosedDataAsync(captured, true)));
            }
            tests.Add(new SharedNamedTestCase("Bounded closure history preserves reasons and handles eviction conservatively", TestClosedHistoryAsync));
            tests.Add(new SharedNamedTestCase("Evicted reset trailers preserve HPACK dynamic table", () => TestResetHpackAsync(true)));
            string[] priorityCases = new string[] { "idle PRIORITY", "open PRIORITY", "HEADERS priority fields" };
            for (int scenario = 0; scenario < priorityCases.Length; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("RFC 9113 :: Self-dependent " + priorityCases[scenario] + " :: h2c", () => TestSelfDependentPriorityAsync(captured, false)));
                tests.Add(new SharedNamedTestCase("RFC 9113 :: Self-dependent " + priorityCases[scenario] + " :: TLS", () => TestSelfDependentPriorityAsync(captured, true)));
            }
            string[] conformanceCases = new string[]
            {
                "SETTINGS ACK payload", "idle RST_STREAM", "idle WINDOW_UPDATE",
                "oversized DATA header", "oversized HEADERS header", "oversized CONTINUATION header",
                "implicit closed stream reuse", "late HPACK table update", "stream window overflow isolation",
                "connection window overflow", "SETTINGS ACK stream ID", "GOAWAY draining", "valid HPACK table updates"
            };
            for (int scenario = 0; scenario < conformanceCases.Length; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("RFC conformance :: " + conformanceCases[scenario] + " :: h2c", () => TestConformanceAsync(captured, false)));
                tests.Add(new SharedNamedTestCase("RFC conformance :: " + conformanceCases[scenario] + " :: TLS", () => TestConformanceAsync(captured, true)));
            }
            tests.Add(new SharedNamedTestCase("RFC negative window and incremental SETTINGS :: h2c", () => TestWindowSettingsAsync(false)));
            tests.Add(new SharedNamedTestCase("RFC negative window and incremental SETTINGS :: TLS", () => TestWindowSettingsAsync(true)));
            string[] handshakeCases = new string[] { "invalid preface", "oversized initial SETTINGS", "non-SETTINGS first frame", "valid handshake" };
            for (int scenario = 0; scenario < handshakeCases.Length; scenario++)
            {
                int captured = scenario;
                tests.Add(new SharedNamedTestCase("RFC dedicated listener :: " + handshakeCases[scenario] + " :: h2c", () => TestInvalidPrefaceAsync(false, captured)));
                tests.Add(new SharedNamedTestCase("RFC dedicated listener :: " + handshakeCases[scenario] + " :: TLS", () => TestInvalidPrefaceAsync(true, captured)));
            }
            return tests.ToArray();
        }

        private static byte[] WindowSetting(int size)
        {
            byte[] payload = new byte[6] { 0, 4, 0, 0, 0, 0 };
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(2), size);
            return payload;
        }

        private static async Task TestConformanceAsync(int scenario, bool tls)
        {
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server =>
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hold", async ctx =>
                {
                    await release.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                    await ctx.Response.Send("complete").ConfigureAwait(false);
                })))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token).ConfigureAwait(false))
                    {
                        await HandshakeAsync(stream, timeout.Token).ConfigureAwait(false);
                        Http2ErrorCode expected = Http2ErrorCode.ProtocolError;
                        if (scenario == 0 || scenario == 10)
                        {
                            expected = scenario == 0 ? Http2ErrorCode.FrameSizeError : Http2ErrorCode.ProtocolError;
                            await WriteAsync(stream, 4, 1, scenario == 0 ? 0 : 1, scenario == 0 ? new byte[6] : Array.Empty<byte>(), timeout.Token);
                        }
                        else if (scenario == 1) await WriteAsync(stream, 3, 0, 1, new byte[4], timeout.Token);
                        else if (scenario == 2) await WindowUpdateAsync(stream, 1, 1, timeout.Token);
                        else if (scenario >= 3 && scenario <= 5)
                        {
                            expected = Http2ErrorCode.FrameSizeError;
                            if (scenario == 5)
                                await WriteAsync(stream, 1, 1, 1, Array.Empty<byte>(), timeout.Token);
                            // Send only the header: rejection must not wait for an oversized payload.
                            Http2FrameHeader header = new Http2FrameHeader
                            {
                                Type = scenario == 3 ? Http2FrameType.Data : scenario == 4 ? Http2FrameType.Headers : Http2FrameType.Continuation,
                                Length = 16385, StreamIdentifier = 1
                            };
                            await stream.WriteAsync(Http2FrameSerializer.SerializeFrameHeader(header), timeout.Token);
                        }
                        else if (scenario == 6)
                        {
                            await GetAsync(stream, 5, "/hold", timeout.Token);
                            await PingAsync(stream, timeout.Token);
                            await GetAsync(stream, 3, "/hold", timeout.Token);
                        }
                        else if (scenario == 7)
                        {
                            expected = Http2ErrorCode.CompressionError;
                            byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "localhost", "/hold");
                            await WriteAsync(stream, 1, 5, 1, headers.Concat(new byte[] { 0x20 }).ToArray(), timeout.Token);
                        }
                        else if (scenario == 8 || scenario == 9)
                        {
                            expected = Http2ErrorCode.FlowControlError;
                            await GetAsync(stream, 1, "/hold", timeout.Token);
                            await PingAsync(stream, timeout.Token);
                            await WindowUpdateAsync(stream, scenario == 8 ? 1 : 0, Int32.MaxValue, timeout.Token);
                        }
                        else if (scenario == 11)
                        {
                            await GetAsync(stream, 1, "/hold", timeout.Token);
                            await PingAsync(stream, timeout.Token);
                            await WriteAsync(stream, 7, 0, 0, new byte[8], timeout.Token);
                            await PingAsync(stream, timeout.Token);
                            await GetAsync(stream, 3, "/hold", timeout.Token);
                            List<Http2RawFrame> refused = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token);
                            Check(refused.Last().Header.StreamIdentifier == 3 &&
                                Http2FrameSerializer.ReadRstStreamFrame(refused.Last()).ErrorCode == Http2ErrorCode.RefusedStream,
                                "Draining connection accepted a new stream.");
                            release.TrySetResult(true);
                            List<Http2RawFrame> completed = await ReadUntilAsync(stream,
                                f => f.Header.StreamIdentifier == 1 && f.Header.Type == Http2FrameType.Data && (f.Header.Flags & 1) != 0, timeout.Token);
                            Check(Encoding.UTF8.GetString(completed.Last().Payload) == "complete", "GOAWAY interrupted an existing stream.");
                            return;
                        }
                        else
                        {
                            // Beginning-of-block table updates remain valid, including consecutive updates.
                            byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "localhost", "/hold");
                            await WriteAsync(stream, 1, 5, 1, new byte[] { 0x20, 0x20 }.Concat(headers).ToArray(), timeout.Token);
                            await PingAsync(stream, timeout.Token);
                            release.TrySetResult(true);
                            await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && f.Header.Type == Http2FrameType.Data && (f.Header.Flags & 1) != 0, timeout.Token);
                            return;
                        }
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream,
                            f => f.Header.Type == (scenario == 8 ? Http2FrameType.RstStream : Http2FrameType.GoAway), timeout.Token);
                        Http2RawFrame error = frames.Last();
                        Http2ErrorCode actual = scenario == 8 ? Http2FrameSerializer.ReadRstStreamFrame(error).ErrorCode
                            : Http2FrameSerializer.ReadGoAwayFrame(error).ErrorCode;
                        Check(actual == expected, "Incorrect conformance error: " + actual + ", expected " + expected);
                        if (scenario == 8)
                        {
                            Check(error.Header.StreamIdentifier == 1, "Overflow reset the wrong stream.");
                            await GetAsync(stream, 3, "/hold", timeout.Token);
                            release.TrySetResult(true);
                            await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && f.Header.Type == Http2FrameType.Data && (f.Header.Flags & 1) != 0, timeout.Token);
                            await PingAsync(stream, timeout.Token);
                        }
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        private static async Task TestWindowSettingsAsync(bool tls)
        {
            byte[] body = Encoding.ASCII.GetBytes("abcdefghijkl");
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server =>
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/window", ctx => ctx.Response.Send(body))))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await WriteAsync(stream, 4, 0, 0, WindowSetting(2), timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Settings && (f.Header.Flags & 1) != 0, timeout.Token);
                    await GetAsync(stream, 1, "/window", timeout.Token);
                    List<Http2RawFrame> first = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Data, timeout.Token);
                    List<byte> received = new List<byte>(first.Last().Payload);
                    Check(received.Count == 2, "Initial stream window was not enforced.");
                    await WriteAsync(stream, 4, 0, 0, WindowSetting(1), timeout.Token);
                    List<Http2RawFrame> negative = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Settings && (f.Header.Flags & 1) != 0, timeout.Token);
                    Check(!negative.Any(f => f.Header.Type == Http2FrameType.Data), "DATA sent with a negative stream window.");
                    // An unrelated setting must preserve the current initial window and its delta.
                    await WriteAsync(stream, 4, 0, 0, new byte[] { 0, 6, 0, 1, 0, 0 }, timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Settings && (f.Header.Flags & 1) != 0, timeout.Token);
                    await WindowUpdateAsync(stream, 1, 1, timeout.Token);
                    await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                    List<Http2RawFrame> zero = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping && (f.Header.Flags & 1) != 0, timeout.Token);
                    Check(!zero.Any(f => f.Header.Type == Http2FrameType.Data), "DATA sent before negative window recovered above zero.");
                    await WindowUpdateAsync(stream, 1, 1, timeout.Token);
                    List<Http2RawFrame> next = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Data, timeout.Token);
                    received.AddRange(next.Last().Payload);
                    Check(next.Last().Payload.Length == 1, "Recovered window did not allow exactly one byte.");
                    await WriteAsync(stream, 4, 0, 0, WindowSetting(12), timeout.Token);
                    List<Http2RawFrame> remaining = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Data && (f.Header.Flags & 1) != 0, timeout.Token);
                    foreach (Http2RawFrame frame in remaining.Where(f => f.Header.Type == Http2FrameType.Data)) received.AddRange(frame.Payload);
                    Check(received.SequenceEqual(body), "SETTINGS increase failed to resume the blocked stream without corrupting DATA.");
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestInvalidPrefaceAsync(bool tls, int scenario)
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server => { }, settings => settings.Protocols.EnableHttp1 = false))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token))
                {
                    if (scenario == 3)
                    {
                        await HandshakeAsync(stream, timeout.Token);
                        await PingAsync(stream, timeout.Token);
                        return;
                    }
                    if (scenario == 0)
                        await stream.WriteAsync(Encoding.ASCII.GetBytes("INVALID CONNECTION PREFACE\r\n\r\n"), timeout.Token);
                    else
                    {
                        await stream.WriteAsync(Http2ConnectionPreface.GetClientPrefaceBytes(), timeout.Token);
                        if (scenario == 1)
                            await stream.WriteAsync(Http2FrameSerializer.SerializeFrameHeader(new Http2FrameHeader
                            { Type = Http2FrameType.Settings, Length = 16385 }), timeout.Token);
                        else await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                    }
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.GoAway, timeout.Token);
                    Check(frames.First().Header.Type == Http2FrameType.Settings, "Server preface must start with SETTINGS.");
                    Check(Http2FrameSerializer.ReadGoAwayFrame(frames.Last()).ErrorCode == (scenario == 1 ? Http2ErrorCode.FrameSizeError : Http2ErrorCode.ProtocolError), "Invalid TLS preface was not rejected.");
                }
            }
        }

        private static async Task TestDispatchAsync(int scenario)
        {
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", async ctx =>
                {
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    await ctx.Response.Send("fast").ConfigureAwait(false);
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port, timeout.Token).ConfigureAwait(false);
                using (NetworkStream stream = client.GetStream())
                {
                    await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).WaitAsync(timeout.Token).ConfigureAwait(false);
                    await Http2FrameSerializer.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
                    byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port, "/fast");
                    bool open = scenario == 2 || scenario == 5 || scenario == 8;
                    if (open) await WriteAsync(stream, 1, 5, 1, headers, timeout.Token).ConfigureAwait(false);
                    if (scenario == 6)
                    {
                        release.TrySetResult(true);
                        await WriteAsync(stream, 1, 5, 1, headers, timeout.Token).ConfigureAwait(false);
                        await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                    }
                    if (scenario == 1 || scenario == 6 || scenario == 13)
                    {
                        int limit = scenario == 13 ? 2001 : 13;
                        for (int id = 3; id <= limit; id += 2)
                            await WriteAsync(stream, 2, 0, id, new byte[5], timeout.Token).ConfigureAwait(false);
                    }
                    if (scenario == 2) await WriteAsync(stream, 2, 0, 1, new byte[5], timeout.Token).ConfigureAwait(false);
                    if (scenario == 3) await WriteAsync(stream, 16, 0, 0, new byte[] { 0, 0, 0, 1, 117, 61, 49 }, timeout.Token).ConfigureAwait(false);
                    if (scenario == 4 || scenario == 5) await WriteAsync(stream, 250, 255, scenario == 5 ? 1 : 0, new byte[100], timeout.Token).ConfigureAwait(false);
                    if (scenario == 6 || scenario == 9) await WriteAsync(stream, 2, 0, 0, new byte[5], timeout.Token).ConfigureAwait(false);
                    if (scenario == 7) await WriteAsync(stream, 2, 0, 5, new byte[4], timeout.Token).ConfigureAwait(false);
                    if (scenario == 8) await WriteAsync(stream, 2, 0, 1, new byte[6], timeout.Token).ConfigureAwait(false);
                    if (scenario == 10 || scenario == 14)
                    {
                        await WriteAsync(stream, 1, 1, 1, headers, timeout.Token).ConfigureAwait(false);
                        await WriteAsync(stream, scenario == 10 ? (byte)2 : (byte)250, 0, 1, new byte[5], timeout.Token).ConfigureAwait(false);
                    }
                    if (scenario == 11) await WriteAsync(stream, 9, 4, 1, new byte[1], timeout.Token).ConfigureAwait(false);
                    if (scenario == 12) await WriteAsync(stream, 5, 4, 1, new byte[4], timeout.Token).ConfigureAwait(false);
                    if (scenario == 6 || scenario == 7 || scenario >= 9 && scenario != 13)
                    {
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.GoAway, timeout.Token).ConfigureAwait(false);
                        Http2GoAwayFrame goAway = Http2FrameSerializer.ReadGoAwayFrame(frames[frames.Count - 1]);
                        Check(goAway.ErrorCode == (scenario == 7 ? Http2ErrorCode.FrameSizeError : Http2ErrorCode.ProtocolError), "Incorrect GOAWAY error code.");
                        if (scenario == 6 || scenario == 7) Check(goAway.LastStreamIdentifier == (scenario == 6 ? 1 : 0), "PRIORITY advanced last-stream-ID.");
                        return;
                    }
                    if (scenario == 8)
                    {
                        List<Http2RawFrame> reset = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token).ConfigureAwait(false);
                        Check(Http2FrameSerializer.ReadRstStreamFrame(reset[reset.Count - 1]).ErrorCode == Http2ErrorCode.FrameSizeError, "Incorrect reset code.");
                    }
                    release.TrySetResult(true);
                    int requestId = scenario == 1 ? 15 : scenario == 13 ? 2003 : scenario == 8 ? 3 : 1;
                    if (!open || scenario == 8) await WriteAsync(stream, 1, 5, requestId, headers, timeout.Token).ConfigureAwait(false);
                    await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == requestId && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                    await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token).ConfigureAwait(false);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping, timeout.Token).ConfigureAwait(false);
                }
                release.TrySetResult(true);
            }
        }

        private static async Task TestWriterAsync(int scenario)
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (CancellationTokenSource caller = new CancellationTokenSource())
            using (GatedWriteStream stream = new GatedWriteStream())
            using (Http2ConnectionWriter writer = new Http2ConnectionWriter(stream, true))
            {
                if (scenario == 1) stream.BlockWrite = 2;
                if (scenario == 2) stream.BlockWrite = 2;
                if (scenario == 5) stream.Fail = true;
                List<Http2RawFrame> frames = new List<Http2RawFrame>();
                int count = scenario == 1 ? 3 : 1;
                for (int i = 0; i < count; i++)
                    frames.Add(new Http2RawFrame(new Http2FrameHeader
                    {
                        Type = i == 0 ? Http2FrameType.Headers : Http2FrameType.Continuation,
                        Length = scenario == 2 ? 20000 : 10, StreamIdentifier = 1,
                        Flags = i == count - 1 ? (byte)4 : (byte)0
                    }, new byte[scenario == 2 ? 20000 : 10]));
                Task first = scenario == 4
                    ? writer.WriteFrameSegmentsAsync(new[] { new Http2FrameWriteSegment { Header = frames[0].Header, Payload = frames[0].Payload, Count = frames[0].Payload.Length } }, caller.Token).AsTask()
                    : writer.WriteFramesAsync(frames, scenario == 3 || scenario == 5 ? CancellationToken.None : caller.Token).AsTask();
                await stream.Entered.WaitAsync(timeout.Token).ConfigureAwait(false);
                Task queued = null;
                if (scenario == 3) queued = writer.WriteFramesAsync(frames, caller.Token).AsTask();
                caller.Cancel();
                stream.Release();
                if (scenario == 5)
                {
                    try { await first.ConfigureAwait(false); throw new InvalidOperationException("Expected transport failure."); }
                    catch (IOException) { }
                    try { await writer.WriteFramesAsync(frames).ConfigureAwait(false); throw new InvalidOperationException("Faulted writer accepted a write."); }
                    catch (IOException) { }
                    Check(stream.Bytes.Length == 0, "Faulted writer wrote additional bytes.");
                    return;
                }
                await first.WaitAsync(timeout.Token).ConfigureAwait(false);
                if (queued != null)
                {
                    try { await queued.ConfigureAwait(false); throw new InvalidOperationException("Queued write was not cancelled."); }
                    catch (OperationCanceledException) { }
                }
                Check(stream.Bytes.Length == count * (9 + frames[0].Payload.Length), "Committed frame was torn or queued write was emitted.");
            }
        }

        private static async Task<Stream> ConnectAsync(TcpClient client, int port, bool tls, CancellationToken token)
        {
            await client.ConnectAsync("127.0.0.1", port, token).ConfigureAwait(false);
            if (!tls) return client.GetStream();
            SslStream ssl = new SslStream(client.GetStream(), false, (sender, certificate, chain, errors) => true);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http2 }
            }, token).ConfigureAwait(false);
            Check(ssl.NegotiatedApplicationProtocol == SslApplicationProtocol.Http2, "ALPN did not negotiate h2.");
            return ssl;
        }

        private static async Task HandshakeAsync(Stream stream, CancellationToken token, int frameSize = 16384)
        {
            await stream.WriteAsync(Http2ConnectionPreface.GetClientPrefaceBytes(), token).ConfigureAwait(false);
            Http2Settings settings = new Http2Settings { InitialWindowSize = 65535, MaxFrameSize = frameSize };
            await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsFrame(settings)), token).ConfigureAwait(false);
            await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Settings && (f.Header.Flags & 1) != 0, token).ConfigureAwait(false);
            await WriteAsync(stream, 4, 1, 0, Array.Empty<byte>(), token).ConfigureAwait(false);
        }

        private static Task GetAsync(Stream stream, int id, string path, CancellationToken token)
        {
            return WriteAsync(stream, 1, 5, id, Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "localhost", path), token);
        }

        private static async Task PingAsync(Stream stream, CancellationToken token)
        {
            await WriteAsync(stream, 6, 0, 0, new byte[8], token).ConfigureAwait(false);
            await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping && (f.Header.Flags & 1) != 0, token).ConfigureAwait(false);
        }

        private static async Task TestResetAsync(int scenario, bool tls)
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int exceptions = 0;
            int aborted = 0;
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server =>
            {
                server.Events.RequestAborted += (sender, args) => Interlocked.Increment(ref aborted);
                server.Events.ExceptionEncountered += (sender, args) => Interlocked.Increment(ref exceptions);
                server.Routes.Exception = async (ctx, ex) => { Interlocked.Increment(ref exceptions); await ctx.Response.Send("error"); };
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/slow", async ctx =>
                {
                    entered.TrySetResult(true);
                    try
                    {
                        if (scenario == 7) await Task.Delay(Timeout.Infinite, ctx.Token).ConfigureAwait(false);
                        else
                        {
                            if (scenario >= 3 && scenario <= 5)
                            {
                                if (scenario == 4) ctx.Response.ServerSentEvents = true;
                                else ctx.Response.ChunkedTransfer = true;
                                if (scenario == 4) await ctx.Response.SendEvent(new ServerSentEvent { Data = "first" }, false);
                                else await ctx.Response.SendChunk(Encoding.UTF8.GetBytes("first"), false);
                            }
                            await release.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                            if (scenario == 3 || scenario == 5) await ctx.Response.SendChunk(new byte[1000000], true);
                            else if (scenario == 4) await ctx.Response.SendEvent(new ServerSentEvent { Data = "late" }, true);
                            else if (scenario == 6)
                            {
                                using (MemoryStream body = new MemoryStream(new byte[200000])) await ctx.Response.Send(body.Length, body);
                            }
                            else await ctx.Response.Send("late");
                        }
                    }
                    catch (OperationCanceledException) { cancelled.TrySetResult(true); throw; }
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token).ConfigureAwait(false))
                    {
                        await HandshakeAsync(stream, timeout.Token).ConfigureAwait(false);
                        await GetAsync(stream, 1, "/slow", timeout.Token).ConfigureAwait(false);
                        await entered.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                        if (scenario >= 3 && scenario <= 5)
                            await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Data, timeout.Token).ConfigureAwait(false);
                        await WriteAsync(stream, 3, 0, 1, new byte[] { 0, 0, 0, 8 }, timeout.Token).ConfigureAwait(false);
                        // PING is a reader barrier: the reset has been processed before releasing the handler.
                        await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                        release.TrySetResult(true);
                        await cancelled.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                        await GetAsync(stream, 3, "/fast", timeout.Token).ConfigureAwait(false);
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                        Check(!frames.Any(f => f.Header.StreamIdentifier == 1), "Server wrote on reset stream.");
                        await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                        Check(exceptions == 0, "Reset invoked exception callbacks.");
                        Check(aborted == 1, "Reset should raise RequestAborted exactly once.");
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        private static async Task TestFlowAsync(int scenario)
        {
            int size = scenario == 3 ? 200000 : 100000;
            byte[] body = new byte[size];
            for (int i = 0; i < body.Length; i++) body[i] = (byte)(i % 251);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/large", async ctx =>
                {
                    if (scenario == 3)
                    {
                        using (MemoryStream source = new MemoryStream(body)) await ctx.Response.Send(body.Length, source);
                    }
                    else await ctx.Response.Send(body);
                });
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token).ConfigureAwait(false))
                using (MemoryStream received = new MemoryStream())
                {
                    await HandshakeAsync(stream, timeout.Token, scenario == 2 || scenario == 3 ? 65536 : 16384).ConfigureAwait(false);
                    await GetAsync(stream, 1, "/large", timeout.Token).ConfigureAwait(false);
                    bool sibling = scenario != 4;
                    bool ended = false;
                    while (!ended || !sibling)
                    {
                        Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, timeout.Token).ConfigureAwait(false);
                        Check(frame.Header.Type != Http2FrameType.GoAway, "Unexpected GOAWAY during flow-control test.");
                        if (frame.Header.StreamIdentifier == 3 && (frame.Header.Flags & 1) != 0) sibling = true;
                        if (frame.Header.Type != Http2FrameType.Data || frame.Header.StreamIdentifier != 1) continue;
                        received.Write(frame.Payload);
                        if (scenario == 4 && received.Length == frame.Payload.Length) await GetAsync(stream, 3, "/fast", timeout.Token).ConfigureAwait(false);
                        for (int id = 0; id <= 1; id++)
                        {
                            Http2WindowUpdateFrame update = new Http2WindowUpdateFrame { StreamIdentifier = id, WindowSizeIncrement = frame.Payload.Length };
                            await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateWindowUpdateFrame(update)), timeout.Token).ConfigureAwait(false);
                        }
                        ended = (frame.Header.Flags & 1) != 0;
                    }
                    Check(received.ToArray().SequenceEqual(body), "Large response bytes changed.");
                    await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestCreditAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/large", ctx => ctx.Response.Send(new byte[200000]));
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/window", ctx => ctx.Response.Send(new byte[65535]));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await GetAsync(stream, 1, "/large", timeout.Token);
                    List<Http2RawFrame> first = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Data, timeout.Token);
                    await WriteAsync(stream, 3, 0, 1, new byte[] { 0, 0, 0, 8 }, timeout.Token);
                    await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                    first.AddRange(await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping, timeout.Token));
                    int received = first.Where(f => f.Header.Type == Http2FrameType.Data).Sum(f => f.Payload.Length);
                    await WindowUpdateAsync(stream, 0, received, timeout.Token);
                    await GetAsync(stream, 3, "/window", timeout.Token);
                    List<Http2RawFrame> sibling = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token);
                    Check(sibling.Where(f => f.Header.Type == Http2FrameType.Data).Sum(f => f.Payload.Length) == 65535, "Connection credit leaked on reset.");
                    Check(!sibling.Any(f => f.Header.StreamIdentifier == 1), "DATA followed peer reset barrier.");
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static Task WindowUpdateAsync(Stream stream, int id, int count, CancellationToken token)
        {
            return stream.WriteAsync(Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateWindowUpdateFrame(
                new Http2WindowUpdateFrame { StreamIdentifier = id, WindowSizeIncrement = count })), token).AsTask();
        }

        private static async Task TestSourceFailureAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/source", async ctx =>
                {
                    using (FailingReadStream source = new FailingReadStream()) await ctx.Response.Send(source.Length, source);
                });
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await GetAsync(stream, 1, "/source", timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token);
                    Check(Http2FrameSerializer.ReadRstStreamFrame(frames.Last()).ErrorCode == Http2ErrorCode.InternalError, "Source failure reset code.");
                    await GetAsync(stream, 3, "/fast", timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token);
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestLateDataAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "localhost", "/upload");
                    // A malformed PRIORITY resets a pending upload before dispatching its handler.
                    await WriteAsync(stream, 1, 4, 1, headers, timeout.Token);
                    await WriteAsync(stream, 2, 0, 1, new byte[6], timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token);
                    byte[] padded = Http2SharedTestUtilities.BuildPaddedDataPayload(new byte[1000], 31);
                    for (int i = 0; i < 200; i++) await WriteAsync(stream, 0, 8, 1, padded, timeout.Token);
                    await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping, timeout.Token);
                    int credit = frames.Where(f => f.Header.Type == Http2FrameType.WindowUpdate && f.Header.StreamIdentifier == 0)
                        .Sum(f => Http2FrameSerializer.ReadWindowUpdateFrame(f).WindowSizeIncrement);
                    Check(credit == padded.Length * 200, "Padded late DATA credit was not returned.");
                    await WriteAsync(stream, 3, 0, 1, new byte[] { 0, 0, 0, 8 }, timeout.Token);
                    await WindowUpdateAsync(stream, 1, 1000, timeout.Token);
                    await GetAsync(stream, 3, "/fast", timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token);
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestReuseAsync()
        {
            await TestClosedOrIdleAsync(true);
        }
        private static async Task TestIdleDataAsync()
        {
            await TestClosedOrIdleAsync(false);
        }
        private static async Task TestClosedOrIdleAsync(bool reuse)
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"))))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await GetAsync(stream, 1, "/fast", timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token);
                    await PingAsync(stream, timeout.Token);
                    if (reuse) await GetAsync(stream, 1, "/fast", timeout.Token);
                    else await WriteAsync(stream, 0, 1, 7, new byte[1], timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.GoAway, timeout.Token);
                    Check(Http2FrameSerializer.ReadGoAwayFrame(frames.Last()).ErrorCode == (reuse ? Http2ErrorCode.StreamClosed : Http2ErrorCode.ProtocolError), "Closed/idle error code.");
                }
            }
        }

        private static async Task TestTimeoutAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Settings.Timeout.DefaultTimeout = TimeSpan.FromMilliseconds(50);
                server.Get("/timeout", async req => { await Task.Delay(10000, req.CancellationToken); return "late"; });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await GetAsync(stream, 1, "/timeout", timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token);
                    Check(frames.Where(f => f.Header.Type == Http2FrameType.Headers).SelectMany(f => WatsonWebserver.Core.Hpack.HpackCodec.Decode(f.Payload)).Any(h => h.Name == ":status" && h.Value == "408"), "Timeout no longer returns 408.");
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestFallbackAsync()
        {
            int caught = 0;
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.Exception = async (ctx, ex) => { caught++; ctx.Response.StatusCode = ctx.Request.Url.RawWithoutQuery.Contains("throw") ? 503 : 500; await ctx.Response.Send("handled"); };
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/throw", ctx => throw new InvalidOperationException("expected"));
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/empty", ctx => Task.CompletedTask);
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    for (int id = 1; id <= 3; id += 2)
                    {
                        await GetAsync(stream, id, id == 1 ? "/throw" : "/empty", timeout.Token);
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == id && (f.Header.Flags & 1) != 0, timeout.Token);
                        Check(frames.Where(f => f.Header.Type == Http2FrameType.Headers).SelectMany(f => WatsonWebserver.Core.Hpack.HpackCodec.Decode(f.Payload)).Any(h => h.Name == ":status" && h.Value == (id == 1 ? "503" : "500")), "Fallback behavior changed: " + String.Join(",", frames.Where(f => f.Header.Type == Http2FrameType.Headers).SelectMany(f => WatsonWebserver.Core.Hpack.HpackCodec.Decode(f.Payload)).Select(h => h.Name + "=" + h.Value)));
                    }
                    Check(caught == 2, "Handler failure and missing response should each reach Routes.Exception.");
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestGoldenAsync(int scenario)
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/golden", async ctx =>
                {
                    ctx.Response.ContentType = "application/octet-stream";
                    if (scenario == 2) ctx.Response.Headers.Add("x-large", new string('a', 40000));
                    if (scenario == 4) ctx.Response.Trailers.Add("x-result", "complete");
                    if (scenario == 3)
                    {
                        ctx.Response.ChunkedTransfer = true;
                        await ctx.Response.SendChunk(new byte[1024], false);
                        await ctx.Response.SendChunk(new byte[2048], true);
                    }
                    else await ctx.Response.Send(new byte[scenario == 1 ? 32768 : scenario == 5 ? 4194304 : 1024]);
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await stream.WriteAsync(Http2ConnectionPreface.GetClientPrefaceBytes(), timeout.Token);
                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsFrame(new Http2Settings
                    {
                        InitialWindowSize = scenario == 5 ? 16777216 : 65535
                    })), timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Settings && (f.Header.Flags & 1) != 0, timeout.Token);
                    await WriteAsync(stream, 4, 1, 0, Array.Empty<byte>(), timeout.Token);
                    if (scenario == 5) await WindowUpdateAsync(stream, 0, 16777216 - 65535, timeout.Token);
                    await GetAsync(stream, 1, "/golden", timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token);
                    StringBuilder actual = new StringBuilder();
                    List<byte> headerBlock = new List<byte>();
                    foreach (Http2RawFrame frame in frames)
                    {
                        if (frame.Header.StreamIdentifier != 1) continue;
                        actual.Append((int)frame.Header.Type).Append(':').Append(frame.Header.Flags).Append(':').Append(frame.Payload.Length).AppendLine();
                        if (frame.Header.Type == Http2FrameType.Headers || frame.Header.Type == Http2FrameType.Continuation)
                        {
                            headerBlock.AddRange(frame.Payload);
                            if ((frame.Header.Flags & 4) != 0)
                            {
                                // Date changes on every run. Normalize only its value before comparing HPACK bytes.
                                List<WatsonWebserver.Core.Hpack.HpackHeaderField> headers = WatsonWebserver.Core.Hpack.HpackCodec.Decode(headerBlock.ToArray());
                                foreach (WatsonWebserver.Core.Hpack.HpackHeaderField header in headers)
                                    if (header.Name == "date") header.Value = "Mon, 19 Oct 2026 00:00:00 GMT";
                                actual.AppendLine(Convert.ToBase64String(WatsonWebserver.Core.Hpack.HpackCodec.Encode(headers)));
                                headerBlock.Clear();
                            }
                        }
                        else
                        {
                            actual.AppendLine(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(frame.Payload)));
                        }
                    }
                    string fixture = Path.Combine(AppContext.BaseDirectory, "Http2Golden", scenario + ".txt");
                    string capture = Environment.GetEnvironmentVariable("WATSON_CAPTURE_GOLDEN");
                    if (!String.IsNullOrEmpty(capture))
                    {
                        Directory.CreateDirectory(capture);
                        File.WriteAllText(Path.Combine(capture, scenario + ".txt"), actual.ToString());
                    }
                    else Check(File.ReadAllText(fixture) == actual.ToString(), "Successful frame sequence differs from 7.3.1: " + scenario);
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestReservationRaceAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                for (int iteration = 0; iteration < 1000; iteration++)
                {
                    Http2StreamWriteGate gate = new Http2StreamWriteGate();
                    Http2WriteReservation reservation = new Http2WriteReservation();
                    reservation.Add(16384);
                    int committed = 0;
                    Task commit = Task.Run(() =>
                    {
                        if (!gate.TryCommit()) return;
                        committed = reservation.Claim();
                        gate.Complete();
                    });
                    Task terminate = Task.Run(() => gate.Terminate());
                    await Task.WhenAll(commit, terminate).WaitAsync(timeout.Token);
                    int refunded = reservation.Claim();
                    Check(committed + refunded == 16384 && reservation.Claim() == 0, "Reservation leaked or was refunded twice.");
                    Check(gate.IsTerminal && !gate.TryCommit(), "Terminal stream accepted a later batch.");
                }
            }
        }

        private static async Task TestQueuedRefundAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (GatedWriteStream stream = new GatedWriteStream())
            using (Http2ConnectionWriter writer = new Http2ConnectionWriter(stream, true))
            using (WebserverSettings settings = new WebserverSettings("127.0.0.1", 8080, false))
            {
                Task blocker = writer.WriteFrameAsync(new Http2RawFrame(new Http2FrameHeader { Type = Http2FrameType.Ping, Length = 8 }, new byte[8])).AsTask();
                await stream.Entered.WaitAsync(timeout.Token);
                Http2StreamWriteGate gate = new Http2StreamWriteGate();
                Http2StreamStateMachine state = new Http2StreamStateMachine(1);
                state.ReceiveHeaders(true);
                int reserved = 0;
                int refunded = 0;
                using (Http2Response response = new Http2Response(new Http2Request { Method = HttpMethod.GET }, settings, writer, state,
                    (id, count, token) => { reserved += count; return Task.FromResult(count); }, 1, 16384,
                    gate, (id, count) => count, count => refunded += count))
                {
                    Task send = response.Send(new byte[1024]);
                    Check(reserved == 1024, "Queued batch did not reserve credit.");
                    gate.Terminate();
                    stream.Release();
                    await blocker.WaitAsync(timeout.Token);
                    try { await send.WaitAsync(timeout.Token); throw new InvalidOperationException("Terminal batch committed."); }
                    catch (OperationCanceledException) { }
                    Check(refunded == reserved, "Queued batch did not refund exactly its reservation.");
                    Check(stream.Bytes.Length == 17, "Stream frames appeared after termination.");
                }
            }
        }

        private static async Task TestResetHpackAsync(bool evict)
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send(ctx.Request.RetrieveHeaderValue("x-test")))))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    await WriteAsync(stream, 1, 4, 1, Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "localhost", "/upload"), timeout.Token);
                    await WriteAsync(stream, 2, 0, 1, new byte[6], timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token);
                    if (evict)
                    {
                        byte[] upload = Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "localhost", "/upload");
                        for (int id = 3; id < 402; id += 2)
                        {
                            await WriteAsync(stream, 1, 4, id, upload, timeout.Token).ConfigureAwait(false);
                            await WriteAsync(stream, 2, 0, id, new byte[6], timeout.Token).ConfigureAwait(false);
                            await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token).ConfigureAwait(false);
                        }
                    }
                    int requestId = evict ? 403 : 3;
                    // Literal with incremental indexing: x-test: value; next request uses dynamic index 62.
                    await WriteAsync(stream, 1, 5, 1, new byte[] { 64, 6, 120, 45, 116, 101, 115, 116, 5, 118, 97, 108, 117, 101 }, timeout.Token);
                    byte[] request = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "localhost", "/fast").Concat(new byte[] { 190 }).ToArray();
                    await WriteAsync(stream, 1, 5, requestId, request, timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == requestId && (f.Header.Flags & 1) != 0, timeout.Token);
                    Check(Encoding.UTF8.GetString(frames.Where(f => f.Header.Type == Http2FrameType.Data).SelectMany(f => f.Payload).ToArray()) == "value", "Discarded trailers lost HPACK context.");
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestResetEvictionAsync()
        {
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"))))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "localhost", "/upload");
                    for (int id = 1; id < 400; id += 2)
                    {
                        await WriteAsync(stream, 1, 4, id, headers, timeout.Token);
                        await WriteAsync(stream, 2, 0, id, new byte[6], timeout.Token);
                        await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.RstStream, timeout.Token);
                    }
                    await WriteAsync(stream, 0, 1, 1, new byte[1000], timeout.Token);
                    await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                    List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping, timeout.Token);
                    Check(frames.Any(f => f.Header.Type == Http2FrameType.WindowUpdate && f.Header.StreamIdentifier == 0 && Http2FrameSerializer.ReadWindowUpdateFrame(f).WindowSizeIncrement == 1000), "Evicted stream lost connection credit.");
                    await GetAsync(stream, 401, "/fast", timeout.Token);
                    await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 401 && (f.Header.Flags & 1) != 0, timeout.Token);
                    await PingAsync(stream, timeout.Token);
                }
            }
        }

        private static async Task TestRapidResetAsync()
        {
            int running = 0;
            int maximum = 0;
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Settings.Protocols.Http2.MaxConcurrentStreams = 100;
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/slow", async ctx =>
                {
                    int active = Interlocked.Increment(ref running);
                    maximum = Math.Max(maximum, active);
                    try { await release.Task.WaitAsync(timeout.Token); await ctx.Response.Send("late"); }
                    finally { if (Interlocked.Decrement(ref running) == 0) drained.TrySetResult(true); }
                });
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                    {
                        await HandshakeAsync(stream, timeout.Token);
                        for (int id = 1; id < 2000; id += 2)
                        {
                            await GetAsync(stream, id, "/slow", timeout.Token);
                            await WriteAsync(stream, 3, 0, id, new byte[] { 0, 0, 0, 8 }, timeout.Token);
                        }
                        await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token);
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream, f => f.Header.Type == Http2FrameType.Ping, timeout.Token);
                        Check(maximum == 100, "Application work did not retain its bound.");
                        Check(frames.Count(f => f.Header.Type == Http2FrameType.RstStream && Http2FrameSerializer.ReadRstStreamFrame(f).ErrorCode == Http2ErrorCode.RefusedStream) == 900, "Over-limit streams were not refused.");
                        release.TrySetResult(true);
                        await drained.Task.WaitAsync(timeout.Token);
                        await PingAsync(stream, timeout.Token);
                        await GetAsync(stream, 2001, "/fast", timeout.Token);
                        await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 2001 && (f.Header.Flags & 1) != 0, timeout.Token);
                        await PingAsync(stream, timeout.Token);
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        private static async Task TestSoakAsync()
        {
            byte[] body = Enumerable.Range(0, 1000).Select(i => (byte)(i % 251)).ToArray();
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send(body));
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/slow", async ctx =>
                {
                    await Task.Delay(Timeout.Infinite, ctx.Token);
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                {
                    await HandshakeAsync(stream, timeout.Token);
                    for (int iteration = 0; iteration < 20; iteration++)
                    {
                        HashSet<int> pending = new HashSet<int>();
                        for (int request = 0; request < 10; request++)
                        {
                            int id = (iteration * 10 + request) * 2 + 1;
                            await GetAsync(stream, id, request % 2 == 0 ? "/slow" : "/fast", timeout.Token);
                            if (request % 2 == 0) await WriteAsync(stream, 3, 0, id, new byte[] { 0, 0, 0, 8 }, timeout.Token);
                            else pending.Add(id);
                        }
                        while (pending.Count > 0)
                        {
                            Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, timeout.Token);
                            Check(frame.Header.Type != Http2FrameType.GoAway, "Soak closed connection.");
                            if (frame.Header.Type == Http2FrameType.Data)
                            {
                                Check(pending.Contains(frame.Header.StreamIdentifier) && frame.Payload.SequenceEqual(body), "Sibling response corrupted.");
                                await WindowUpdateAsync(stream, 0, frame.Payload.Length, timeout.Token);
                            }
                            if ((frame.Header.Flags & 1) != 0) pending.Remove(frame.Header.StreamIdentifier);
                        }
                        await PingAsync(stream, timeout.Token);
                    }
                }
            }
        }

        private static async Task TestApiResetAsync()
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int exceptions = 0;
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, server =>
            {
                server.Get("/api", async req => { entered.TrySetResult(true); await release.Task.WaitAsync(timeout.Token); return "late"; });
                server.Routes.Exception = (ctx, ex) => { exceptions++; return ctx.Response.Send("error"); };
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync();
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, false, timeout.Token))
                    {
                        await HandshakeAsync(stream, timeout.Token);
                        await GetAsync(stream, 1, "/api", timeout.Token);
                        await entered.Task.WaitAsync(timeout.Token);
                        await WriteAsync(stream, 3, 0, 1, new byte[] { 0, 0, 0, 8 }, timeout.Token);
                        await PingAsync(stream, timeout.Token);
                        release.TrySetResult(true);
                        await GetAsync(stream, 3, "/fast", timeout.Token);
                        await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token);
                        await PingAsync(stream, timeout.Token);
                        Check(exceptions == 0, "Reset API route invoked exception handler.");
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        private static Task TestClosedHistoryAsync()
        {
            Http2ClosedStreamHistory history = new Http2ClosedStreamHistory();
            history.Record(1, false);
            history.Record(3, true);
            history.Record(3, false);
            Check(!history.AllowsLateFrames(1), "Known normal or peer-reset closure must reject DATA.");
            Check(history.AllowsLateFrames(3), "Local reset must tolerate in-flight frames.");
            for (int id = 5; id <= 259; id += 2) history.Record(id, false);
            Check(history.AllowsLateFrames(1), "Evicted history must use conservative RFC 9113 minimal processing.");
            Check(history.AllowsLateFrames(3), "Eviction must not reject in-flight frames after local reset.");
            Check(!history.AllowsLateFrames(259), "Recent normal closure lost its reason.");
            return Task.CompletedTask;
        }

        private static async Task TestClosedDataAsync(int closure, bool tls)
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/fast", ctx => ctx.Response.Send("fast"));
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hold", async ctx =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                    await ctx.Response.Send("late").ConfigureAwait(false);
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token).ConfigureAwait(false))
                    {
                        await HandshakeAsync(stream, timeout.Token).ConfigureAwait(false);
                        if (closure == 0)
                        {
                            await GetAsync(stream, 1, "/fast", timeout.Token).ConfigureAwait(false);
                            await ReadUntilAsync(stream, f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                        }
                        else if (closure == 1)
                        {
                            await WriteAsync(stream, 1, 4, 1, Http2SharedTestUtilities.BuildRequestHeaderBlock("POST", "http", "localhost", "/upload"), timeout.Token).ConfigureAwait(false);
                            await WriteAsync(stream, 3, 0, 1, new byte[] { 0, 0, 0, 8 }, timeout.Token).ConfigureAwait(false);
                        }
                        else if (closure == 2)
                        {
                            await GetAsync(stream, 1, "/hold", timeout.Token).ConfigureAwait(false);
                            await entered.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                        }
                        await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                        byte[] padded = Http2SharedTestUtilities.BuildPaddedDataPayload(new byte[10], 5);
                        await WriteAsync(stream, 0, 8, closure == 3 ? 0 : 1, padded, timeout.Token).ConfigureAwait(false);
                        List<Http2RawFrame> frames = await ReadUntilAsync(stream,
                            f => f.Header.Type == Http2FrameType.RstStream || f.Header.Type == Http2FrameType.GoAway, timeout.Token).ConfigureAwait(false);
                        Http2RawFrame error = frames.Last();
                        if (closure == 2)
                        {
                            Check(error.Header.Type == Http2FrameType.RstStream && error.Header.StreamIdentifier == 1,
                                "Half-closed remote DATA must be a stream error.");
                            Check(Http2FrameSerializer.ReadRstStreamFrame(error).ErrorCode == Http2ErrorCode.StreamClosed,
                                "Half-closed remote DATA error must be STREAM_CLOSED.");
                            await GetAsync(stream, 3, "/fast", timeout.Token).ConfigureAwait(false);
                            List<Http2RawFrame> sibling = await ReadUntilAsync(stream,
                                f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                            Check(sibling.Where(f => f.Header.Type == Http2FrameType.WindowUpdate && f.Header.StreamIdentifier == 0)
                                .Sum(f => Http2FrameSerializer.ReadWindowUpdateFrame(f).WindowSizeIncrement) == padded.Length,
                                "Rejected half-closed DATA must return full padded connection credit.");
                            await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            Check(error.Header.Type == Http2FrameType.GoAway, "Closed or stream-zero DATA must produce GOAWAY.");
                            Check(Http2FrameSerializer.ReadGoAwayFrame(error).ErrorCode == (closure == 3 ? Http2ErrorCode.ProtocolError : Http2ErrorCode.StreamClosed),
                                "Closed or stream-zero DATA error code is incorrect.");
                        }
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        private static async Task TestSelfDependentPriorityAsync(int scenario, bool tls)
        {
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (LoopbackServerHost host = new LoopbackServerHost(tls, true, false, server =>
            {
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/priority", async ctx =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                    await ctx.Response.Send("priority-ok").ConfigureAwait(false);
                });
            }))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                try
                {
                    using (Stream stream = await ConnectAsync(client, host.Port, tls, timeout.Token).ConfigureAwait(false))
                    {
                        await HandshakeAsync(stream, timeout.Token).ConfigureAwait(false);
                        byte[] headers = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", tls ? "https" : "http", "localhost", "/priority");
                        if (scenario == 0)
                        {
                            // Idle phantom stream 3 depends on itself, with the exclusive bit set.
                            // It must not create stream state or prevent opening lower stream 1.
                            await WriteAsync(stream, 2, 0, 3, new byte[] { 128, 0, 0, 3, 255 }, timeout.Token).ConfigureAwait(false);
                        }
                        else if (scenario == 1)
                        {
                            await WriteAsync(stream, 1, 5, 1, headers, timeout.Token).ConfigureAwait(false);
                            await entered.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                            await WriteAsync(stream, 2, 0, 1, new byte[] { 0, 0, 0, 1, 0 }, timeout.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            byte[] payload = new byte[5 + headers.Length];
                            payload[0] = 128;
                            payload[3] = 1;
                            payload[4] = 255;
                            Buffer.BlockCopy(headers, 0, payload, 5, headers.Length);
                            await WriteAsync(stream, 1, 37, 1, payload, timeout.Token).ConfigureAwait(false);
                            await entered.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                        }
                        await WriteAsync(stream, 6, 0, 0, new byte[8], timeout.Token).ConfigureAwait(false);
                        List<Http2RawFrame> barrier = await ReadUntilAsync(stream,
                            f => f.Header.Type == Http2FrameType.Ping && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                        Check(!barrier.Any(f => f.Header.Type == Http2FrameType.RstStream), "Self-dependent priority caused a stream reset.");
                        if (scenario == 0) await WriteAsync(stream, 1, 5, 1, headers, timeout.Token).ConfigureAwait(false);
                        release.TrySetResult(true);
                        List<Http2RawFrame> response = await ReadUntilAsync(stream,
                            f => f.Header.StreamIdentifier == 1 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                        Check(!response.Any(f => f.Header.Type == Http2FrameType.RstStream), "Self-dependent priority reset the response.");
                        Check(response.Where(f => f.Header.Type == Http2FrameType.Headers)
                            .SelectMany(f => WatsonWebserver.Core.Hpack.HpackCodec.Decode(f.Payload))
                            .Any(h => h.Name == ":status" && h.Value == "200"), "Self-dependent priority changed response status.");
                        Check(Encoding.UTF8.GetString(response.Where(f => f.Header.Type == Http2FrameType.Data)
                            .SelectMany(f => f.Payload).ToArray()) == "priority-ok", "Self-dependent priority changed response body.");
                        await GetAsync(stream, 3, "/priority", timeout.Token).ConfigureAwait(false);
                        List<Http2RawFrame> sibling = await ReadUntilAsync(stream,
                            f => f.Header.StreamIdentifier == 3 && (f.Header.Flags & 1) != 0, timeout.Token).ConfigureAwait(false);
                        Check(!sibling.Any(f => f.Header.Type == Http2FrameType.RstStream), "Connection reuse failed after self-dependent priority.");
                        await PingAsync(stream, timeout.Token).ConfigureAwait(false);
                    }
                }
                finally { release.TrySetResult(true); }
            }
        }

        internal static async Task WriteAsync(Stream stream, byte type, byte flags, int id, byte[] payload, CancellationToken token)
        {
            Http2RawFrame frame = new Http2RawFrame(new Http2FrameHeader { Type = (Http2FrameType)type, Flags = flags, StreamIdentifier = id, Length = payload.Length }, payload);
            await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(frame), token).ConfigureAwait(false);
        }

        internal static async Task<List<Http2RawFrame>> ReadUntilAsync(Stream stream, Func<Http2RawFrame, bool> predicate, CancellationToken token)
        {
            List<Http2RawFrame> frames = new List<Http2RawFrame>();
            while (true)
            {
                Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, token).ConfigureAwait(false);
                frames.Add(frame);
                if (predicate(frame)) return frames;
                Check(frame.Header.Type != Http2FrameType.GoAway, "Unexpected GOAWAY.");
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
