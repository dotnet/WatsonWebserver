namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Routing;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared optimization-focused smoke tests executed by both runners.
    /// </summary>
    public static class SharedOptimizationSmokeTests
    {
        /// <summary>
        /// Verify static route snapshots remain readable during concurrent mutation.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestStaticRouteSnapshotsAsync()
        {
            StaticRouteManager routeManager = new StaticRouteManager();
            Func<HttpContextBase, Task> handler = NoOpRouteAsync;
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 25; i++)
            {
                int pathIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int iteration = 0; iteration < 100; iteration++)
                    {
                        string path = "/snapshot/" + pathIndex.ToString() + "/" + iteration.ToString();
                        routeManager.Add(CoreHttpMethod.GET, path, handler);
                        routeManager.Exists(CoreHttpMethod.GET, path);
                        routeManager.GetAll();
                        routeManager.Match(CoreHttpMethod.GET, path, out StaticRoute route);

                        if (route == null)
                        {
                            throw new InvalidOperationException("Expected route lookup to succeed during mutation.");
                        }

                        routeManager.Remove(CoreHttpMethod.GET, path);
                    }
                }));
            }

            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Verify the default serialization helper preserves pretty and compact JSON semantics.
        /// </summary>
        /// <returns>Task.</returns>
        public static Task TestDefaultSerializationHelperAsync()
        {
            DefaultSerializationHelper serializer = new DefaultSerializationHelper();
            StateObservationResponse payload = new StateObservationResponse();
            payload.TraceHeader = "abc";
            payload.Body = "hello";
            payload.ContentLength = 5;
            payload.ChunkedTransfer = false;

            string compact = serializer.SerializeJson(payload, false);
            string pretty = serializer.SerializeJson(payload, true);

            if (String.IsNullOrEmpty(compact) || String.IsNullOrEmpty(pretty))
            {
                throw new InvalidOperationException("Serialized JSON should not be empty.");
            }

            if (compact.Contains(Environment.NewLine, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Compact JSON should not be indented.");
            }

            if (!pretty.Contains(Environment.NewLine, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Pretty JSON should be indented.");
            }

            StateObservationResponse compactRoundTrip = serializer.DeserializeJson<StateObservationResponse>(compact);
            StateObservationResponse prettyRoundTrip = serializer.DeserializeJson<StateObservationResponse>(pretty);

            if (compactRoundTrip == null || prettyRoundTrip == null)
            {
                throw new InvalidOperationException("Serialized JSON should round-trip to typed instances.");
            }

            if (!String.Equals(compactRoundTrip.TraceHeader, payload.TraceHeader, StringComparison.Ordinal)
                || !String.Equals(prettyRoundTrip.Body, payload.Body, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Round-tripped JSON payload does not match the source instance.");
            }

            return Task.CompletedTask;
        }

        private static Task NoOpRouteAsync(HttpContextBase context)
        {
            return Task.CompletedTask;
        }
    }
}
