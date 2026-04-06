namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;
    using WatsonWebserver.Core.Routing;
    using WatsonWebserver.Core.WebSockets;

    /// <summary>
    /// Shared tests that validate netstandard2.1 compatibility paths.
    /// </summary>
    public static class SharedNetstandard21CompatTests
    {
        /// <summary>
        /// Get the netstandard2.1 compatibility tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("NetStd21 :: Http3RuntimeDetector returns result without throwing", TestHttp3RuntimeDetectorDoesNotThrow));
            tests.Add(CreateSync("NetStd21 :: NormalizeForRuntime disables HTTP/3 when unavailable", TestNormalizeDisablesHttp3WhenUnavailable));
            tests.Add(CreateSync("NetStd21 :: WebSocketHandshakeUtilities.ComputeAcceptKey produces correct RFC value", TestComputeAcceptKeyMatchesRfc));
            tests.Add(CreateSync("NetStd21 :: StaticRouteManager add and match round-trip", TestStaticRouteManagerRoundTrip));
            tests.Add(CreateSync("NetStd21 :: Webserver can be constructed", TestWebserverCanBeConstructed));
            tests.Add(CreateSync("NetStd21 :: Http3RuntimeAvailability default is unavailable", TestHttp3AvailabilityDefault));

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

        private static void TestHttp3RuntimeDetectorDoesNotThrow()
        {
            Http3RuntimeAvailability availability = Http3RuntimeDetector.Detect();
            AssertNotNull(availability, "Detect() should never return null.");
            AssertNotNull(availability.Message, "Message should never be null.");
        }

        private static void TestNormalizeDisablesHttp3WhenUnavailable()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 18899, false);
            settings.Protocols.EnableHttp3 = true;

            Http3RuntimeAvailability unavailable = new Http3RuntimeAvailability
            {
                AssemblyPresent = false,
                IsAvailable = false,
                Message = "Test: QUIC unavailable."
            };

            ProtocolRuntimeNormalizationResult result = WebserverSettingsValidator.NormalizeForRuntime(settings, unavailable, null);
            AssertTrue(result.Http3Disabled, "HTTP/3 should have been disabled.");
            AssertTrue(!settings.Protocols.EnableHttp3, "Protocol setting should reflect disabled HTTP/3.");
        }

        private static void TestComputeAcceptKeyMatchesRfc()
        {
            string requestKey = "dGhlIHNhbXBsZSBub25jZQ==";
            string expected = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";
            string actual = WebSocketHandshakeUtilities.ComputeAcceptKey(requestKey);
            AssertEquals(expected, actual, "ComputeAcceptKey should match RFC 6455 example.");
        }

        private static void TestStaticRouteManagerRoundTrip()
        {
            StaticRouteManager manager = new StaticRouteManager();
            bool handlerInvoked = false;

            manager.Add(HttpMethod.GET, "/test/path", delegate (HttpContextBase ctx)
            {
                handlerInvoked = true;
                return Task.CompletedTask;
            });

            StaticRoute route;
            Func<HttpContextBase, Task> handler = manager.Match(HttpMethod.GET, "/test/path", out route);
            AssertNotNull(handler, "Handler should be found for registered route.");
            AssertNotNull(route, "Route should be returned.");

            handler(null).GetAwaiter().GetResult();
            AssertTrue(handlerInvoked, "Handler should have been invoked.");

            Func<HttpContextBase, Task> missing = manager.Match(HttpMethod.POST, "/test/path", out route);
            AssertTrue(missing == null, "Handler should be null for unregistered method.");
        }

        private static void TestWebserverCanBeConstructed()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 18898, false);
            using (Webserver server = new Webserver(settings, delegate (HttpContextBase ctx) { return Task.CompletedTask; }))
            {
                AssertNotNull(server, "Webserver should be constructable.");
                AssertNotNull(server.Settings, "Settings should be assigned.");
            }
        }

        private static void TestHttp3AvailabilityDefault()
        {
            Http3RuntimeAvailability availability = new Http3RuntimeAvailability();
            AssertTrue(!availability.IsAvailable, "Default availability should be false.");
            AssertTrue(!availability.AssemblyPresent, "Default assembly present should be false.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion failed: " + message);
        }

        private static void AssertEquals(object expected, object actual, string message)
        {
            if (!Object.Equals(expected, actual))
            {
                throw new Exception("Assertion failed: " + message + " Expected: " + expected + " Actual: " + actual);
            }
        }

        private static void AssertNotNull(object value, string message)
        {
            if (value == null) throw new Exception("Assertion failed: " + message);
        }
    }
}
