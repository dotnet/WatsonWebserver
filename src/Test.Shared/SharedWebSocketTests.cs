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

    /// <summary>
    /// Shared websocket integration tests.
    /// </summary>
    public static class SharedWebSocketTests
    {
        /// <summary>
        /// Get the shared websocket tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static SharedNamedTestCase[] GetTests()
        {
            return new[]
            {
                new SharedNamedTestCase("WebSocket :: Duplicate route registrations that are ambiguous are rejected", TestDuplicateRouteRegistrationsRejectedAsync),
                new SharedNamedTestCase("WebSocket :: One-way client-to-server text works", TestOneWayClientToServerTextAsync),
                new SharedNamedTestCase("WebSocket :: One-way client-to-server binary works", TestOneWayClientToServerBinaryAsync),
                new SharedNamedTestCase("WebSocket :: One-way server-to-client binary works", TestOneWayServerToClientBinaryAsync),
                new SharedNamedTestCase("WebSocket :: Bidirectional request-reply works", TestBidirectionalRequestReplyAsync),
                new SharedNamedTestCase("WebSocket :: Bidirectional alternating ping-pong traffic works", TestAlternatingPingPongTrafficAsync),
                new SharedNamedTestCase("WebSocket :: Full-duplex concurrent chatter works", TestFullDuplexConcurrentChatterAsync),
                new SharedNamedTestCase("WebSocket :: Burst traffic with many small messages works", TestBurstTrafficAsync),
                new SharedNamedTestCase("WebSocket :: Sustained medium-message traffic works", TestSustainedMediumTrafficAsync),
                new SharedNamedTestCase("WebSocket :: Fragmented message assembly works", TestFragmentedMessageAssemblyAsync),
                new SharedNamedTestCase("WebSocket :: Slow consumer scenario works", TestSlowConsumerScenarioAsync),
                new SharedNamedTestCase("WebSocket :: HTTP/1.1 loopback text echo succeeds", TestHttp11TextEchoAsync),
                new SharedNamedTestCase("WebSocket :: Same-path HTTP and WebSocket dispatch by upgrade", TestSamePathDispatchAsync),
                new SharedNamedTestCase("WebSocket :: Queryless websocket requests survive observability event materialization", TestQuerylessWebSocketWithObservabilityHandlersAsync),
                new SharedNamedTestCase("WebSocket :: Exception event materialization survives queryless websocket failures", TestQuerylessWebSocketExceptionEventMaterializationAsync),
                new SharedNamedTestCase("WebSocket :: Session enumeration and disconnect API work", TestSessionEnumerationAndDisconnectAsync),
                new SharedNamedTestCase("WebSocket :: Post-auth route sees authentication metadata", TestPostAuthWebSocketRouteAsync),
                new SharedNamedTestCase("WebSocket :: Client supplied GUID honored only when enabled", TestClientSuppliedGuidOptInAsync),
                new SharedNamedTestCase("WebSocket :: Observability events fire in start-end order", TestObservabilityEventsAsync),
                new SharedNamedTestCase("WebSocket :: Unsupported version handshake is rejected", TestUnsupportedVersionHandshakeRejectedAsync),
                new SharedNamedTestCase("WebSocket :: Server stop closes active websocket sessions", TestServerStopClosesSessionsAsync),
                new SharedNamedTestCase("WebSocket :: Near-limit message transfer succeeds", TestNearLimitMessageTransferAsync),
                new SharedNamedTestCase("WebSocket :: Binary payload integrity is preserved", TestBinaryPayloadIntegrityAsync),
                new SharedNamedTestCase("WebSocket :: Concurrent server sends arrive as complete messages", TestConcurrentServerSendsAsync),
                new SharedNamedTestCase("WebSocket :: Abrupt client disconnect cleans registry", TestAbruptClientDisconnectCleansRegistryAsync),
                new SharedNamedTestCase("WebSocket :: Missing Sec-WebSocket-Key is rejected without registry leak", TestMissingKeyHandshakeRejectedAsync),
                new SharedNamedTestCase("WebSocket :: Wrong HTTP method for websocket initiation is rejected", TestWrongMethodHandshakeRejectedAsync),
                new SharedNamedTestCase("WebSocket :: Route parameters and request metadata survive upgrade", TestRouteParametersAndRequestMetadataAsync),
                new SharedNamedTestCase("WebSocket :: Unsupported subprotocol requests are handled safely", TestUnsupportedSubprotocolRequestsHandledAsync),
                new SharedNamedTestCase("WebSocket :: Session statistics advance correctly", TestSessionStatisticsAsync),
                new SharedNamedTestCase("WebSocket :: Session statistics remain correct under concurrent sends", TestConcurrentSendStatisticsAsync),
                new SharedNamedTestCase("WebSocket :: Invalid client supplied GUID header is handled safely", TestInvalidClientGuidHandledSafelyAsync),
                new SharedNamedTestCase("WebSocket :: Oversized message is rejected and cleaned up", TestOversizedMessageRejectedAsync),
                new SharedNamedTestCase("WebSocket :: Route handler exception closes the session cleanly", TestRouteHandlerExceptionClosesSessionAsync),
                new SharedNamedTestCase("WebSocket :: Non-websocket requests on websocket-only paths stay on normal HTTP behavior", TestNonWebSocketRequestsOnWebSocketOnlyPathAsync),
                new SharedNamedTestCase("WebSocket :: Concurrent receive attempts are rejected per session", TestConcurrentReceiveRejectedAsync),
                new SharedNamedTestCase("WebSocket :: Client receive cancellation drains the session registry", TestClientReceiveCancellationAsync),
                new SharedNamedTestCase("WebSocket :: Client cancellation during server send drains the session registry", TestClientCancellationDuringSendAsync),
                new SharedNamedTestCase("WebSocket :: Half-open network failure drains the session registry", TestHalfOpenNetworkFailureCleanupAsync),
                new SharedNamedTestCase("WebSocket :: Session end event fires once per session", TestSessionEndedEventFiresOnceAsync),
                new SharedNamedTestCase("WebSocket :: Repeated connect-disconnect cycles drain the registry", TestRepeatedConnectDisconnectCyclesAsync),
                new SharedNamedTestCase("WebSocket :: UTF-8 text handling is preserved", TestUtf8TextHandlingAsync),
                new SharedNamedTestCase("WebSocket :: TLS websocket loopback succeeds", TestTlsWebSocketLoopbackAsync),
                new SharedNamedTestCase("WebSocket :: Close state is retained after session shutdown", TestCloseStateRetainedAsync),
                new SharedNamedTestCase("WebSocket :: Mixed text and binary traffic work on one session", TestMixedTextAndBinaryTrafficAsync),
                new SharedNamedTestCase("WebSocket :: Server initiated close reaches the client cleanly", TestServerInitiatedCloseAsync),
                new SharedNamedTestCase("WebSocket :: Client initiated close drains the session registry", TestClientInitiatedCloseAsync),
                new SharedNamedTestCase("WebSocket :: Concurrent many-session traffic works", TestConcurrentManySessionTrafficAsync),
                new SharedNamedTestCase("HTTP :: Queryless requests survive observability event materialization", TestQuerylessHttpWithObservabilityHandlersAsync)
            };
        }

        private static Task TestDuplicateRouteRegistrationsRejectedAsync()
        {
            using (Webserver server = new Webserver(new WebserverSettings(), ctx => Task.CompletedTask))
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/dup", (ctx, session) => Task.CompletedTask);

                AssertThrows<InvalidOperationException>(
                    () => server.WebSocket("/ws/dup", (ctx, session) => Task.CompletedTask),
                    "Expected duplicate static websocket registrations to be rejected.");

                server.WebSocket("/ws/{id}", (ctx, session) => Task.CompletedTask);

                AssertThrows<InvalidOperationException>(
                    () => server.WebSocket("/ws/{id}", (ctx, session) => Task.CompletedTask),
                    "Expected duplicate parameterized websocket registrations to be rejected.");
            }

            return Task.CompletedTask;
        }

        private static async Task TestOneWayClientToServerTextAsync()
        {
            TaskCompletionSource<string> messageSource = CreateTaskCompletionSource<string>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/oneway-text", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    messageSource.TrySetResult(message?.Text);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/oneway-text"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("oneway-text")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string received = await messageSource.Task.ConfigureAwait(false);
                    AssertEquals("oneway-text", received, "Expected client-to-server text payload to reach the route handler.");
                }
            }
        }

        private static async Task TestOneWayClientToServerBinaryAsync()
        {
            TaskCompletionSource<byte[]> messageSource = CreateTaskCompletionSource<byte[]>();
            byte[] payload = new byte[] { 10, 20, 30, 40, 50 };

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/oneway-binary", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    messageSource.TrySetResult(message?.Data);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/oneway-binary"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, timeout.Token).ConfigureAwait(false);

                    byte[] received = await messageSource.Task.ConfigureAwait(false);
                    AssertEquals(payload.Length, received.Length, "Expected client-to-server binary payload length to match.");
                    for (int i = 0; i < payload.Length; i++)
                    {
                        if (payload[i] != received[i]) throw new InvalidOperationException("Unexpected client-to-server binary payload content at index " + i + ".");
                    }
                }
            }
        }

        private static async Task TestOneWayServerToClientBinaryAsync()
        {
            byte[] payload = new byte[] { 1, 3, 5, 7, 9 };

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/server-binary", async (ctx, session) =>
                {
                    await session.SendBinaryAsync(payload, ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/server-binary"), timeout.Token).ConfigureAwait(false);
                    byte[] response = await ReceiveBinaryAsync(client, timeout.Token).ConfigureAwait(false);

                    AssertEquals(payload.Length, response.Length, "Expected one-way server binary payload length to match.");
                    for (int i = 0; i < payload.Length; i++)
                    {
                        if (payload[i] != response[i]) throw new InvalidOperationException("Unexpected one-way server binary payload content at index " + i + ".");
                    }
                }
            }
        }

        private static async Task TestBidirectionalRequestReplyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/request-reply", async (ctx, session) =>
                {
                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync("reply:" + message.Text, ctx.Token).ConfigureAwait(false);
                        break;
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/request-reply"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("request")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("reply:request", response, "Expected bidirectional request-reply websocket behavior.");
                }
            }
        }

        private static async Task TestAlternatingPingPongTrafficAsync()
        {
            const int rounds = 5;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/ping-pong", async (ctx, session) =>
                {
                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync("pong:" + message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/ping-pong"), timeout.Token).ConfigureAwait(false);

                    for (int i = 0; i < rounds; i++)
                    {
                        string payload = "ping-" + i;
                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                        string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                        AssertEquals("pong:" + payload, response, "Expected alternating ping-pong response.");
                    }
                }
            }
        }

        private static async Task TestFullDuplexConcurrentChatterAsync()
        {
            const int messageCount = 6;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/full-duplex", async (ctx, session) =>
                {
                    Task heartbeatTask = Task.Run(async () =>
                    {
                        for (int i = 0; i < messageCount; i++)
                        {
                            await session.SendTextAsync("server:" + i, ctx.Token).ConfigureAwait(false);
                            await Task.Delay(25, ctx.Token).ConfigureAwait(false);
                        }
                    }, ctx.Token);

                    int receivedCount = 0;
                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync("ack:" + message.Text, ctx.Token).ConfigureAwait(false);
                        receivedCount++;
                        if (receivedCount >= messageCount) break;
                    }

                    await heartbeatTask.ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/full-duplex"), timeout.Token).ConfigureAwait(false);

                    Task sender = Task.Run(async () =>
                    {
                        for (int i = 0; i < messageCount; i++)
                        {
                            await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("client:" + i)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                            await Task.Delay(15, timeout.Token).ConfigureAwait(false);
                        }
                    }, timeout.Token);

                    HashSet<string> responses = new HashSet<string>(StringComparer.Ordinal);
                    while (responses.Count < messageCount * 2)
                    {
                        string message = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                        if (message != null) responses.Add(message);
                    }

                    await sender.ConfigureAwait(false);

                    for (int i = 0; i < messageCount; i++)
                    {
                        AssertTrue(responses.Contains("server:" + i), "Expected concurrent server chatter message server:" + i + ".");
                        AssertTrue(responses.Contains("ack:client:" + i), "Expected concurrent ack message ack:client:" + i + ".");
                    }
                }
            }
        }

        private static async Task TestBurstTrafficAsync()
        {
            const int count = 25;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/burst", async (ctx, session) =>
                {
                    int seen = 0;
                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync("burst:" + message.Text, ctx.Token).ConfigureAwait(false);
                        seen++;
                        if (seen >= count) break;
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/burst"), timeout.Token).ConfigureAwait(false);

                    for (int i = 0; i < count; i++)
                    {
                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("m" + i)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                    }

                    for (int i = 0; i < count; i++)
                    {
                        string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                        AssertEquals("burst:m" + i, response, "Expected burst response ordering to be preserved.");
                    }
                }
            }
        }

        private static async Task TestSustainedMediumTrafficAsync()
        {
            const int count = 8;
            string payload = new string('m', 1024);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/medium", async (ctx, session) =>
                {
                    int seen = 0;
                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                        seen++;
                        if (seen >= count) break;
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/medium"), timeout.Token).ConfigureAwait(false);

                    for (int i = 0; i < count; i++)
                    {
                        string outbound = payload + i;
                        await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outbound)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                        string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                        AssertEquals(outbound, response, "Expected sustained medium websocket payload to round-trip unchanged.");
                    }
                }
            }
        }

        private static async Task TestFragmentedMessageAssemblyAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/fragmented", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/fragmented"), timeout.Token).ConfigureAwait(false);

                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("frag-")), WebSocketMessageType.Text, false, timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("ment-")), WebSocketMessageType.Text, false, timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("ed")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("frag-ment-ed", response, "Expected fragmented websocket frames to be reassembled into one message.");
                }
            }
        }

        private static async Task TestSlowConsumerScenarioAsync()
        {
            const int count = 12;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/slow-consumer", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage trigger = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (trigger == null) return;

                    for (int i = 0; i < count; i++)
                    {
                        await session.SendTextAsync("queued:" + i, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/slow-consumer"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("start")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    await Task.Delay(300, timeout.Token).ConfigureAwait(false);

                    for (int i = 0; i < count; i++)
                    {
                        string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                        AssertEquals("queued:" + i, response, "Expected queued websocket messages to remain readable for a slow consumer.");
                    }
                }
            }
        }

        private static async Task TestHttp11TextEchoAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureEchoRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/echo"), timeout.Token).ConfigureAwait(false);

                    byte[] sendBytes = Encoding.UTF8.GetBytes("hello");
                    await client.SendAsync(new ArraySegment<byte>(sendBytes), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("echo:hello", responseText, "Unexpected websocket echo payload.");

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestSamePathDispatchAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureSamePathRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient httpClient = new HttpClient())
                {
                    string httpText = await httpClient.GetStringAsync("http://127.0.0.1:" + host.Port.ToString() + "/chat").ConfigureAwait(false);
                    AssertEquals("http-route", httpText, "Unexpected HTTP same-path response.");
                }

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/chat"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("ping")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("ws-route", responseText, "Unexpected websocket same-path response.");

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestQuerylessWebSocketWithObservabilityHandlersAsync()
        {
            TaskCompletionSource<string> requestUrlSource = CreateTaskCompletionSource<string>();
            TaskCompletionSource<string> responseUrlSource = CreateTaskCompletionSource<string>();
            TaskCompletionSource<Exception> exceptionSource = CreateTaskCompletionSource<Exception>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Events.RequestReceived += (sender, args) => requestUrlSource.TrySetResult(args?.Url);
                server.Events.ResponseSent += (sender, args) => responseUrlSource.TrySetResult(args?.Url);
                server.Events.ExceptionEncountered += (sender, args) => exceptionSource.TrySetResult(args?.Exception);
                server.WebSocket("/ws/queryless-events", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync("ack:" + message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/queryless-events"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("hello")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string response = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("ack:hello", response, "Expected queryless websocket request to succeed with observability handlers attached.");

                    string requestUrl = await requestUrlSource.Task.ConfigureAwait(false);
                    string responseUrl = await responseUrlSource.Task.ConfigureAwait(false);
                    AssertTrue(requestUrl.EndsWith("/ws/queryless-events", StringComparison.Ordinal), "Expected request event materialization to preserve the queryless websocket URL.");
                    AssertTrue(responseUrl.EndsWith("/ws/queryless-events", StringComparison.Ordinal), "Expected response event materialization to preserve the queryless websocket URL.");
                    AssertTrue(!requestUrl.Contains('?', StringComparison.Ordinal), "Expected the queryless websocket request event URL to remain queryless.");
                    AssertTrue(!responseUrl.Contains('?', StringComparison.Ordinal), "Expected the queryless websocket response event URL to remain queryless.");
                    AssertTrue(!exceptionSource.Task.IsCompleted, "Did not expect exception events for a successful queryless websocket request.");
                }
            }
        }

        private static async Task TestQuerylessHttpWithObservabilityHandlersAsync()
        {
            TaskCompletionSource<string> requestUrlSource = CreateTaskCompletionSource<string>();
            TaskCompletionSource<string> responseUrlSource = CreateTaskCompletionSource<string>();
            TaskCompletionSource<Exception> exceptionSource = CreateTaskCompletionSource<Exception>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Events.RequestReceived += (sender, args) => requestUrlSource.TrySetResult(args?.Url);
                server.Events.ResponseSent += (sender, args) => responseUrlSource.TrySetResult(args?.Url);
                server.Events.ExceptionEncountered += (sender, args) => exceptionSource.TrySetResult(args?.Exception);
                server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/http/queryless-events", async ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.Send("ok", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    string response = await client.GetStringAsync(new Uri("http://127.0.0.1:" + host.Port.ToString() + "/http/queryless-events"), timeout.Token).ConfigureAwait(false);
                    AssertEquals("ok", response, "Expected queryless HTTP request to succeed with observability handlers attached.");

                    string requestUrl = await requestUrlSource.Task.ConfigureAwait(false);
                    string responseUrl = await responseUrlSource.Task.ConfigureAwait(false);
                    AssertTrue(requestUrl.EndsWith("/http/queryless-events", StringComparison.Ordinal), "Expected request event materialization to preserve the queryless HTTP URL.");
                    AssertTrue(responseUrl.EndsWith("/http/queryless-events", StringComparison.Ordinal), "Expected response event materialization to preserve the queryless HTTP URL.");
                    AssertTrue(!requestUrl.Contains('?', StringComparison.Ordinal), "Expected the queryless HTTP request event URL to remain queryless.");
                    AssertTrue(!responseUrl.Contains('?', StringComparison.Ordinal), "Expected the queryless HTTP response event URL to remain queryless.");
                    AssertTrue(!exceptionSource.Task.IsCompleted, "Did not expect exception events for a successful queryless HTTP request.");
                }
            }
        }

        private static async Task TestQuerylessWebSocketExceptionEventMaterializationAsync()
        {
            TaskCompletionSource<Exception> exceptionSource = CreateTaskCompletionSource<Exception>();
            TaskCompletionSource<string> exceptionUrlSource = CreateTaskCompletionSource<string>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Events.ExceptionEncountered += (sender, args) =>
                {
                    exceptionSource.TrySetResult(args?.Exception);
                    exceptionUrlSource.TrySetResult(args?.Url);
                };
                server.WebSocket("/ws/queryless-failure", (ctx, session) =>
                {
                    throw new InvalidOperationException("boom");
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/queryless-failure"), timeout.Token).ConfigureAwait(false);

                    WebSocketReceiveResult closeResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, closeResult.MessageType, "Expected queryless websocket handler failure to close the websocket.");

                    Exception exception = await exceptionSource.Task.ConfigureAwait(false);
                    string exceptionUrl = await exceptionUrlSource.Task.ConfigureAwait(false);
                    AssertTrue(exception is InvalidOperationException, "Expected the original websocket route exception to surface through ExceptionEncountered.");
                    AssertEquals("boom", exception.Message, "Expected the original websocket route exception message.");
                    AssertTrue(exceptionUrl.EndsWith("/ws/queryless-failure", StringComparison.Ordinal), "Expected exception event materialization to preserve the queryless websocket URL.");
                    AssertTrue(!exceptionUrl.Contains('?', StringComparison.Ordinal), "Expected the queryless websocket exception event URL to remain queryless.");
                }
            }
        }

        private static void ConfigureEchoRoutes(Webserver server)
        {
            server.Settings.WebSockets.Enable = true;
            server.WebSocket("/ws/echo", async (ctx, session) =>
            {
                WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                if (message != null)
                {
                    await session.SendTextAsync("echo:" + message.Text, ctx.Token).ConfigureAwait(false);
                }
            });
        }

        private static async Task TestSessionEnumerationAndDisconnectAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/manage", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/manage"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);

                    await WaitUntilAsync(() => CountSessions(host.Server) == 1, timeout.Token).ConfigureAwait(false);
                    AssertTrue(host.Server.IsWebSocketSessionConnected(sessionId), "Expected the session to be reported as connected.");

                    bool disconnectResult = await host.Server.DisconnectWebSocketSessionAsync(sessionId, WebSocketCloseStatus.NormalClosure, "disconnect").ConfigureAwait(false);
                    AssertTrue(disconnectResult, "Expected disconnect-by-guid to succeed.");

                    WebSocketReceiveResult receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, receiveResult.MessageType, "Expected a close frame after disconnect.");

                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestPostAuthWebSocketRouteAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Routes.AuthenticateRequest = ctx =>
                {
                    ctx.Metadata = "auth-meta";
                    return Task.CompletedTask;
                };

                server.WebSocket("/ws/auth", async (ctx, session) =>
                {
                    await session.SendTextAsync((string)ctx.Metadata, ctx.Token).ConfigureAwait(false);
                }, auth: true);
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/auth"), timeout.Token).ConfigureAwait(false);
                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("auth-meta", responseText, "Expected post-auth websocket route to see authentication metadata.");
                }
            }
        }

        private static async Task TestClientSuppliedGuidOptInAsync()
        {
            Guid requestedGuid = Guid.NewGuid();
            TaskCompletionSource<Guid> enabledGuidSource = CreateTaskCompletionSource<Guid>();
            TaskCompletionSource<Guid> disabledGuidSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost enabledHost = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Settings.WebSockets.AllowClientSuppliedGuid = true;
                server.WebSocket("/ws/guid-enabled", async (ctx, session) =>
                {
                    enabledGuidSource.TrySetResult(session.Id);
                    await session.SendTextAsync(session.Id.ToString(), ctx.Token).ConfigureAwait(false);
                });
            }))
            using (LoopbackServerHost disabledHost = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/guid-disabled", async (ctx, session) =>
                {
                    disabledGuidSource.TrySetResult(session.Id);
                    await session.SendTextAsync(session.Id.ToString(), ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await enabledHost.StartAsync().ConfigureAwait(false);
                await disabledHost.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket enabledClient = new ClientWebSocket())
                using (ClientWebSocket disabledClient = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    enabledClient.Options.SetRequestHeader("x-guid", requestedGuid.ToString());
                    disabledClient.Options.SetRequestHeader("x-guid", requestedGuid.ToString());

                    await enabledClient.ConnectAsync(new Uri("ws://127.0.0.1:" + enabledHost.Port.ToString() + "/ws/guid-enabled"), timeout.Token).ConfigureAwait(false);
                    Guid enabledGuid = await enabledGuidSource.Task.ConfigureAwait(false);
                    AssertEquals(requestedGuid, enabledGuid, "Expected the supplied GUID to be honored when enabled.");

                    await disabledClient.ConnectAsync(new Uri("ws://127.0.0.1:" + disabledHost.Port.ToString() + "/ws/guid-disabled"), timeout.Token).ConfigureAwait(false);
                    Guid disabledGuid = await disabledGuidSource.Task.ConfigureAwait(false);
                    AssertTrue(disabledGuid != requestedGuid, "Expected the supplied GUID to be ignored when disabled.");
                }
            }
        }

        private static async Task TestObservabilityEventsAsync()
        {
            List<string> events = new List<string>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Events.WebSocketSessionStarted += (sender, args) => events.Add("started");
                server.Events.WebSocketSessionEnded += (sender, args) => events.Add("ended");
                server.WebSocket("/ws/events", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync("ok", ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/events"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("go")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("ok", responseText, "Unexpected websocket response.");

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", timeout.Token).ConfigureAwait(false);
                    await WaitUntilAsync(() => events.Count >= 2, timeout.Token).ConfigureAwait(false);
                }

                AssertEquals("started", events[0], "Expected the first websocket lifecycle event to be started.");
                AssertEquals("ended", events[1], "Expected the second websocket lifecycle event to be ended.");
            }
        }

        private static async Task TestUnsupportedVersionHandshakeRejectedAsync()
        {
            TaskCompletionSource<string> failureReasonSource = CreateTaskCompletionSource<string>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Events.WebSocketHandshakeFailed += (sender, args) => failureReasonSource.TrySetResult(args.Reason);
                server.WebSocket("/ws/reject", async (ctx, session) =>
                {
                    await session.SendTextAsync("unexpected", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                string responseText = await SendRawUpgradeRequestAsync(host.Port,
                    "GET /ws/reject HTTP/1.1\r\n" +
                    "Host: 127.0.0.1\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Version: 12\r\n" +
                    "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                    "\r\n").ConfigureAwait(false);

                AssertTrue(responseText.Contains("426 Upgrade Required", StringComparison.Ordinal), "Expected the handshake to be rejected with 426.");
                AssertTrue(responseText.Contains("Sec-WebSocket-Version: 13", StringComparison.Ordinal), "Expected the supported version header in the rejection response.");

                string failureReason = await failureReasonSource.Task.ConfigureAwait(false);
                AssertTrue(!String.IsNullOrWhiteSpace(failureReason), "Expected a handshake failure reason.");
            }
        }

        private static async Task TestServerStopClosesSessionsAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/stop", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/stop"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    await WaitUntilAsync(() => host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);

                    host.Server.Stop();

                    WebSocketReceiveResult receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, receiveResult.MessageType, "Expected server stop to close the websocket.");
                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestNearLimitMessageTransferAsync()
        {
            string payload = new string('a', 3500);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Settings.WebSockets.MaxMessageSize = 4096;
                server.WebSocket("/ws/large", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/large"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals(payload.Length, responseText.Length, "Unexpected echoed payload length.");
                    AssertEquals(payload, responseText, "Unexpected echoed payload content.");
                }
            }
        }

        private static async Task TestBinaryPayloadIntegrityAsync()
        {
            byte[] payload = new byte[256];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/binary", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendBinaryAsync(message.Data, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/binary"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, timeout.Token).ConfigureAwait(false);

                    byte[] response = await ReceiveBinaryAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals(payload.Length, response.Length, "Unexpected binary echo payload length.");
                    for (int i = 0; i < payload.Length; i++)
                    {
                        if (payload[i] != response[i])
                        {
                            throw new InvalidOperationException("Unexpected binary echo payload content at index " + i + ".");
                        }
                    }
                }
            }
        }

        private static async Task TestConcurrentServerSendsAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/concurrent-send", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        Task sendOne = session.SendTextAsync("one", ctx.Token);
                        Task sendTwo = session.SendTextAsync("two", ctx.Token);
                        Task sendThree = session.SendTextAsync("three", ctx.Token);
                        await Task.WhenAll(sendOne, sendTwo, sendThree).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/concurrent-send"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("go")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    HashSet<string> received = new HashSet<string>(StringComparer.Ordinal);
                    received.Add(await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false));
                    received.Add(await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false));
                    received.Add(await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false));

                    AssertTrue(received.SetEquals(new[] { "one", "two", "three" }), "Expected three complete messages from concurrent sends.");
                }
            }
        }

        private static async Task TestAbruptClientDisconnectCleansRegistryAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/abort", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/abort"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    await WaitUntilAsync(() => host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);

                    client.Abort();
                    client.Dispose();

                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestMissingKeyHandshakeRejectedAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/missing-key", async (ctx, session) =>
                {
                    await session.SendTextAsync("unexpected", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                string responseText = await SendRawUpgradeRequestAsync(host.Port,
                    "GET /ws/missing-key HTTP/1.1\r\n" +
                    "Host: 127.0.0.1\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Version: 13\r\n" +
                    "\r\n").ConfigureAwait(false);

                AssertTrue(responseText.Contains("400 Bad Request", StringComparison.Ordinal), "Expected missing key handshake to be rejected with 400.");
                AssertEquals(0, CountSessions(host.Server), "Expected no session registry entries after failed handshake.");
            }
        }

        private static async Task TestWrongMethodHandshakeRejectedAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/method", async (ctx, session) =>
                {
                    await session.SendTextAsync("unexpected", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                string responseText = await SendRawUpgradeRequestAsync(host.Port,
                    "POST /ws/method HTTP/1.1\r\n" +
                    "Host: 127.0.0.1\r\n" +
                    "Connection: Upgrade\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Sec-WebSocket-Version: 13\r\n" +
                    "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                    "Content-Length: 0\r\n" +
                    "\r\n").ConfigureAwait(false);

                AssertTrue(responseText.Contains("405 Method Not Allowed", StringComparison.Ordinal), "Expected wrong-method websocket initiation to be rejected with 405.");
                AssertEquals(0, CountSessions(host.Server), "Expected no session registry entries after wrong-method rejection.");
            }
        }

        private static async Task TestRouteParametersAndRequestMetadataAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/room/{room}", async (ctx, session) =>
                {
                    string payload = (ctx.Request.Url.Parameters["room"] ?? String.Empty) +
                        "|" + (session.Request.Query.TryGetValue("name", out string name) ? name : String.Empty) +
                        "|" + (session.Request.Headers["X-Test"] ?? String.Empty);
                    await session.SendTextAsync(payload, ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    client.Options.SetRequestHeader("X-Test", "header-value");
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/room/general?name=alice"), timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("general|alice|header-value", responseText, "Expected route parameters, query, and headers to survive the websocket upgrade.");
                }
            }
        }

        private static async Task TestUnsupportedSubprotocolRequestsHandledAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/subprotocol", async (ctx, session) =>
                {
                    string negotiated = session.Subprotocol ?? String.Empty;
                    await session.SendTextAsync("subprotocol:" + negotiated, ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    client.Options.AddSubProtocol("chat");
                    client.Options.AddSubProtocol("superchat");
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/subprotocol"), timeout.Token).ConfigureAwait(false);

                    AssertTrue(String.IsNullOrEmpty(client.SubProtocol), "Expected unsupported requested subprotocols to remain unnegotiated.");
                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("subprotocol:", responseText, "Expected the server session to report no negotiated subprotocol.");
                }
            }
        }

        private static async Task TestSessionStatisticsAsync()
        {
            TaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics> statsSource = CreateTaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/stats", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync("reply:" + message.Text, ctx.Token).ConfigureAwait(false);
                        statsSource.TrySetResult(session.Statistics.Snapshot());
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    string payload = "hello-stats";
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/stats"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("reply:" + payload, responseText, "Unexpected websocket stats response.");

                    WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics stats = await statsSource.Task.ConfigureAwait(false);
                    AssertEquals(1L, stats.MessagesReceived, "Expected one received websocket message.");
                    AssertEquals(1L, stats.MessagesSent, "Expected one sent websocket message.");
                    AssertEquals((long)Encoding.UTF8.GetByteCount(payload), stats.BytesReceived, "Unexpected received websocket byte count.");
                    AssertEquals((long)Encoding.UTF8.GetByteCount(responseText), stats.BytesSent, "Unexpected sent websocket byte count.");
                }
            }
        }

        private static async Task TestConcurrentSendStatisticsAsync()
        {
            TaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics> statsSource = CreateTaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/stats-concurrent", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage trigger = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (trigger == null) return;

                    await Task.WhenAll(
                        session.SendTextAsync("one", ctx.Token),
                        session.SendTextAsync("two", ctx.Token),
                        session.SendTextAsync("three", ctx.Token)).ConfigureAwait(false);

                    statsSource.TrySetResult(session.Statistics.Snapshot());
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/stats-concurrent"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("go")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    HashSet<string> received = new HashSet<string>(StringComparer.Ordinal)
                    {
                        await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false),
                        await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false),
                        await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false)
                    };

                    AssertTrue(received.SetEquals(new[] { "one", "two", "three" }), "Expected concurrent stats test to receive all three messages.");

                    WatsonWebserver.Core.WebSockets.WebSocketSessionStatistics stats = await statsSource.Task.ConfigureAwait(false);
                    AssertEquals(1L, stats.MessagesReceived, "Expected one received trigger message.");
                    AssertEquals(3L, stats.MessagesSent, "Expected three sent messages under concurrent send.");
                    AssertEquals((long)Encoding.UTF8.GetByteCount("go"), stats.BytesReceived, "Unexpected received trigger byte count.");
                    AssertEquals((long)(Encoding.UTF8.GetByteCount("one") + Encoding.UTF8.GetByteCount("two") + Encoding.UTF8.GetByteCount("three")), stats.BytesSent, "Unexpected concurrent-send byte count.");
                }
            }
        }

        private static async Task TestInvalidClientGuidHandledSafelyAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Settings.WebSockets.AllowClientSuppliedGuid = true;
                server.WebSocket("/ws/guid-invalid", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.SendTextAsync(session.Id.ToString(), ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    client.Options.SetRequestHeader("x-guid", "definitely-not-a-guid");
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/guid-invalid"), timeout.Token).ConfigureAwait(false);

                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals(sessionId.ToString(), responseText, "Expected the server-generated GUID to be surfaced to the client.");
                    AssertTrue(sessionId != Guid.Empty, "Expected a non-empty server-generated GUID.");
                }
            }
        }

        private static async Task TestOversizedMessageRejectedAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Settings.WebSockets.MaxMessageSize = 32;
                server.WebSocket("/ws/oversized", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/oversized"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(new string('z', 64))), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    WebSocketReceiveResult receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, receiveResult.MessageType, "Expected oversized-message rejection to close the websocket.");
                    AssertTrue(receiveResult.CloseStatus.HasValue, "Expected oversized-message rejection to surface a websocket close status.");
                    await WaitUntilAsync(() => !host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);
                }

                AssertEquals(0, CountSessions(host.Server), "Expected oversized-message rejection to drain the registry.");
            }
        }

        private static async Task TestRouteHandlerExceptionClosesSessionAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/throw", (ctx, session) =>
                {
                    throw new InvalidOperationException("boom");
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/throw"), timeout.Token).ConfigureAwait(false);
                    WebSocketReceiveResult receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);

                    AssertEquals(WebSocketMessageType.Close, receiveResult.MessageType, "Expected route-handler exception to close the websocket.");
                    AssertEquals(WebSocketCloseStatus.InternalServerError, receiveResult.CloseStatus ?? WebSocketCloseStatus.Empty, "Expected route-handler exception to use InternalServerError.");
                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestNonWebSocketRequestsOnWebSocketOnlyPathAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/only", async (ctx, session) =>
                {
                    await session.SendTextAsync("unexpected", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:" + host.Port.ToString() + "/ws/only").ConfigureAwait(false);
                    AssertEquals(404, (int)response.StatusCode, "Expected ordinary HTTP behavior on a websocket-only path.");
                }
            }
        }

        private static async Task TestConcurrentReceiveRejectedAsync()
        {
            TaskCompletionSource<string> resultSource = CreateTaskCompletionSource<string>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/receive-lock", async (ctx, session) =>
                {
                    Task<WatsonWebserver.Core.WebSockets.WebSocketMessage> first = session.ReceiveAsync(ctx.Token);
                    try
                    {
                        await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                        resultSource.TrySetResult("unexpected-success");
                    }
                    catch (InvalidOperationException)
                    {
                        resultSource.TrySetResult("rejected");
                    }

                    try
                    {
                        await first.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/receive-lock"), timeout.Token).ConfigureAwait(false);
                    string result = await resultSource.Task.ConfigureAwait(false);
                    AssertEquals("rejected", result, "Expected concurrent receive attempts to be rejected.");
                    client.Abort();
                }
            }
        }

        private static async Task TestClientReceiveCancellationAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/client-receive-cancel", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await Task.Delay(500, ctx.Token).ConfigureAwait(false);
                    await session.SendTextAsync("first", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/client-receive-cancel"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);

                    using (CancellationTokenSource receiveCancel = new CancellationTokenSource(100))
                    {
                        await AssertThrowsAsync<OperationCanceledException>(
                            async () => await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), receiveCancel.Token).ConfigureAwait(false),
                            "Expected client receive cancellation to throw OperationCanceledException.");
                    }

                    await WaitUntilAsync(() => !host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestClientCancellationDuringSendAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/send-cancel", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    WatsonWebserver.Core.WebSockets.WebSocketMessage trigger = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (trigger == null) return;

                    string payload = new string('s', 32 * 1024);
                    for (int i = 0; i < 50; i++)
                    {
                        await session.SendTextAsync("chunk-" + i + ":" + payload, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/send-cancel"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("go")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                    await Task.Delay(200, timeout.Token).ConfigureAwait(false);
                    client.Abort();
                    await WaitUntilAsync(() => !host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);
                }

                AssertEquals(0, CountSessions(host.Server), "Expected client cancellation during server send to drain the websocket registry.");
            }
        }

        private static async Task TestSessionEndedEventFiresOnceAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();
            Dictionary<Guid, int> endedCounts = new Dictionary<Guid, int>();
            object sync = new object();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.Events.WebSocketSessionEnded += (sender, args) =>
                {
                    lock (sync)
                    {
                        if (!endedCounts.ContainsKey(args.Session.Id)) endedCounts[args.Session.Id] = 0;
                        endedCounts[args.Session.Id]++;
                    }
                };

                server.WebSocket("/ws/end-once", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                Guid sessionId;
                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/end-once"), timeout.Token).ConfigureAwait(false);
                    sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    client.Abort();
                    await WaitUntilAsync(() =>
                    {
                        lock (sync)
                        {
                            return endedCounts.TryGetValue(sessionId, out int count) && count >= 1;
                        }
                    }, timeout.Token).ConfigureAwait(false);
                }

                lock (sync)
                {
                    AssertEquals(1, endedCounts.TryGetValue(sessionId, out int count) ? count : 0, "Expected the websocket session-ended event to fire once.");
                }
            }
        }

        private static async Task TestHalfOpenNetworkFailureCleanupAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/half-open", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (TcpClient tcpClient = new TcpClient())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await tcpClient.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);
                    using (NetworkStream stream = tcpClient.GetStream())
                    {
                        string request =
                            "GET /ws/half-open HTTP/1.1\r\n" +
                            "Host: 127.0.0.1\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            "Sec-WebSocket-Version: 13\r\n" +
                            "Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n" +
                            "\r\n";
                        byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length, timeout.Token).ConfigureAwait(false);
                        await stream.FlushAsync(timeout.Token).ConfigureAwait(false);

                        byte[] responseBuffer = new byte[4096];
                        int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length, timeout.Token).ConfigureAwait(false);
                        string responseText = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead);
                        AssertTrue(responseText.Contains("101 Switching Protocols", StringComparison.Ordinal), "Expected raw half-open test to complete the websocket handshake.");
                    }

                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    tcpClient.Close();
                    await WaitUntilAsync(() => !host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);
                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }

                AssertEquals(0, CountSessions(host.Server), "Expected half-open network failure to drain the websocket registry.");
            }
        }

        private static async Task TestUtf8TextHandlingAsync()
        {
            string payload = "こんにちは, Watson 👋";

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/utf8", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync(message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/utf8"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals(payload, responseText, "Expected UTF-8 websocket text to round-trip unchanged.");
                }
            }
        }

        private static async Task TestTlsWebSocketLoopbackAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(true, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/tls", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    if (message != null)
                    {
                        await session.SendTextAsync("tls:" + message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    client.Options.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
                    await client.ConnectAsync(new Uri("wss://127.0.0.1:" + host.Port.ToString() + "/ws/tls"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("secure")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("tls:secure", responseText, "Expected TLS websocket loopback to succeed.");
                }
            }
        }

        private static async Task TestCloseStateRetainedAsync()
        {
            TaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSession> sessionSource = CreateTaskCompletionSource<WatsonWebserver.Core.WebSockets.WebSocketSession>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/close-state", async (ctx, session) =>
                {
                    await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, "close-state", ctx.Token).ConfigureAwait(false);
                    sessionSource.TrySetResult(session);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/close-state"), timeout.Token).ConfigureAwait(false);
                    WebSocketReceiveResult result = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, result.MessageType, "Expected the close-state test route to close the websocket.");

                    WatsonWebserver.Core.WebSockets.WebSocketSession session = await sessionSource.Task.ConfigureAwait(false);
                    AssertEquals(WebSocketCloseStatus.PolicyViolation, session.CloseStatus, "Expected the session to retain its close status.");
                    AssertEquals("close-state", session.CloseStatusDescription, "Expected the session to retain its close description.");
                    AssertEquals(WebSocketState.Closed, session.State, "Expected the session state to report closed after shutdown.");
                    AssertTrue(!session.IsConnected, "Expected the session to report disconnected after shutdown.");
                }
            }
        }

        private static async Task TestMixedTextAndBinaryTrafficAsync()
        {
            byte[] binaryPayload = new byte[] { 0, 1, 2, 127, 128, 254, 255 };

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/mixed", async (ctx, session) =>
                {
                    WatsonWebserver.Core.WebSockets.WebSocketMessage first = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                    WatsonWebserver.Core.WebSockets.WebSocketMessage second = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);

                    await session.SendTextAsync(first.Text, ctx.Token).ConfigureAwait(false);
                    await session.SendBinaryAsync(second.Data, ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/mixed"), timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("mixed-text")), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                    await client.SendAsync(new ArraySegment<byte>(binaryPayload), WebSocketMessageType.Binary, true, timeout.Token).ConfigureAwait(false);

                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    byte[] responseBinary = await ReceiveBinaryAsync(client, timeout.Token).ConfigureAwait(false);

                    AssertEquals("mixed-text", responseText, "Expected mixed-session text payload to round-trip unchanged.");
                    AssertEquals(binaryPayload.Length, responseBinary.Length, "Unexpected mixed-session binary payload length.");
                    for (int i = 0; i < binaryPayload.Length; i++)
                    {
                        if (binaryPayload[i] != responseBinary[i])
                        {
                            throw new InvalidOperationException("Unexpected mixed-session binary payload content at index " + i + ".");
                        }
                    }
                }
            }
        }

        private static async Task TestServerInitiatedCloseAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/server-close", async (ctx, session) =>
                {
                    await session.SendTextAsync("closing", ctx.Token).ConfigureAwait(false);
                    await session.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "server-close", ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/server-close"), timeout.Token).ConfigureAwait(false);
                    string responseText = await ReceiveTextAsync(client, timeout.Token).ConfigureAwait(false);
                    AssertEquals("closing", responseText, "Expected server close route to send a final text message before closing.");

                    WebSocketReceiveResult receiveResult = await client.ReceiveAsync(new ArraySegment<byte>(new byte[128]), timeout.Token).ConfigureAwait(false);
                    AssertEquals(WebSocketMessageType.Close, receiveResult.MessageType, "Expected a close frame from server-initiated close.");
                    AssertTrue(receiveResult.CloseStatus.HasValue, "Expected server-initiated close to include a close status.");
                }
            }
        }

        private static async Task TestClientInitiatedCloseAsync()
        {
            TaskCompletionSource<Guid> sessionIdSource = CreateTaskCompletionSource<Guid>();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/client-close", async (ctx, session) =>
                {
                    sessionIdSource.TrySetResult(session.Id);
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/client-close"), timeout.Token).ConfigureAwait(false);
                    Guid sessionId = await sessionIdSource.Task.ConfigureAwait(false);
                    await WaitUntilAsync(() => host.Server.IsWebSocketSessionConnected(sessionId), timeout.Token).ConfigureAwait(false);

                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "client-finished", timeout.Token).ConfigureAwait(false);
                    await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                }

                AssertEquals(0, CountSessions(host.Server), "Expected client-initiated close to drain the websocket registry.");
            }
        }

        private static async Task TestRepeatedConnectDisconnectCyclesAsync()
        {
            const int iterations = 8;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/repeat", async (ctx, session) =>
                {
                    await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        using (ClientWebSocket client = new ClientWebSocket())
                        {
                            await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/repeat"), timeout.Token).ConfigureAwait(false);
                            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "cycle-" + i, timeout.Token).ConfigureAwait(false);
                        }

                        await WaitUntilAsync(() => CountSessions(host.Server) == 0, timeout.Token).ConfigureAwait(false);
                    }
                }

                AssertEquals(0, CountSessions(host.Server), "Expected repeated connect-disconnect cycles to leave no live websocket sessions.");
            }
        }

        private static async Task TestConcurrentManySessionTrafficAsync()
        {
            const int clientCount = 6;
            TaskCompletionSource<bool> allConnectedSource = CreateTaskCompletionSource<bool>();
            int connectedCount = 0;

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/multi", async (ctx, session) =>
                {
                    if (Interlocked.Increment(ref connectedCount) == clientCount)
                    {
                        allConnectedSource.TrySetResult(true);
                    }

                    await foreach (WatsonWebserver.Core.WebSockets.WebSocketMessage message in session.ReadMessagesAsync(ctx.Token).ConfigureAwait(false))
                    {
                        await session.SendTextAsync("ack:" + message.Text, ctx.Token).ConfigureAwait(false);
                    }
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    ClientWebSocket[] clients = new ClientWebSocket[clientCount];
                    try
                    {
                        for (int i = 0; i < clientCount; i++)
                        {
                            clients[i] = new ClientWebSocket();
                            await clients[i].ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/multi"), timeout.Token).ConfigureAwait(false);
                        }

                        await allConnectedSource.Task.ConfigureAwait(false);
                        await WaitUntilAsync(() => CountSessions(host.Server) == clientCount, timeout.Token).ConfigureAwait(false);

                        Task[] sends = new Task[clientCount];
                        Task<string>[] receives = new Task<string>[clientCount];

                        for (int i = 0; i < clientCount; i++)
                        {
                            string payload = "client-" + i;
                            sends[i] = clients[i].SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload)), WebSocketMessageType.Text, true, timeout.Token);
                            receives[i] = ReceiveTextAsync(clients[i], timeout.Token);
                        }

                        await Task.WhenAll(sends).ConfigureAwait(false);
                        string[] responses = await Task.WhenAll(receives).ConfigureAwait(false);

                        for (int i = 0; i < clientCount; i++)
                        {
                            AssertEquals("ack:client-" + i, responses[i], "Expected a matching response for each concurrent websocket client.");
                        }
                    }
                    finally
                    {
                        for (int i = 0; i < clients.Length; i++)
                        {
                            if (clients[i] == null) continue;
                            try
                            {
                                clients[i].Dispose();
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }
                }
            }
        }

        private static void ConfigureSamePathRoutes(Webserver server)
        {
            server.Settings.WebSockets.Enable = true;
            server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/chat", async ctx =>
            {
                await ctx.Response.Send("http-route", ctx.Token).ConfigureAwait(false);
            });

            server.WebSocket("/chat", async (ctx, session) =>
            {
                WatsonWebserver.Core.WebSockets.WebSocketMessage message = await session.ReceiveAsync(ctx.Token).ConfigureAwait(false);
                if (message != null)
                {
                    await session.SendTextAsync("ws-route", ctx.Token).ConfigureAwait(false);
                }
            });
        }

        private static async Task<string> ReceiveTextAsync(ClientWebSocket client, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
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
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
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

        private static async Task<string> SendRawUpgradeRequestAsync(int port, string request)
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    byte[] buffer = new byte[4096];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    return Encoding.ASCII.GetString(buffer, 0, bytesRead);
                }
            }
        }

        private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken token)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            while (!predicate())
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private static int CountSessions(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            int count = 0;
            foreach (object _ in server.ListWebSocketSessions())
            {
                count++;
            }

            return count;
        }

        private static TaskCompletionSource<T> CreateTaskCompletionSource<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertThrows<TException>(Action action, string message) where TException : Exception
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }

        private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message) where TException : Exception
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException(message);
        }
    }
}
