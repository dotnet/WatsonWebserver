namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Quic;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;
    using WatsonWebserver.Core.Http3;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared live protocol coverage used by both Test.Automated and Test.XUnit.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public static class ProtocolGapSharedTests
    {
        private static readonly JsonSerializerOptions _JsonSerializerOptions = new JsonSerializerOptions();

        /// <summary>
        /// Verifies that the HTTP/2 serialized writer produces parseable, non-interleaved responses under multiplexed load.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunHttp2WriterSerializationCorrectnessAsync()
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureCommonRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    using (TcpClient client = new TcpClient())
                    {
                        await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                        using (NetworkStream stream = client.GetStream())
                        {
                            Http2RawFrame serverSettings = await PerformHttp2ClientHandshakeAsync(stream).ConfigureAwait(false);
                            Http2RawFrame serverAcknowledgement = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

                            if (serverSettings.Header.Type != Http2FrameType.Settings)
                            {
                                throw new InvalidOperationException("Expected HTTP/2 server SETTINGS frame.");
                            }

                            if (serverAcknowledgement.Header.Type != Http2FrameType.Settings)
                            {
                                throw new InvalidOperationException("Expected HTTP/2 server SETTINGS acknowledgement.");
                            }

                            int[] delays = new int[] { 120, 10, 90, 30, 70, 50 };

                            for (int i = 0; i < delays.Length; i++)
                            {
                                int streamIdentifier = 1 + (i * 2);
                                string path = "/test/http2-delay/" + delays[i].ToString();
                                byte[] requestHeaderBytes = BuildHttp2RequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), path);
                                Http2RawFrame requestFrame = new Http2RawFrame(
                                    new Http2FrameHeader
                                    {
                                        Length = requestHeaderBytes.Length,
                                        Type = Http2FrameType.Headers,
                                        Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                                        StreamIdentifier = streamIdentifier
                                    },
                                    requestHeaderBytes);

                                byte[] wireBytes = Http2FrameSerializer.SerializeFrame(requestFrame);
                                await stream.WriteAsync(wireBytes, 0, wireBytes.Length).ConfigureAwait(false);
                            }

                            await stream.FlushAsync().ConfigureAwait(false);

                            List<Http2CompletedResponse> responses = await ReadHttp2ResponsesAsync(stream, delays.Length).ConfigureAwait(false);
                            if (responses.Count != delays.Length)
                            {
                                throw new InvalidOperationException("Expected " + delays.Length.ToString() + " multiplexed HTTP/2 responses.");
                            }

                            Dictionary<int, string> expectedBodies = new Dictionary<int, string>();
                            for (int i = 0; i < delays.Length; i++)
                            {
                                expectedBodies[1 + (i * 2)] = "delay-" + delays[i].ToString();
                            }

                            for (int i = 0; i < responses.Count; i++)
                            {
                                Http2CompletedResponse response = responses[i];
                                if (!expectedBodies.TryGetValue(response.StreamIdentifier, out string expectedBody))
                                {
                                    throw new InvalidOperationException("Received unexpected HTTP/2 stream identifier " + response.StreamIdentifier.ToString() + ".");
                                }

                                if (response.Response.Headers.Get(":status") != "200")
                                {
                                    throw new InvalidOperationException("HTTP/2 multiplexed response did not report status 200.");
                                }

                                if (!String.Equals(response.Response.BodyString, expectedBody, StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException("HTTP/2 multiplexed response body mismatch for stream " + response.StreamIdentifier.ToString() + ".");
                                }
                            }
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that HTTP/3 transport flow control can carry a large request and response body without corruption.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunHttp3TransportBackpressureAsync()
        {
            if (!QuicListener.IsSupported)
            {
                return;
            }

            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = new LoopbackServerHost(true, false, true, ConfigureCommonRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    byte[] requestBody = CreateLargePayload(128 * 1024);

                    await using (QuicConnection connection = await ConnectHttp3ClientAsync(host.Port).ConfigureAwait(false))
                    {
                        await PerformHttp3ClientHandshakeAsync(connection).ConfigureAwait(false);
                        Http3MessageBody response = await SendHttp3RequestAsync(connection, "POST", "localhost:" + host.Port.ToString(), "/test/http3-echo-large", requestBody, null, null).ConfigureAwait(false);

                        NameValueCollection responseHeaders = DecodeHttp3Headers(response.Headers.HeaderBlock);
                        if (responseHeaders.Get(":status") != "200")
                        {
                            throw new InvalidOperationException("HTTP/3 large echo returned unexpected status.");
                        }

                        byte[] responseBodyBytes = response.BodyOrNull != null ? response.Body.ToArray() : Array.Empty<byte>();

                        if (responseBodyBytes.Length != requestBody.Length)
                        {
                            throw new InvalidOperationException("HTTP/3 large echo returned an unexpected body length.");
                        }

                        for (int i = 0; i < requestBody.Length; i++)
                        {
                            if (responseBodyBytes[i] != requestBody[i])
                            {
                                throw new InvalidOperationException("HTTP/3 large echo corrupted payload content.");
                            }
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that aborting one HTTP/3 request stream does not prevent a sibling stream on the same connection from completing.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunHttp3SiblingStreamSurvivalAsync()
        {
            if (!QuicListener.IsSupported)
            {
                return;
            }

            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = new LoopbackServerHost(true, false, true, ConfigureCommonRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await using (QuicConnection connection = await ConnectHttp3ClientAsync(host.Port).ConfigureAwait(false))
                    {
                        await PerformHttp3ClientHandshakeAsync(connection).ConfigureAwait(false);

                        QuicStream slowStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, CancellationToken.None).ConfigureAwait(false);
                        QuicStream fastStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, CancellationToken.None).ConfigureAwait(false);
                        try
                        {
                            await WriteHttp3RequestAsync(slowStream, "GET", "localhost:" + host.Port.ToString(), "/test/http2-delay/250", null, null, null).ConfigureAwait(false);
                            await WriteHttp3RequestAsync(fastStream, "GET", "localhost:" + host.Port.ToString(), "/test/get", null, null, null).ConfigureAwait(false);
                            await Task.Delay(25).ConfigureAwait(false);
                            slowStream.Abort(QuicAbortDirection.Read, 0);
                            slowStream.Abort(QuicAbortDirection.Write, 0);
                            Http3MessageBody response = await Http3MessageSerializer.ReadMessageAsync(fastStream, CancellationToken.None).ConfigureAwait(false);
                            NameValueCollection responseHeaders = DecodeHttp3Headers(response.Headers.HeaderBlock);
                            string responseBody = response.BodyOrNull != null ? Encoding.UTF8.GetString(response.Body.ToArray()) : String.Empty;

                            if (responseHeaders.Get(":status") != "200")
                            {
                                throw new InvalidOperationException("HTTP/3 sibling stream request returned unexpected status.");
                            }

                            if (!String.Equals(responseBody, "GET response", StringComparison.Ordinal))
                            {
                                throw new InvalidOperationException("HTTP/3 sibling stream did not complete successfully after sibling abort.");
                            }
                        }
                        finally
                        {
                            await fastStream.DisposeAsync().ConfigureAwait(false);
                            await slowStream.DisposeAsync().ConfigureAwait(false);
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies structured authentication and middleware parity across HTTP/1.1, HTTP/2, and HTTP/3.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunCrossProtocolAuthSessionEventParityAsync()
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = new LoopbackServerHost(true, true, QuicListener.IsSupported, ConfigureCommonRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertSecureRouteAsync(host.BaseAddress, HttpVersion.Version11).ConfigureAwait(false);
                    await AssertSecureRouteAsync(host.BaseAddress, HttpVersion.Version20).ConfigureAwait(false);

                    if (QuicListener.IsSupported)
                    {
                        await AssertSecureRouteAsync(host.BaseAddress, HttpVersion.Version30).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that a single TLS-enabled server can interoperate with HTTP/1.1, HTTP/2, and HTTP/3 clients.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RunMixedVersionClientInteroperabilityAsync()
        {
            await ExecuteWithRetryAsync(async () =>
            {
                using (LoopbackServerHost host = new LoopbackServerHost(true, true, QuicListener.IsSupported, ConfigureCommonRoutes))
                {
                    await host.StartAsync().ConfigureAwait(false);

                    await AssertGetInteropAsync(host.BaseAddress, HttpVersion.Version11).ConfigureAwait(false);
                    await AssertGetInteropAsync(host.BaseAddress, HttpVersion.Version20).ConfigureAwait(false);

                    if (QuicListener.IsSupported)
                    {
                        await AssertGetInteropAsync(host.BaseAddress, HttpVersion.Version30).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        private static void ConfigureCommonRoutes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Middleware.Add(async (ctx, next, token) =>
            {
                ctx.Response.Headers["X-Test-Middleware"] = "ran";
                await next().ConfigureAwait(false);
            });

            server.Routes.AuthenticateApiRequest = async (ctx) =>
            {
                string authorization = ctx.Request.RetrieveHeaderValue("Authorization");
                if (String.Equals(authorization, "Bearer test-token", StringComparison.Ordinal))
                {
                    return new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = AuthorizationResultEnum.Permitted,
                        Metadata = new ProtocolGapResponse
                        {
                            UserId = 42,
                            Role = "Admin"
                        }
                    };
                }

                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                };
            };

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/get", async (ctx) =>
            {
                await ctx.Response.Send("GET response", ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Parameter.Add(CoreHttpMethod.GET, "/test/http2-delay/{ms}", async (ctx) =>
            {
                int delay = 0;
                int.TryParse(ctx.Request.Url.Parameters["ms"], out delay);
                if (delay > 0)
                {
                    await Task.Delay(delay, ctx.Token).ConfigureAwait(false);
                }

                await ctx.Response.Send("delay-" + delay.ToString(), ctx.Token).ConfigureAwait(false);
            });

            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.POST, "/test/http3-echo-large", async (ctx) =>
            {
                byte[] body = ctx.Request.DataAsBytes;
                ctx.Response.ContentType = "application/octet-stream";
                await ctx.Response.Send(body, ctx.Token).ConfigureAwait(false);
            });

            server.Get("/test/secure", async (req) =>
            {
                ProtocolGapResponse metadata = req.Metadata as ProtocolGapResponse;
                ProtocolGapResponse response = new ProtocolGapResponse();
                response.UserId = metadata != null ? metadata.UserId : 0;
                response.Role = metadata != null ? metadata.Role : String.Empty;
                response.Middleware = req.Http.Response.Headers["X-Test-Middleware"] ?? String.Empty;
                response.Path = req.Http.Request.Url.RawWithoutQuery;
                return response;
            }, auth: true);
        }

        private static async Task AssertSecureRouteAsync(Uri baseAddress, Version version)
        {
            using (HttpClient client = CreateTlsHttpClient(version))
            using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(baseAddress, "/test/secure")))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer test-token");
                request.Headers.TryAddWithoutValidation("X-Test-Request", "parity");

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                ProtocolGapResponse payload = JsonSerializer.Deserialize<ProtocolGapResponse>(responseJson, _JsonSerializerOptions);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Secure route failed for protocol " + version.ToString() + ".");
                }

                if (payload == null)
                {
                    throw new InvalidOperationException("Secure route returned no JSON payload for protocol " + version.ToString() + ".");
                }

                if (payload.UserId != 42 || !String.Equals(payload.Role, "Admin", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Structured authentication metadata mismatch for protocol " + version.ToString() + ".");
                }

                if (!String.Equals(payload.Middleware, "ran", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Middleware marker mismatch for protocol " + version.ToString() + ".");
                }

                if (!String.Equals(payload.Path, "/test/secure", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Secure route path mismatch for protocol " + version.ToString() + ".");
                }
            }
        }

        private static async Task AssertGetInteropAsync(Uri baseAddress, Version version)
        {
            if (version == HttpVersion.Version30)
            {
                await using (QuicConnection connection = await ConnectHttp3ClientAsync(baseAddress.Port).ConfigureAwait(false))
                {
                    await PerformHttp3ClientHandshakeAsync(connection).ConfigureAwait(false);
                    Http3MessageBody response = await SendHttp3RequestAsync(connection, "GET", "localhost:" + baseAddress.Port.ToString(), "/test/get", null, null, null).ConfigureAwait(false);
                    NameValueCollection responseHeaders = DecodeHttp3Headers(response.Headers.HeaderBlock);
                    string responseBody = response.BodyOrNull != null ? Encoding.UTF8.GetString(response.Body.ToArray()) : String.Empty;

                    if (responseHeaders.Get(":status") != "200")
                    {
                        throw new InvalidOperationException("Interop GET failed for protocol " + version.ToString() + ".");
                    }

                    if (!String.Equals(responseBody, "GET response", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Interop GET returned an unexpected payload for protocol " + version.ToString() + ".");
                    }
                }

                return;
            }

            using (HttpClient client = CreateTlsHttpClient(version))
            {
                HttpResponseMessage response = await client.GetAsync(new Uri(baseAddress, "/test/get")).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("Interop GET failed for protocol " + version.ToString() + ".");
                }

                if (!String.Equals(responseBody, "GET response", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Interop GET returned an unexpected payload for protocol " + version.ToString() + ".");
                }
            }
        }

        private static HttpClient CreateTlsHttpClient(Version version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }

        private static byte[] CreateLargePayload(int length)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));

            byte[] payload = new byte[length];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)('a' + (i % 26));
            }

            return payload;
        }

        private static async Task ExecuteWithRetryAsync(Func<Task> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));

            Exception lastException = null;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await operation().ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await Task.Delay(150).ConfigureAwait(false);
                }
            }

            throw lastException ?? new InvalidOperationException("Retryable shared protocol test failed without an exception.");
        }

        private static async Task<Http2RawFrame> PerformHttp2ClientHandshakeAsync(NetworkStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Http2Settings clientSettings = new Http2Settings();
            byte[] prefaceBytes = Http2ConnectionPreface.GetClientPrefaceBytes();
            byte[] settingsBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsFrame(clientSettings));

            await stream.WriteAsync(prefaceBytes, 0, prefaceBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(settingsBytes, 0, settingsBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            Http2RawFrame serverSettings = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

            byte[] acknowledgementBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsAcknowledgementFrame());
            await stream.WriteAsync(acknowledgementBytes, 0, acknowledgementBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            return serverSettings;
        }

        private static byte[] BuildHttp2RequestHeaderBlock(string method, string scheme, string authority, string path)
        {
            if (String.IsNullOrEmpty(method)) throw new ArgumentNullException(nameof(method));
            if (String.IsNullOrEmpty(scheme)) throw new ArgumentNullException(nameof(scheme));
            if (String.IsNullOrEmpty(authority)) throw new ArgumentNullException(nameof(authority));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            List<HpackHeaderField> requestHeaders = new List<HpackHeaderField>();
            requestHeaders.Add(new HpackHeaderField { Name = ":method", Value = method });
            requestHeaders.Add(new HpackHeaderField { Name = ":scheme", Value = scheme });
            requestHeaders.Add(new HpackHeaderField { Name = ":authority", Value = authority });
            requestHeaders.Add(new HpackHeaderField { Name = ":path", Value = path });
            return HpackCodec.Encode(requestHeaders);
        }

        private static async Task<List<Http2CompletedResponse>> ReadHttp2ResponsesAsync(NetworkStream stream, int expectedResponses)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (expectedResponses < 1) throw new ArgumentOutOfRangeException(nameof(expectedResponses));

            Dictionary<int, Http2ResponseAccumulator> responseMap = new Dictionary<int, Http2ResponseAccumulator>();
            List<Http2CompletedResponse> completedResponses = new List<Http2CompletedResponse>();

            while (completedResponses.Count < expectedResponses)
            {
                Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

                if (frame.Header.Type == Http2FrameType.Headers || frame.Header.Type == Http2FrameType.Continuation)
                {
                    Http2ResponseAccumulator accumulator = GetOrCreateAccumulator(responseMap, frame.Header.StreamIdentifier);
                    if (frame.Payload.Length > 0)
                    {
                        await accumulator.HeaderBlock.WriteAsync(frame.Payload, 0, frame.Payload.Length).ConfigureAwait(false);
                    }

                    bool endHeaders = (frame.Header.Flags & (byte)Http2FrameFlags.EndHeaders) == (byte)Http2FrameFlags.EndHeaders;
                    if (endHeaders)
                    {
                        List<HpackHeaderField> decodedHeaderFields = HpackCodec.Decode(accumulator.HeaderBlock.ToArray());
                        NameValueCollection destination = accumulator.HeadersReceived ? accumulator.Response.Trailers : accumulator.Response.Headers;
                        for (int i = 0; i < decodedHeaderFields.Count; i++)
                        {
                            destination[decodedHeaderFields[i].Name] = decodedHeaderFields[i].Value;
                        }

                        accumulator.HeadersReceived = true;
                        accumulator.HeaderBlock.SetLength(0);
                    }

                    bool endStreamOnHeaders = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                    if (endStreamOnHeaders)
                    {
                        accumulator.Response.BodyString = Encoding.UTF8.GetString(accumulator.Body.ToArray());
                        completedResponses.Add(new Http2CompletedResponse
                        {
                            StreamIdentifier = frame.Header.StreamIdentifier,
                            Response = accumulator.Response
                        });
                        responseMap.Remove(frame.Header.StreamIdentifier);
                    }
                }
                else if (frame.Header.Type == Http2FrameType.Data)
                {
                    Http2ResponseAccumulator accumulator = GetOrCreateAccumulator(responseMap, frame.Header.StreamIdentifier);
                    if (frame.Payload.Length > 0)
                    {
                        await accumulator.Body.WriteAsync(frame.Payload, 0, frame.Payload.Length).ConfigureAwait(false);
                    }

                    bool endStreamOnData = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                    if (endStreamOnData)
                    {
                        accumulator.Response.BodyString = Encoding.UTF8.GetString(accumulator.Body.ToArray());
                        completedResponses.Add(new Http2CompletedResponse
                        {
                            StreamIdentifier = frame.Header.StreamIdentifier,
                            Response = accumulator.Response
                        });
                        responseMap.Remove(frame.Header.StreamIdentifier);
                    }
                }
                else if (frame.Header.Type == Http2FrameType.Settings)
                {
                    bool isAcknowledgement = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                    if (!isAcknowledgement)
                    {
                        byte[] acknowledgementBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsAcknowledgementFrame());
                        await stream.WriteAsync(acknowledgementBytes, 0, acknowledgementBytes.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);
                    }
                }
                else if (frame.Header.Type == Http2FrameType.WindowUpdate)
                {
                    continue;
                }
                else
                {
                    throw new IOException("Unexpected HTTP/2 frame type while reading multiplexed responses.");
                }
            }

            return completedResponses;
        }

        private static Http2ResponseAccumulator GetOrCreateAccumulator(Dictionary<int, Http2ResponseAccumulator> responseMap, int streamIdentifier)
        {
            if (responseMap == null) throw new ArgumentNullException(nameof(responseMap));
            if (streamIdentifier < 1) throw new ArgumentOutOfRangeException(nameof(streamIdentifier));

            if (responseMap.TryGetValue(streamIdentifier, out Http2ResponseAccumulator existingAccumulator))
            {
                return existingAccumulator;
            }

            Http2ResponseAccumulator accumulator = new Http2ResponseAccumulator();
            responseMap[streamIdentifier] = accumulator;
            return accumulator;
        }

        private static async Task<QuicConnection> ConnectHttp3ClientAsync(int port)
        {
            QuicClientConnectionOptions options = new QuicClientConnectionOptions();
            options.RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port);
            options.MaxInboundBidirectionalStreams = 16;
            options.MaxInboundUnidirectionalStreams = 4;
            options.DefaultCloseErrorCode = 0;
            options.DefaultStreamErrorCode = 0;

            SslClientAuthenticationOptions authenticationOptions = new SslClientAuthenticationOptions();
            authenticationOptions.TargetHost = "localhost";
            authenticationOptions.ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 };
            authenticationOptions.EnabledSslProtocols = SslProtocols.Tls13;
            authenticationOptions.RemoteCertificateValidationCallback = delegate { return true; };
            options.ClientAuthenticationOptions = authenticationOptions;

            return await QuicConnection.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task<Http3Settings> PerformHttp3ClientHandshakeAsync(QuicConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            await WriteHttp3ControlBootstrapStreamAsync(connection, new Http3Settings()).ConfigureAwait(false);
            await WriteHttp3BootstrapStreamAsync(connection, Http3StreamType.QpackEncoder, Array.Empty<byte>()).ConfigureAwait(false);
            await WriteHttp3BootstrapStreamAsync(connection, Http3StreamType.QpackDecoder, Array.Empty<byte>()).ConfigureAwait(false);

            QuicStream controlStream = await AcceptHttp3ServerBootstrapControlStreamAsync(connection).ConfigureAwait(false);
            try
            {
                Http3ControlStreamPayload peerPayload = await ReadHttp3ControlStreamBootstrapAsync(controlStream).ConfigureAwait(false);
                return peerPayload.Settings;
            }
            finally
            {
                await controlStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task<Http3MessageBody> SendHttp3RequestAsync(
            QuicConnection connection,
            string method,
            string authority,
            string path,
            byte[] body,
            List<Http3HeaderField> additionalHeaders,
            List<Http3HeaderField> trailerHeaders)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (String.IsNullOrEmpty(method)) throw new ArgumentNullException(nameof(method));
            if (String.IsNullOrEmpty(authority)) throw new ArgumentNullException(nameof(authority));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            QuicStream requestStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, CancellationToken.None).ConfigureAwait(false);
            try
            {
                await WriteHttp3RequestAsync(requestStream, method, authority, path, body, additionalHeaders, trailerHeaders).ConfigureAwait(false);
                return await Http3MessageSerializer.ReadMessageAsync(requestStream, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await requestStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task WriteHttp3RequestAsync(
            QuicStream requestStream,
            string method,
            string authority,
            string path,
            byte[] body,
            List<Http3HeaderField> additionalHeaders,
            List<Http3HeaderField> trailerHeaders)
        {
            if (requestStream == null) throw new ArgumentNullException(nameof(requestStream));
            if (String.IsNullOrEmpty(method)) throw new ArgumentNullException(nameof(method));
            if (String.IsNullOrEmpty(authority)) throw new ArgumentNullException(nameof(authority));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            headers.Add(new Http3HeaderField { Name = ":method", Value = method });
            headers.Add(new Http3HeaderField { Name = ":scheme", Value = "https" });
            headers.Add(new Http3HeaderField { Name = ":authority", Value = authority });
            headers.Add(new Http3HeaderField { Name = ":path", Value = path });

            if (additionalHeaders != null)
            {
                for (int i = 0; i < additionalHeaders.Count; i++)
                {
                    headers.Add(additionalHeaders[i]);
                }
            }

            byte[] headerBytes = Http3HeaderCodec.Encode(headers);
            byte[] trailerBytes = trailerHeaders != null ? Http3HeaderCodec.Encode(trailerHeaders) : null;
            byte[] payload = Http3MessageSerializer.SerializeMessage(headerBytes, body, trailerBytes);
            await requestStream.WriteAsync(payload, true, CancellationToken.None).ConfigureAwait(false);
        }

        private static NameValueCollection DecodeHttp3Headers(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
            List<Http3HeaderField> decodedHeaders = Http3HeaderCodec.Decode(payload);
            for (int i = 0; i < decodedHeaders.Count; i++)
            {
                headers.Add(decodedHeaders[i].Name, decodedHeaders[i].Value);
            }

            return headers;
        }

        private static async Task<QuicStream> AcceptHttp3ServerBootstrapControlStreamAsync(QuicConnection connection)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            QuicStream controlStream = null;
            int receivedBootstrapStreams = 0;

            while (receivedBootstrapStreams < 3)
            {
                QuicStream peerStream = await connection.AcceptInboundStreamAsync(CancellationToken.None).ConfigureAwait(false);
                long streamType = await Http3VarInt.ReadAsync(peerStream, CancellationToken.None).ConfigureAwait(false);

                if (streamType == (long)Http3StreamType.Control)
                {
                    controlStream = peerStream;
                    receivedBootstrapStreams++;
                    continue;
                }

                try
                {
                    if (streamType == (long)Http3StreamType.QpackEncoder || streamType == (long)Http3StreamType.QpackDecoder)
                    {
                    }
                    else
                    {
                        throw new IOException("Unexpected HTTP/3 bootstrap stream type " + streamType.ToString() + ".");
                    }

                    receivedBootstrapStreams++;
                }
                finally
                {
                    await peerStream.DisposeAsync().ConfigureAwait(false);
                }
            }

            if (controlStream == null) throw new IOException("HTTP/3 peer bootstrap did not include a control stream.");
            return controlStream;
        }

        private static async Task<Http3ControlStreamPayload> ReadHttp3ControlStreamBootstrapAsync(QuicStream controlStream)
        {
            if (controlStream == null) throw new ArgumentNullException(nameof(controlStream));

            Http3Frame settingsFrame = await Http3FrameSerializer.ReadFrameAsync(controlStream, CancellationToken.None).ConfigureAwait(false);
            if (settingsFrame.Header.Type != (long)Http3FrameType.Settings)
            {
                throw new IOException("Peer control stream did not begin with SETTINGS.");
            }

            Http3ControlStreamPayload payload = new Http3ControlStreamPayload();
            payload.StreamType = Http3StreamType.Control;
            payload.Settings = Http3SettingsSerializer.ReadSettingsFrame(settingsFrame);
            return payload;
        }

        private static async Task WriteHttp3ControlBootstrapStreamAsync(QuicConnection connection, Http3Settings settings)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            QuicStream controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, CancellationToken.None).ConfigureAwait(false);
            try
            {
                byte[] payload = Http3ControlStreamSerializer.Serialize(settings);
                await controlStream.WriteAsync(payload, true, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await controlStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        private static async Task WriteHttp3BootstrapStreamAsync(QuicConnection connection, Http3StreamType streamType, byte[] payload)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            QuicStream controlStream = await connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, CancellationToken.None).ConfigureAwait(false);
            try
            {
                byte[] streamTypeBytes = Http3VarInt.Encode((long)streamType);
                byte[] payloadBytes = payload ?? Array.Empty<byte>();
                byte[] combinedPayload = new byte[streamTypeBytes.Length + payloadBytes.Length];
                Buffer.BlockCopy(streamTypeBytes, 0, combinedPayload, 0, streamTypeBytes.Length);
                if (payloadBytes.Length > 0)
                {
                    Buffer.BlockCopy(payloadBytes, 0, combinedPayload, streamTypeBytes.Length, payloadBytes.Length);
                }

                await controlStream.WriteAsync(combinedPayload, true, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                await controlStream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
