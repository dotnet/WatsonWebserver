namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Routing;
    using WatsonWebserver.Core.Settings;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Shared core unit-style tests that can execute in both runners.
    /// </summary>
    public static class SharedCoreUnitTests
    {
        /// <summary>
        /// Get the shared core unit tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("ApiErrorResponse :: Status code derived from error", TestApiErrorResponseStatusCodeDerivedFromError));
            tests.Add(CreateSync("ApiErrorResponse :: Description auto-populated", TestApiErrorResponseDescriptionAutoPopulated));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Success", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Success, 200); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Created", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Created, 201); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for BadRequest", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.BadRequest, 400); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for NotAuthorized", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.NotAuthorized, 401); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for NotFound", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.NotFound, 404); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for RequestTimeout", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.RequestTimeout, 408); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for Conflict", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.Conflict, 409); }));
            tests.Add(CreateSync("ApiErrorResponse :: Status code maps correctly for InternalError", delegate { TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum.InternalError, 500); }));

            tests.Add(CreateSync("AuthResult :: IsPermitted true when success and permitted", TestAuthResultPermitted));
            tests.Add(CreateSync("AuthResult :: IsPermitted false when not found", TestAuthResultNotFound));
            tests.Add(CreateSync("AuthResult :: IsPermitted false when denied explicit", TestAuthResultDeniedExplicit));
            tests.Add(CreateSync("AuthResult :: Metadata propagated", TestAuthResultMetadataPropagated));

            tests.Add(CreateSync("TimeoutSettings :: Default is zero", TestTimeoutSettingsDefaultIsZero));
            tests.Add(CreateSync("TimeoutSettings :: Constructor sets timeout", TestTimeoutSettingsConstructorSetsTimeout));
            tests.Add(CreateSync("TimeoutSettings :: Negative timeout throws", TestTimeoutSettingsNegativeTimeoutThrows));

            tests.Add(CreateSync("WebSocketSettings :: Defaults match v1 plan", TestWebSocketSettingsDefaults));
            tests.Add(CreateSync("WebSocketSettings :: Numeric values clamp to safe ranges", TestWebSocketSettingsClampValues));
            tests.Add(CreateSync("WebSocketSettings :: Empty client guid header resets to default", TestWebSocketSettingsClientGuidHeaderDefaults));
            tests.Add(CreateSync("WebSocketSettings :: Invalid supported version rejected", TestWebSocketSettingsInvalidVersionRejected));
            tests.Add(CreateSync("WebSocketHandshakeUtilities :: Accept key matches RFC example", TestWebSocketHandshakeAcceptKey));
            tests.Add(CreateSync("WebSocketMessage :: Text factory preserves payload", TestWebSocketMessageTextFactory));
            tests.Add(CreateSync("WebSocketSessionStatistics :: Counters accumulate correctly", TestWebSocketSessionStatisticsCounters));
            tests.Add(CreateSync("WebSocketRouteManager :: Same-path HTTP and WebSocket registration remain separate", TestWebSocketRouteManagerSamePathHttpAndWebSocket));
            tests.Add(CreateSync("WebSocketRouteManager :: Parameter routes populate path values", TestWebSocketRouteManagerParameterizedRoute));
            tests.Add(CreateSync("WebSocketConnectionRegistry :: Connected lookup and removal work", TestWebSocketConnectionRegistryOperations));
            tests.Add(CreateSync("WebserverBase :: WebSocket registration lands in selected routing group", TestWebserverBaseWebSocketRegistration));
            tests.Add(CreateAsync("Webserver :: Dispose during active connection shutdown does not emit unobserved task exception", TestDisposeDuringActiveConnectionShutdownDoesNotEmitUnobservedTaskExceptionAsync));

            tests.Add(CreateSync("WebserverException :: Status code maps from result", TestWebserverExceptionStatusCodeMapsFromResult));
            tests.Add(CreateSync("WebserverException :: Message custom message", TestWebserverExceptionMessageCustomMessage));
            tests.Add(CreateSync("WebserverException :: Message default message", TestWebserverExceptionMessageDefaultMessage));
            tests.Add(CreateSync("WebserverException :: Data can be set", TestWebserverExceptionDataCanBeSet));
            tests.Add(CreateSync("WebserverException :: Inner exception preserved", TestWebserverExceptionInnerExceptionPreserved));

            tests.Add(CreateSync("HttpRequestBase :: Query returns empty QueryDetails when factory yields null", TestQueryPropertyReturnsEmptyWhenFactoryYieldsNull));
            tests.Add(CreateSync("HttpRequestBase :: Url returns empty UrlDetails when factory yields null", TestUrlPropertyReturnsEmptyWhenFactoryYieldsNull));
            tests.Add(CreateSync("HttpRequestBase :: Headers returns empty collection when factory yields null", TestHeadersPropertyReturnsEmptyWhenFactoryYieldsNull));

            return tests.ToArray();
        }

        private static SharedNamedTestCase CreateSync(string name, Action action)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SharedNamedTestCase(name, delegate
            {
                action();
                return Task.CompletedTask;
            });
        }

        private static SharedNamedTestCase CreateAsync(string name, Func<Task> func)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return new SharedNamedTestCase(name, func);
        }

        private static void TestApiErrorResponseStatusCodeDerivedFromError()
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = ApiResultEnum.NotFound };
            AssertEquals(404, response.StatusCode, "Expected 404 for NotFound.");
        }

        private static void TestApiErrorResponseDescriptionAutoPopulated()
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = ApiResultEnum.NotAuthorized };
            AssertTrue(!String.IsNullOrEmpty(response.Description), "Description should be auto-populated.");
        }

        private static void TestApiErrorResponseStatusCodeMapsCorrectly(ApiResultEnum error, int expectedCode)
        {
            ApiErrorResponse response = new ApiErrorResponse { Error = error };
            AssertEquals(expectedCode, response.StatusCode, "Unexpected status-code mapping.");
        }

        private static void TestAuthResultPermitted()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Permitted
            };
            AssertTrue(result.IsPermitted(), "AuthResult should be permitted.");
        }

        private static void TestAuthResultNotFound()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
            AssertTrue(!result.IsPermitted(), "AuthResult should not be permitted when not found.");
        }

        private static void TestAuthResultDeniedExplicit()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.DeniedExplicit
            };
            AssertTrue(!result.IsPermitted(), "AuthResult should not be permitted when explicitly denied.");
        }

        private static void TestAuthResultMetadataPropagated()
        {
            AuthResult result = new AuthResult { Metadata = "metadata" };
            AssertTrue(result.Metadata != null, "Metadata should be retained.");
        }

        private static void TestTimeoutSettingsDefaultIsZero()
        {
            TimeoutSettings settings = new TimeoutSettings();
            AssertEquals(TimeSpan.Zero, settings.DefaultTimeout, "Default timeout should be zero.");
        }

        private static void TestTimeoutSettingsConstructorSetsTimeout()
        {
            TimeoutSettings settings = new TimeoutSettings(TimeSpan.FromSeconds(30));
            AssertEquals(TimeSpan.FromSeconds(30), settings.DefaultTimeout, "Constructor should set the timeout.");
        }

        private static void TestTimeoutSettingsNegativeTimeoutThrows()
        {
            try
            {
                TimeoutSettings settings = new TimeoutSettings();
                settings.DefaultTimeout = TimeSpan.FromSeconds(-1);
                throw new InvalidOperationException("Expected DefaultTimeout setter to reject negative values.");
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        private static void TestWebSocketSettingsDefaults()
        {
            WebSocketSettings settings = new WebSocketSettings();
            AssertTrue(!settings.Enable, "WebSockets should be disabled by default.");
            AssertEquals(16777216, settings.MaxMessageSize, "Unexpected default max-message size.");
            AssertEquals(65536, settings.ReceiveBufferSize, "Unexpected default receive-buffer size.");
            AssertEquals(5000, settings.CloseHandshakeTimeoutMs, "Unexpected default close-handshake timeout.");
            AssertTrue(!settings.AllowClientSuppliedGuid, "Client-supplied GUIDs should be disabled by default.");
            AssertEquals("x-guid", settings.ClientGuidHeaderName, "Unexpected default client GUID header name.");
            AssertEquals(1, settings.SupportedVersions.Count, "Unexpected supported-version count.");
            AssertEquals("13", settings.SupportedVersions[0], "Unexpected supported version.");
            AssertTrue(settings.EnableHttp1, "HTTP/1 WebSockets should be enabled by default.");
            AssertTrue(!settings.EnableHttp2, "HTTP/2 WebSockets should be disabled by default.");
            AssertTrue(!settings.EnableHttp3, "HTTP/3 WebSockets should be disabled by default.");
        }

        private static void TestWebSocketSettingsClampValues()
        {
            WebSocketSettings settings = new WebSocketSettings();
            settings.MaxMessageSize = 1;
            settings.ReceiveBufferSize = Int32.MaxValue;
            settings.CloseHandshakeTimeoutMs = 10;

            AssertEquals(WebSocketSettings.MinMaxMessageSize, settings.MaxMessageSize, "Max-message size should clamp to the minimum.");
            AssertEquals(WebSocketSettings.MaxReceiveBufferSize, settings.ReceiveBufferSize, "Receive buffer size should clamp to the maximum.");
            AssertEquals(WebSocketSettings.MinCloseHandshakeTimeoutMs, settings.CloseHandshakeTimeoutMs, "Close timeout should clamp to the minimum.");
        }

        private static void TestWebSocketSettingsClientGuidHeaderDefaults()
        {
            WebSocketSettings settings = new WebSocketSettings();
            settings.ClientGuidHeaderName = "   ";
            AssertEquals("x-guid", settings.ClientGuidHeaderName, "Blank client GUID header names should reset to the default.");
        }

        private static void TestWebSocketSettingsInvalidVersionRejected()
        {
            try
            {
                WebserverSettings settings = new WebserverSettings();
                settings.WebSockets.SupportedVersions = new List<string> { "12" };
                WebserverSettingsValidator.Validate(settings, supportsHttp2: true, supportsHttp3: true);
                throw new InvalidOperationException("Expected invalid WebSocket versions to be rejected.");
            }
            catch (WebserverConfigurationException)
            {
            }
        }

        private static void TestWebSocketHandshakeAcceptKey()
        {
            string acceptKey = WebSocketHandshakeUtilities.ComputeAcceptKey("dGhlIHNhbXBsZSBub25jZQ==");
            AssertEquals("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", acceptKey, "Unexpected RFC 6455 accept key.");
        }

        private static void TestWebSocketMessageTextFactory()
        {
            WebSocketMessage message = WebSocketMessage.FromText("hello");
            AssertEquals(WebSocketMessageType.Text, message.MessageType, "Unexpected message type.");
            AssertEquals("hello", message.Text, "Unexpected text payload.");
            AssertEquals(5, message.Length, "Unexpected payload length.");
        }

        private static void TestWebSocketSessionStatisticsCounters()
        {
            WebSocketSessionStatistics statistics = new WebSocketSessionStatistics();
            statistics.IncrementReceived(5);
            statistics.IncrementReceived(0);
            statistics.IncrementSent(7);
            WebSocketSessionStatistics snapshot = statistics.Snapshot();

            AssertEquals(2L, snapshot.MessagesReceived, "Unexpected received-message count.");
            AssertEquals(5L, snapshot.BytesReceived, "Unexpected received-byte count.");
            AssertEquals(1L, snapshot.MessagesSent, "Unexpected sent-message count.");
            AssertEquals(7L, snapshot.BytesSent, "Unexpected sent-byte count.");
        }

        private static void TestWebSocketRouteManagerSamePathHttpAndWebSocket()
        {
            RoutingGroup group = new RoutingGroup();
            group.Static.Add(HttpMethod.GET, "/chat", ctx => Task.CompletedTask);
            group.WebSockets.Add("/chat", (ctx, session) => Task.CompletedTask);

            Func<HttpContextBase, Task> httpHandler = group.Static.Match(HttpMethod.GET, "/chat", out StaticRoute httpRoute);
            Func<HttpContextBase, WebSocketSession, Task> wsHandler = group.WebSockets.Match("/chat", out NameValueCollection _, out WebSocketRoute wsRoute);

            AssertTrue(httpHandler != null, "Expected the HTTP route to remain registered.");
            AssertTrue(wsHandler != null, "Expected the WebSocket route to remain registered.");
            AssertEquals("/chat/", httpRoute.Path, "Unexpected HTTP route path.");
            AssertEquals("/chat/", wsRoute.Path, "Unexpected WebSocket route path.");
        }

        private static void TestWebSocketRouteManagerParameterizedRoute()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/chat/{room}", (ctx, session) => Task.CompletedTask);

            Func<HttpContextBase, WebSocketSession, Task> handler = manager.Match("/chat/general", out NameValueCollection parameters, out WebSocketRoute route);
            AssertTrue(handler != null, "Expected parameterized WebSocket route to match.");
            AssertTrue(parameters != null, "Expected route parameters.");
            AssertEquals("general", parameters["room"], "Unexpected route parameter value.");
            AssertEquals("/chat/{room}", route.Path, "Unexpected parameterized route path.");
        }

        private static void TestWebSocketConnectionRegistryOperations()
        {
            TestWebSocket socket = new TestWebSocket();
            WebSocketRequestDescriptor descriptor = new WebSocketRequestDescriptor("/chat", null, null, "13", Array.Empty<string>(), "127.0.0.1", 12345);
            WebSocketSession session = new WebSocketSession(socket, descriptor);
            WebSocketConnectionRegistry registry = new WebSocketConnectionRegistry();

            registry.Add(session);
            AssertTrue(registry.IsConnected(session.Id), "Expected the session to appear connected.");
            AssertTrue(registry.Remove(session.Id), "Expected registry removal to succeed.");
            AssertTrue(!registry.IsConnected(session.Id), "Expected removed session to be absent.");
        }

        private static void TestWebserverBaseWebSocketRegistration()
        {
            TestWebserver server = new TestWebserver();
            server.WebSocket("/chat", (ctx, session) => Task.CompletedTask, auth: true);
            Func<HttpContextBase, WebSocketSession, Task> handler = server.Routes.PostAuthentication.WebSockets.Match("/chat", out NameValueCollection _, out WebSocketRoute route);

            AssertTrue(handler != null, "Expected the WebSocket route to be added to the post-auth group.");
            AssertEquals("/chat/", route.Path, "Unexpected route path.");
        }

        private static void TestWebserverExceptionStatusCodeMapsFromResult()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.NotFound);
            AssertEquals(404, exception.StatusCode, "Unexpected WebserverException status code.");
        }

        private static void TestWebserverExceptionMessageCustomMessage()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.BadRequest, "Invalid input");
            AssertEquals("Invalid input", exception.Message, "Custom message should be preserved.");
        }

        private static void TestWebserverExceptionMessageDefaultMessage()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.NotFound);
            AssertEquals("Not found.", exception.Message, "Default message should be mapped from the result.");
        }

        private static void TestWebserverExceptionDataCanBeSet()
        {
            WebserverException exception = new WebserverException(ApiResultEnum.Conflict);
            exception.Data = "name";
            AssertTrue(exception.Data != null, "Data payload should be assignable.");
        }

        private static void TestWebserverExceptionInnerExceptionPreserved()
        {
            Exception inner = new InvalidOperationException("inner");
            WebserverException exception = new WebserverException(ApiResultEnum.InternalError, "outer", inner);
            AssertTrue(ReferenceEquals(inner, exception.InnerException), "Inner exception should be preserved.");
        }

        private static void TestQueryPropertyReturnsEmptyWhenFactoryYieldsNull()
        {
            HttpRequest request = new HttpRequest();
            request.Query = null;
            QueryDetails query = request.Query;
            AssertTrue(query != null, "Query should never be null even when factory returns null.");
        }

        private static void TestUrlPropertyReturnsEmptyWhenFactoryYieldsNull()
        {
            HttpRequest request = new HttpRequest();
            request.Url = null;
            UrlDetails url = request.Url;
            AssertTrue(url != null, "Url should never be null even when factory returns null.");
        }

        private static void TestHeadersPropertyReturnsEmptyWhenFactoryYieldsNull()
        {
            HttpRequest request = new HttpRequest();
            request.Headers = null;
            System.Collections.Specialized.NameValueCollection headers = request.Headers;
            AssertTrue(headers != null, "Headers should never be null even when factory returns null.");
        }

        private static async Task TestDisposeDuringActiveConnectionShutdownDoesNotEmitUnobservedTaskExceptionAsync()
        {
            Exception unobservedException = null;
            EventHandler<UnobservedTaskExceptionEventArgs> unobservedHandler = delegate (object sender, UnobservedTaskExceptionEventArgs args)
            {
                args.SetObserved();
                Interlocked.CompareExchange(ref unobservedException, args.Exception, null);
            };

            TaskScheduler.UnobservedTaskException += unobservedHandler;

            try
            {
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    await RunActiveConnectionDisposeCycleAsync().ConfigureAwait(false);
                    await ForceTaskFinalizationAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= unobservedHandler;
                await ForceTaskFinalizationAsync().ConfigureAwait(false);
            }

            AssertTrue(unobservedException == null, "Unexpected unobserved task exception while disposing active connections: " + unobservedException);
        }

        private static async Task RunActiveConnectionDisposeCycleAsync()
        {
            int port = GetAvailablePort();
            WebserverSettings settings = new WebserverSettings("127.0.0.1", port, false);
            settings.IO.MaxRequests = 16;
            settings.IO.ReadTimeoutMs = 60000;
            settings.Protocols.IdleTimeoutMs = 60000;

            Webserver server = new Webserver(settings, ctx => Task.CompletedTask);
            List<TcpClient> clients = new List<TcpClient>();

            try
            {
                server.Start();

                for (int i = 0; i < 64; i++)
                {
                    TcpClient client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", port).ConfigureAwait(false);
                    clients.Add(client);
                }

                await Task.Delay(100).ConfigureAwait(false);
            }
            finally
            {
                server.Dispose();

                for (int i = 0; i < clients.Count; i++)
                {
                    clients[i].Dispose();
                }
            }
        }

        private static async Task ForceTaskFinalizationAsync()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(100).ConfigureAwait(false);
        }

        private static int GetAvailablePort()
        {
            using (TcpListener listener = new TcpListener(IPAddress.Loopback, 0))
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
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
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
            }
        }

        private sealed class TestWebserver : WebserverBase
        {
            public TestWebserver() : base(new WebserverSettings(), ctx => Task.CompletedTask)
            {
            }

            public override bool IsListening => false;

            public override int RequestCount => 0;

            public override void Dispose()
            {
            }

            public override void Start(CancellationToken token = default)
            {
            }

            public override Task StartAsync(CancellationToken token = default)
            {
                return Task.CompletedTask;
            }

            public override void Stop()
            {
            }
        }

        private sealed class TestWebSocket : WebSocket
        {
            private WebSocketState _State = WebSocketState.Open;

            public override WebSocketCloseStatus? CloseStatus => null;

            public override string CloseStatusDescription => null;

            public override string SubProtocol => null;

            public override WebSocketState State => _State;

            public override void Abort()
            {
                _State = WebSocketState.Aborted;
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                _State = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                _State = WebSocketState.CloseSent;
                return Task.CompletedTask;
            }

            public override void Dispose()
            {
                _State = WebSocketState.Closed;
            }

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
