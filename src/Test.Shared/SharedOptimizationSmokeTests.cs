namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using WatsonWebserver;
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

        /// <summary>
        /// Verify HTTP/1.1 cached response headers preserve dynamic fields.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task TestHttp1CachedHeadersAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureCachedHeaderRoutes))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = CreateHttpClient(new Version(1, 1)))
                {
                    HttpResponseMessage firstResponse = await client.GetAsync(new Uri(host.BaseAddress, "/cache?case=first")).ConfigureAwait(false);
                    HttpResponseMessage secondResponse = await client.GetAsync(new Uri(host.BaseAddress, "/cache?case=second")).ConfigureAwait(false);
                    string firstBody = await firstResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    string secondBody = await secondResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (!String.Equals(firstBody, "alpha", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected first response body.");
                    }

                    if (!String.Equals(secondBody, "beta-beta", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Unexpected second response body.");
                    }

                    if (!firstResponse.Headers.Contains("X-Test-Only"))
                    {
                        throw new InvalidOperationException("Expected first response custom header.");
                    }

                    if (secondResponse.Headers.Contains("X-Test-Only"))
                    {
                        throw new InvalidOperationException("Second response should not inherit a cached custom header.");
                    }

                    if (firstResponse.Content.Headers.ContentLength != 5 || secondResponse.Content.Headers.ContentLength != 9)
                    {
                        throw new InvalidOperationException("Content-Length should remain dynamic when header templates are cached.");
                    }
                }
            }
        }

        private static Task NoOpRouteAsync(HttpContextBase context)
        {
            return Task.CompletedTask;
        }

        private static void ConfigureCachedHeaderRoutes(Webserver server)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.GET, "/cache", async (HttpContextBase context) =>
            {
                string testCase = context.Request.RetrieveQueryValue("case");
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";

                if (String.Equals(testCase, "first", StringComparison.Ordinal))
                {
                    context.Response.Headers["X-Test-Only"] = "yes";
                    await context.Response.Send("alpha", context.Token).ConfigureAwait(false);
                }
                else
                {
                    await context.Response.Send("beta-beta", context.Token).ConfigureAwait(false);
                }
            });
        }

        private static HttpClient CreateHttpClient(Version version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            HttpClient client = new HttpClient(handler);
            client.DefaultRequestVersion = version;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            return client;
        }
    }
}
