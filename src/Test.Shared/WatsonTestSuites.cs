namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Central, runner-agnostic catalog of every Watson Webserver test case.
    /// This type is the single source of truth consumed by the Touchstone CLI runner
    /// (Test.Automated), the xUnit adapter (Test.XUnit), and the NUnit adapter (Test.Nunit).
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class WatsonTestSuites
    {
        #region Public-Members

        /// <summary>
        /// The complete, ordered set of test suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();

                suites.Add(NamedSuite(
                    "CoreUnit",
                    "Core Unit Coverage",
                    SharedCoreUnitTests.GetTests()));

                suites.Add(NamedSuite(
                    "RequestParameters",
                    "Request Parameters",
                    SharedRequestParametersTests.GetTests()));

                suites.Add(NamedSuite(
                    "Middleware",
                    "Middleware Pipeline",
                    SharedMiddlewarePipelineTests.GetTests()));

                suites.Add(NamedSuite(
                    "WebSocketServer",
                    "WebSocket Server",
                    SharedWebSocketTests.GetTests()));

                suites.Add(NamedSuite(
                    "WebSocketClient",
                    "WebSocket Client",
                    SharedWebSocketClientTests.GetTests()));

                suites.Add(NamedSuite(
                    "NetStandard21Compat",
                    "netstandard2.1 Compatibility",
                    SharedNetstandard21CompatTests.GetTests()));

                suites.Add(NamedSuite(
                    "OpenApi",
                    "OpenAPI Composition",
                    SharedOpenApiCompositionTests.GetTests()));

                suites.Add(NamedSuite(
                    "Telemetry",
                    "Telemetry",
                    SharedTelemetryTests.GetTests()));

                suites.Add(ApiRoutesSuite());
                suites.Add(LegacySmokeSuite());
                suites.Add(Http2SmokeSuite());
                suites.Add(DataStreamSuite());
                suites.Add(BodyAccessSuite());
                suites.Add(OptimizationSuite());
                suites.Add(RouteMethodparitySuite());
                suites.Add(ProtocolGapSuite());
                suites.Add(LegacyCoverageAggregateSuite());

                return suites;
            }
        }

        #endregion

        #region Private-Suite-Builders

        private static TestSuiteDescriptor ApiRoutesSuite()
        {
            const string suiteId = "ApiRoutes";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "API :: GET returns JSON", SharedApiRouteTests.TestGetReturnsJsonAsync));
            cases.Add(Case(suiteId, "API :: GET extracts path and query parameters", SharedApiRouteTests.TestGetExtractsParametersAsync));
            cases.Add(Case(suiteId, "API :: POST deserializes typed body", SharedApiRouteTests.TestPostDeserializesBodyAsync));
            cases.Add(Case(suiteId, "API :: POST raw body access", SharedApiRouteTests.TestPostRawBodyAsync));
            cases.Add(Case(suiteId, "API :: PUT with typed body", SharedApiRouteTests.TestPutWorksAsync));
            cases.Add(Case(suiteId, "API :: PATCH with typed body", SharedApiRouteTests.TestPatchWorksAsync));
            cases.Add(Case(suiteId, "API :: DELETE with parameter", SharedApiRouteTests.TestDeleteWorksAsync));
            cases.Add(Case(suiteId, "API :: String return is text/plain", SharedApiRouteTests.TestStringReturnTextPlainAsync));
            cases.Add(Case(suiteId, "API :: Null return yields empty 200", SharedApiRouteTests.TestNullReturnEmptyResponseAsync));
            cases.Add(Case(suiteId, "API :: Explicit status code preserved", SharedApiRouteTests.TestExplicitStatusCodeReturnAsync));
            cases.Add(Case(suiteId, "API :: WebserverException returns structured error", SharedApiRouteTests.TestWebserverExceptionReturnsStructuredErrorAsync));
            cases.Add(Case(suiteId, "API :: Unmatched route returns 401 with auth enabled", SharedApiRouteTests.TestUnmatchedRouteReturns401Async));
            cases.Add(Case(suiteId, "API :: Timed-out handler returns 408", SharedApiRouteTests.TestTimeoutReturns408Async));
            cases.Add(Case(suiteId, "API :: Protected route returns 401 without token", SharedApiRouteTests.TestProtectedRouteReturns401WithoutTokenAsync));
            cases.Add(Case(suiteId, "API :: Protected route returns 200 with valid token", SharedApiRouteTests.TestProtectedRouteReturns200WithValidTokenAsync));
            cases.Add(Case(suiteId, "API :: Middleware adds response header", SharedApiRouteTests.TestMiddlewareAddsHeaderAsync));
            cases.Add(Case(suiteId, "API :: Health-check endpoint returns 200", SharedApiRouteTests.TestHealthCheckReturns200Async));

            return new TestSuiteDescriptor(suiteId, "API Routes", cases);
        }

        private static TestSuiteDescriptor LegacySmokeSuite()
        {
            const string suiteId = "LegacySmoke";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "HTTP/1.1 :: Basic GET Request", SharedLegacySmokeTests.TestHttp11BasicGetAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Basic POST Request", SharedLegacySmokeTests.TestHttp11BasicPostAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Body Echo Request", SharedLegacySmokeTests.TestHttp11BodyEchoAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Basic PUT Request", SharedLegacySmokeTests.TestHttp11BasicPutAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Basic DELETE Request", SharedLegacySmokeTests.TestHttp11BasicDeleteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Parameter Route Request", SharedLegacySmokeTests.TestHttp11ParameterRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Query-String Route Request", SharedLegacySmokeTests.TestHttp11QueryStringRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Static Content Route", SharedLegacySmokeTests.TestHttp11StaticContentRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Header Echo Request", SharedLegacySmokeTests.TestHttp11HeaderEchoAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Transfer Response", SharedLegacySmokeTests.TestHttp11ChunkedTransferEncodingAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Edge-Case Response", SharedLegacySmokeTests.TestHttp11ChunkedEdgeCasesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Response Observability", SharedLegacySmokeTests.TestHttp11ChunkedResponseObservabilityAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Request Body Via DataAsBytes", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Request Body Via DataAsString", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Request Body Via ReadBodyAsync", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyReadBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Chunked Request Body Via Manual Chunk Read", SharedLegacySmokeTests.TestHttp11ChunkedRequestBodyManualReadChunkAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Large Chunked Request Body", SharedLegacySmokeTests.TestHttp11LargeChunkedRequestBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Preservation Hello", SharedLegacySmokeTests.TestHttp11DataPreservationHelloAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Preservation Hello CRLF", SharedLegacySmokeTests.TestHttp11DataPreservationHelloCrLfAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Server-Sent Events", SharedLegacySmokeTests.TestHttp11ServerSentEventsAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Server-Sent Events Edge Cases", SharedLegacySmokeTests.TestHttp11ServerSentEventsEdgeCasesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Server-Sent Events Observability", SharedLegacySmokeTests.TestHttp11ServerSentEventsObservabilityAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Disconnect During Large Response", SharedLegacySmokeTests.TestHttp11DisconnectDuringLargeResponseAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Double-Send Response Handling", SharedLegacySmokeTests.TestHttp11DoubleSendResponseAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Exception In Route Handler Returns 500", SharedLegacySmokeTests.TestHttp11ExceptionInRouteHandlerAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Custom Exception Route Sends Response", SharedLegacySmokeTests.TestHttp11CustomExceptionRouteSendsResponseAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Custom Exception Route No-Send Falls Back To Default 500", SharedLegacySmokeTests.TestHttp11CustomExceptionRouteNoSendFallsBackToDefault500Async));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Custom Exception Route Throw Falls Back To Default 500", SharedLegacySmokeTests.TestHttp11CustomExceptionRouteThrowFallsBackToDefault500Async));
            cases.Add(Case(suiteId, "HTTP/1.1 :: PreRouting Exception Uses Custom Exception Route", SharedLegacySmokeTests.TestHttp11PreRoutingExceptionUsesCustomExceptionRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: AuthenticateRequest Exception Uses Custom Exception Route", SharedLegacySmokeTests.TestHttp11AuthenticateRequestExceptionUsesCustomExceptionRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: AuthenticateApiRequest Exception Uses Custom Exception Route", SharedLegacySmokeTests.TestHttp11AuthenticateApiRequestExceptionUsesCustomExceptionRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Empty POST Body", SharedLegacySmokeTests.TestHttp11EmptyPostBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: OPTIONS Preflight", SharedLegacySmokeTests.TestHttp11OptionsPreflightAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Request With Many Headers", SharedLegacySmokeTests.TestHttp11RequestWithManyHeadersAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Unmatched Route Returns 404", SharedLegacySmokeTests.TestHttp11NotFoundRouteAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Expect: 100-continue PUT Request", SharedLegacySmokeTests.TestHttp11ExpectContinueAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: AWS Chunked Content-Encoding Not Rejected", SharedLegacySmokeTests.TestHttp11AwsChunkedContentEncodingNotRejectedAsync));

            return new TestSuiteDescriptor(suiteId, "HTTP/1.1 Legacy Smoke", cases);
        }

        private static TestSuiteDescriptor Http2SmokeSuite()
        {
            const string suiteId = "Http2Smoke";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "HTTP/2 h2c :: Basic GET Request", SharedHttp2SmokeTests.TestHttp2BasicGetAsync));
            cases.Add(Case(suiteId, "HTTP/2 h2c :: Continuation Header Block Request", SharedHttp2SmokeTests.TestHttp2ContinuationHeaderBlockAsync));
            cases.Add(Case(suiteId, "HTTP/2 h2c :: Padded Priority Headers And Data Request", SharedHttp2SmokeTests.TestHttp2PaddedPriorityHeadersAndDataAsync));
            cases.Add(Case(suiteId, "HTTP/2 h2c :: Response Trailers", SharedHttp2SmokeTests.TestHttp2ResponseTrailersAsync));
            cases.Add(Case(suiteId, "HTTP/2 h2c :: Chunked API Response", SharedHttp2SmokeTests.TestHttp2ChunkedApiResponseAsync));
            cases.Add(Case(suiteId, "HTTP/2 h2c :: SSE API Response", SharedHttp2SmokeTests.TestHttp2ServerSentEventsResponseAsync));

            return new TestSuiteDescriptor(suiteId, "HTTP/2 h2c Smoke", cases);
        }

        private static TestSuiteDescriptor DataStreamSuite()
        {
            const string suiteId = "DataStream";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Stream Read Returns EOF", SharedDataStreamTests.TestDataStreamReadReturnsEofAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Stream ReadAsync Returns EOF", SharedDataStreamTests.TestDataStreamReadAsyncReturnsEofAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Stream Large Body", SharedDataStreamTests.TestDataStreamLargeBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: DataAsBytes Still Works", SharedDataStreamTests.TestDataAsBytesStillWorksAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Stream Empty Body", SharedDataStreamTests.TestDataStreamEmptyBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Data Stream Keep-Alive Multiple Requests", SharedDataStreamTests.TestDataStreamKeepAliveMultipleRequestsAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: ReadBodyAsync Through ContentLengthStream", SharedDataStreamTests.TestReadBodyAsyncThroughContentLengthStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: DataAsString Through ContentLengthStream", SharedDataStreamTests.TestDataAsStringThroughContentLengthStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: WebSocket Upgrade With ContentLengthStream", SharedDataStreamTests.TestWebSocketUpgradeWithContentLengthStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: HTTP Body Then WebSocket On Same Server", SharedDataStreamTests.TestHttpBodyThenWebSocketOnSameServerAsync));

            return new TestSuiteDescriptor(suiteId, "Data Stream Access", cases);
        }

        private static TestSuiteDescriptor BodyAccessSuite()
        {
            const string suiteId = "BodyAccess";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "HTTP/1.1 Body :: POST via Data.Read", SharedBodyAccessTests.TestHttp1PostDataStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: POST via Data.ReadAsync", SharedBodyAccessTests.TestHttp1PostDataStreamReadAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: POST via DataAsBytes", SharedBodyAccessTests.TestHttp1PostDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: POST via DataAsString", SharedBodyAccessTests.TestHttp1PostDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: POST via ReadBodyAsync", SharedBodyAccessTests.TestHttp1PostReadBodyAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PUT via Data.Read", SharedBodyAccessTests.TestHttp1PutDataStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PUT via DataAsBytes", SharedBodyAccessTests.TestHttp1PutDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PUT via DataAsString", SharedBodyAccessTests.TestHttp1PutDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PUT via ReadBodyAsync", SharedBodyAccessTests.TestHttp1PutReadBodyAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PATCH via DataAsBytes", SharedBodyAccessTests.TestHttp1PatchDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: PATCH via Data.Read", SharedBodyAccessTests.TestHttp1PatchDataStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: DELETE with body via DataAsBytes", SharedBodyAccessTests.TestHttp1DeleteWithBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Empty body via Data.Read", SharedBodyAccessTests.TestHttp1EmptyBodyStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Single byte via Data.Read", SharedBodyAccessTests.TestHttp1SingleByteBodyStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: 128KB via Data.ReadAsync", SharedBodyAccessTests.TestHttp1LargeBodyStreamAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: 128KB via DataAsBytes", SharedBodyAccessTests.TestHttp1LargeBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Keep-alive 10x stream reads", SharedBodyAccessTests.TestHttp1KeepAliveStreamReadsAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Keep-alive alternating access methods", SharedBodyAccessTests.TestHttp1KeepAliveAlternatingAccessAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Mixed cache bytes-string-async", SharedBodyAccessTests.TestHttp1MixedDataAsBytesDataAsStringReadBodyAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Mixed cache string-async-bytes", SharedBodyAccessTests.TestHttp1MixedDataAsStringReadBodyAsyncDataAsBytes));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Mixed cache async-bytes-string", SharedBodyAccessTests.TestHttp1MixedReadBodyAsyncDataAsBytesDataAsString));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Mixed stream read then readasync", SharedBodyAccessTests.TestHttp1MixedDataReadThenReadAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 Body :: Mixed stream readasync then read", SharedBodyAccessTests.TestHttp1MixedDataReadAsyncThenRead));
            cases.Add(Case(suiteId, "HTTP/2 Body :: POST via DataAsBytes", SharedBodyAccessTests.TestHttp2PostDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: POST via DataAsString", SharedBodyAccessTests.TestHttp2PostDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: POST via ReadBodyAsync", SharedBodyAccessTests.TestHttp2PostReadBodyAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: POST via Data.Read", SharedBodyAccessTests.TestHttp2PostDataStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: POST via Data.ReadAsync", SharedBodyAccessTests.TestHttp2PostDataStreamReadAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: PUT via DataAsBytes", SharedBodyAccessTests.TestHttp2PutDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: PUT via DataAsString", SharedBodyAccessTests.TestHttp2PutDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: PATCH via DataAsBytes", SharedBodyAccessTests.TestHttp2PatchDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: DELETE with body via DataAsBytes", SharedBodyAccessTests.TestHttp2DeleteWithBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Empty body", SharedBodyAccessTests.TestHttp2EmptyBodyAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: 32KB via DataAsBytes", SharedBodyAccessTests.TestHttp2LargeBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: 32KB via Data.ReadAsync", SharedBodyAccessTests.TestHttp2LargeBodyStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: 48KB multi-frame via DataAsBytes", SharedBodyAccessTests.TestHttp2MultiFrameBodyAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Mixed cache bytes-string-async", SharedBodyAccessTests.TestHttp2MixedDataAsBytesDataAsStringReadBodyAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Mixed cache string-async-bytes", SharedBodyAccessTests.TestHttp2MixedDataAsStringReadBodyAsyncDataAsBytes));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Mixed cache async-bytes-string", SharedBodyAccessTests.TestHttp2MixedReadBodyAsyncDataAsBytesDataAsString));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Mixed stream read then readasync", SharedBodyAccessTests.TestHttp2MixedDataReadThenReadAsync));
            cases.Add(Case(suiteId, "HTTP/2 Body :: Mixed stream readasync then read", SharedBodyAccessTests.TestHttp2MixedDataReadAsyncThenRead));
            cases.Add(Case(suiteId, "HTTP/3 Body :: POST via DataAsBytes", SharedBodyAccessTests.TestHttp3PostDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: POST via DataAsString", SharedBodyAccessTests.TestHttp3PostDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: POST via ReadBodyAsync", SharedBodyAccessTests.TestHttp3PostReadBodyAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: POST via Data.Read", SharedBodyAccessTests.TestHttp3PostDataStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: POST via Data.ReadAsync", SharedBodyAccessTests.TestHttp3PostDataStreamReadAsyncAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: PUT via DataAsBytes", SharedBodyAccessTests.TestHttp3PutDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: PUT via DataAsString", SharedBodyAccessTests.TestHttp3PutDataAsStringAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: PATCH via DataAsBytes", SharedBodyAccessTests.TestHttp3PatchDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: DELETE with body via DataAsBytes", SharedBodyAccessTests.TestHttp3DeleteWithBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Empty body", SharedBodyAccessTests.TestHttp3EmptyBodyAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: 32KB via DataAsBytes", SharedBodyAccessTests.TestHttp3LargeBodyDataAsBytesAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: 32KB via Data.ReadAsync", SharedBodyAccessTests.TestHttp3LargeBodyStreamReadAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: 48KB multi-frame via DataAsBytes", SharedBodyAccessTests.TestHttp3MultiFrameBodyAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Mixed cache bytes-string-async", SharedBodyAccessTests.TestHttp3MixedDataAsBytesDataAsStringReadBodyAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Mixed cache string-async-bytes", SharedBodyAccessTests.TestHttp3MixedDataAsStringReadBodyAsyncDataAsBytes));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Mixed cache async-bytes-string", SharedBodyAccessTests.TestHttp3MixedReadBodyAsyncDataAsBytesDataAsString));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Mixed stream read then readasync", SharedBodyAccessTests.TestHttp3MixedDataReadThenReadAsync));
            cases.Add(Case(suiteId, "HTTP/3 Body :: Mixed stream readasync then read", SharedBodyAccessTests.TestHttp3MixedDataReadAsyncThenRead));
            cases.Add(Case(suiteId, "WebSocket Body :: Text echo", SharedBodyAccessTests.TestWebSocketTextEchoAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Binary echo", SharedBodyAccessTests.TestWebSocketBinaryEchoAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Medium text (2KB)", SharedBodyAccessTests.TestWebSocketMediumTextAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Medium binary (3KB)", SharedBodyAccessTests.TestWebSocketMediumBinaryAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Fragmented text assembly", SharedBodyAccessTests.TestWebSocketFragmentedTextAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Fragmented binary assembly", SharedBodyAccessTests.TestWebSocketFragmentedBinaryAsync));
            cases.Add(Case(suiteId, "WebSocket Body :: Interleaved text and binary", SharedBodyAccessTests.TestWebSocketInterleavedTextAndBinaryAsync));

            return new TestSuiteDescriptor(suiteId, "Body Access (All Protocols)", cases);
        }

        private static TestSuiteDescriptor OptimizationSuite()
        {
            const string suiteId = "OptimizationCoverage";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "Routing :: Static Route Snapshots Remain Readable During Concurrent Mutation", SharedOptimizationSmokeTests.TestStaticRouteSnapshotsAsync));
            cases.Add(Case(suiteId, "Serialization :: Default Helper Preserves Pretty And Compact JSON", SharedOptimizationSmokeTests.TestDefaultSerializationHelperAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Cached Response Headers Preserve Dynamic Fields", SharedOptimizationSmokeTests.TestHttp1CachedHeadersAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Context Timing Starts At Request Entry", SharedOptimizationSmokeTests.TestContextTimestampStartsAtRequestEntryAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Keep-Alive Pooling Resets Request State", SharedOptimizationSmokeTests.TestHttp1KeepAlivePoolingAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Stream Send Preserves Direct Passthrough Body", SharedOptimizationSmokeTests.TestHttp1StreamSendAsync));
            cases.Add(Case(suiteId, "HTTP/2 :: Lazy Header Materialization Stays Coherent", SharedOptimizationSmokeTests.TestHttp2LazyHeaderMaterializationAsync));
            cases.Add(Case(suiteId, "HTTP/3 :: Lazy Header Materialization Stays Coherent", SharedOptimizationSmokeTests.TestHttp3LazyHeaderMaterializationAsync));

            return new TestSuiteDescriptor(suiteId, "Optimization Coverage", cases);
        }

        private static TestSuiteDescriptor RouteMethodparitySuite()
        {
            const string suiteId = "RouteMethodParity";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "Parity :: GET Static Route", SharedRouteMethodParityTests.RunGetStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: POST Static Route", SharedRouteMethodParityTests.RunPostStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: PUT Static Route", SharedRouteMethodParityTests.RunPutStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: DELETE Static Route", SharedRouteMethodParityTests.RunDeleteStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: PATCH Static Route", SharedRouteMethodParityTests.RunPatchStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: HEAD Static Route", SharedRouteMethodParityTests.RunHeadStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: OPTIONS Static Route", SharedRouteMethodParityTests.RunOptionsStaticRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: GET Parameter Route", SharedRouteMethodParityTests.RunGetParameterRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: POST Parameter Route", SharedRouteMethodParityTests.RunPostParameterRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: GET Dynamic Route", SharedRouteMethodParityTests.RunGetDynamicRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: GET Content Route", SharedRouteMethodParityTests.RunGetContentRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: GET API Route", SharedRouteMethodParityTests.RunGetApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: POST API Route", SharedRouteMethodParityTests.RunPostApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: PUT API Route", SharedRouteMethodParityTests.RunPutApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: PATCH API Route", SharedRouteMethodParityTests.RunPatchApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: DELETE API Route", SharedRouteMethodParityTests.RunDeleteApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: HEAD API Route", SharedRouteMethodParityTests.RunHeadApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: OPTIONS API Route", SharedRouteMethodParityTests.RunOptionsApiRouteParityAsync));
            cases.Add(Case(suiteId, "Parity :: Not Found", SharedRouteMethodParityTests.RunNotFoundParityAsync));

            return new TestSuiteDescriptor(suiteId, "Route Method Parity", cases);
        }

        private static TestSuiteDescriptor ProtocolGapSuite()
        {
            const string suiteId = "ProtocolGap";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(Case(suiteId, "HTTP/2 :: Writer Serialization Correctness", ProtocolGapSharedTests.RunHttp2WriterSerializationCorrectnessAsync));
            cases.Add(Case(suiteId, "HTTP/1.1 :: Caller Disconnect Cancels Active Request", ProtocolGapSharedTests.RunHttp1CallerDisconnectCancelsActiveRequestAsync));
            cases.Add(Case(suiteId, "HTTP/2 :: RST_STREAM Cancels Active Request", ProtocolGapSharedTests.RunHttp2RstStreamCancelsActiveRequestAsync));
            cases.Add(Case(suiteId, "HTTP/3 :: Transport Backpressure Behavior", ProtocolGapSharedTests.RunHttp3TransportBackpressureAsync));
            cases.Add(Case(suiteId, "HTTP/3 :: Sibling Stream Survival After Abort", ProtocolGapSharedTests.RunHttp3SiblingStreamSurvivalAsync));
            cases.Add(Case(suiteId, "Cross-Protocol :: Auth, Session, And Event Parity", ProtocolGapSharedTests.RunCrossProtocolAuthSessionEventParityAsync));
            cases.Add(Case(suiteId, "Interop :: Mixed-Version Client Interoperability", ProtocolGapSharedTests.RunMixedVersionClientInteroperabilityAsync));

            return new TestSuiteDescriptor(suiteId, "Protocol Gap Coverage", cases);
        }

        private static TestSuiteDescriptor LegacyCoverageAggregateSuite()
        {
            const string suiteId = "LegacyCoverage";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(new TestCaseDescriptor(
                suiteId,
                "FullAutomatedCoverage",
                "Comprehensive Automated Coverage (HTTP/1.1, HTTP/2, HTTP/3, TLS, wire protocol)",
                RunLegacyCoverageAsync));

            return new TestSuiteDescriptor(suiteId, "Comprehensive Legacy Coverage", cases);
        }

        #endregion

        #region Private-Helpers

        private static async Task RunLegacyCoverageAsync(System.Threading.CancellationToken token)
        {
            LegacyCoverageSuite suite = new LegacyCoverageSuite();
            IReadOnlyList<AutomatedTestResult> results = await suite.RunAsync().ConfigureAwait(false);

            List<AutomatedTestResult> failures = new List<AutomatedTestResult>();
            for (int i = 0; i < results.Count; i++)
            {
                if (!results[i].Passed)
                {
                    failures.Add(results[i]);
                }
            }

            if (failures.Count > 0)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(failures.Count.ToString());
                builder.Append(" of ");
                builder.Append(results.Count.ToString());
                builder.Append(" comprehensive coverage assertions failed:");

                for (int i = 0; i < failures.Count; i++)
                {
                    builder.Append(Environment.NewLine);
                    builder.Append("  - ");
                    builder.Append(failures[i].TestName);

                    if (!String.IsNullOrEmpty(failures[i].ErrorMessage))
                    {
                        builder.Append(": ");
                        builder.Append(failures[i].ErrorMessage);
                    }
                }

                throw new Exception(builder.ToString());
            }
        }

        private static TestSuiteDescriptor NamedSuite(string suiteId, string displayName, IReadOnlyList<SharedNamedTestCase> namedCases)
        {
            if (String.IsNullOrEmpty(suiteId)) throw new ArgumentNullException(nameof(suiteId));
            if (String.IsNullOrEmpty(displayName)) throw new ArgumentNullException(nameof(displayName));
            if (namedCases == null) throw new ArgumentNullException(nameof(namedCases));

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            for (int i = 0; i < namedCases.Count; i++)
            {
                SharedNamedTestCase named = namedCases[i];
                cases.Add(Case(suiteId, named.Name, named.ExecuteAsync));
            }

            return new TestSuiteDescriptor(suiteId, displayName, cases);
        }

        private static TestCaseDescriptor Case(string suiteId, string displayName, Func<Task> executeAsync)
        {
            if (String.IsNullOrEmpty(suiteId)) throw new ArgumentNullException(nameof(suiteId));
            if (String.IsNullOrEmpty(displayName)) throw new ArgumentNullException(nameof(displayName));
            if (executeAsync == null) throw new ArgumentNullException(nameof(executeAsync));

            return new TestCaseDescriptor(
                suiteId,
                displayName,
                displayName,
                delegate (System.Threading.CancellationToken token)
                {
                    return executeAsync();
                });
        }

        #endregion
    }
}
