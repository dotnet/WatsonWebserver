namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http2;
    using CoreHttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Shared telemetry tests exercised by both runners. Assertions ride the BCL MeterListener and
    /// ActivityListener, so no OpenTelemetry SDK dependency is required. Each test uses a unique meter
    /// and activity-source name so parallel test classes cannot leak measurements or spans into it.
    /// </summary>
    public static class SharedTelemetryTests
    {
        /// <summary>
        /// Get the shared telemetry test cases.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();
            tests.Add(new SharedNamedTestCase("Telemetry :: Request metrics and span", TestRequestMetricsAndSpanAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Forwarded client address", TestForwardedClientAddressAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Forwarded trusted proxy chain", TestForwardedTrustedProxyChainAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Prometheus endpoint scrape", TestPrometheusEndpointAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Disabled emits nothing", TestDisabledEmitsNothingAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Error path span and exception metric", TestErrorPathAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Inbound trace context propagation", TestContextPropagationAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: Request attributes and auth metric", TestRequestAttributesAndAuthAsync));
            tests.Add(new SharedNamedTestCase("Telemetry :: HTTP/2 protocol version tag", TestHttp2ProtocolVersionAsync));
            return tests.ToArray();
        }

        private static async Task TestRequestMetricsAndSpanAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<string> metrics = new ConcurrentBag<string>();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();

            using (MeterListener meterListener = BuildMeterListener(metrics, source))
            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameOnly(source)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/users/123")).ConfigureAwait(false);
                    AssertTrue((int)response.StatusCode == 200, "Expected 200 from the user route.");
                }

                await WaitUntilAsync(delegate { return ContainsEvent(metrics, "duration:route=/users/{id};method=GET;status=200"); }, 5000).ConfigureAwait(false);

                AssertTrue(ContainsEvent(metrics, "duration:route=/users/{id};method=GET;status=200"), "Expected request duration with the route template.");
                AssertTrue(ContainsEvent(metrics, "active:1"), "Expected an active-request increment.");
                AssertTrue(ContainsEvent(metrics, "active:-1"), "Expected an active-request decrement.");
                AssertTrue(ContainsEvent(metrics, "routematch:Parameter"), "Expected a parameter route-match counter.");

                Activity serverSpan = FindSpanWithRoute(spans, "/users/{id}");
                AssertTrue(serverSpan != null, "Expected a server span carrying the route template.");
                AssertTrue(serverSpan.Kind == ActivityKind.Server, "Expected a Server-kind span.");
                AssertTrue(serverSpan.Status == ActivityStatusCode.Ok, "Expected an Ok span status for a 200.");
            }
        }

        private static async Task TestForwardedClientAddressAsync()
        {
            string trustedSource = NewSourceName();
            string untrustedSource = NewSourceName();
            ConcurrentBag<Activity> trustedSpans = new ConcurrentBag<Activity>();
            ConcurrentBag<Activity> untrustedSpans = new ConcurrentBag<Activity>();

            using (ActivityListener activityListener = BuildActivityListener(trustedSpans, trustedSource))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameAnd(trustedSource, EnableForwardedHeaders)))
            {
                await host.StartAsync().ConfigureAwait(false);
                await SendWithForwardedForAsync(host, "203.0.113.9").ConfigureAwait(false);
                await WaitUntilAsync(delegate { return FindSpanWithRoute(trustedSpans, "/users/{id}") != null; }, 5000).ConfigureAwait(false);

                Activity span = FindSpanWithRoute(trustedSpans, "/users/{id}");
                AssertTrue(span != null, "Expected a server span for the trusted case.");
                AssertTrue(GetTag(span, "client.address") == "203.0.113.9", "Expected the forwarded client address to be trusted.");
                AssertTrue(GetTag(span, "network.peer.address") == "127.0.0.1", "Expected the raw socket peer to remain 127.0.0.1.");
            }

            using (ActivityListener activityListener = BuildActivityListener(untrustedSpans, untrustedSource))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameOnly(untrustedSource)))
            {
                await host.StartAsync().ConfigureAwait(false);
                await SendWithForwardedForAsync(host, "203.0.113.9").ConfigureAwait(false);
                await WaitUntilAsync(delegate { return FindSpanWithRoute(untrustedSpans, "/users/{id}") != null; }, 5000).ConfigureAwait(false);

                Activity span = FindSpanWithRoute(untrustedSpans, "/users/{id}");
                AssertTrue(span != null, "Expected a server span for the untrusted case.");
                AssertTrue(GetTag(span, "client.address") == "127.0.0.1", "Expected the forwarded header to be ignored when trust is off.");
            }
        }

        private static async Task TestForwardedTrustedProxyChainAsync()
        {
            string trustedSource = NewSourceName();
            string untrustedSource = NewSourceName();
            ConcurrentBag<Activity> trustedSpans = new ConcurrentBag<Activity>();
            ConcurrentBag<Activity> untrustedSpans = new ConcurrentBag<Activity>();

            using (ActivityListener activityListener = BuildActivityListener(trustedSpans, trustedSource))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameAnd(trustedSource, EnableTrustedProxyChain)))
            {
                await host.StartAsync().ConfigureAwait(false);
                await SendWithForwardedForAsync(host, "203.0.113.9, 10.25.1.10").ConfigureAwait(false);
                await WaitUntilAsync(delegate { return FindSpanWithRoute(trustedSpans, "/users/{id}") != null; }, 5000).ConfigureAwait(false);

                Activity span = FindSpanWithRoute(trustedSpans, "/users/{id}");
                AssertTrue(span != null, "Expected a server span for the trusted proxy-chain case.");
                AssertTrue(GetTag(span, "client.address") == "203.0.113.9", "Expected the client address before the trusted proxy hop.");
            }

            using (ActivityListener activityListener = BuildActivityListener(untrustedSpans, untrustedSource))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameAnd(untrustedSource, EnableTrustedProxyChain)))
            {
                await host.StartAsync().ConfigureAwait(false);
                await SendWithForwardedForAsync(host, "203.0.113.9, 198.51.100.10").ConfigureAwait(false);
                await WaitUntilAsync(delegate { return FindSpanWithRoute(untrustedSpans, "/users/{id}") != null; }, 5000).ConfigureAwait(false);

                Activity span = FindSpanWithRoute(untrustedSpans, "/users/{id}");
                AssertTrue(span != null, "Expected a server span for the untrusted proxy-chain case.");
                AssertTrue(GetTag(span, "client.address") == "198.51.100.10", "Expected traversal to stop at the untrusted proxy hop.");
            }
        }

        private static async Task TestPrometheusEndpointAsync()
        {
            string source = NewSourceName();

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameAnd(source, EnablePrometheus)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage seed = await client.GetAsync(new Uri(host.BaseAddress, "/users/7")).ConfigureAwait(false);
                    AssertTrue((int)seed.StatusCode == 200, "Expected 200 from the seed request.");

                    string body = String.Empty;
                    for (int attempt = 0; attempt < 20; attempt++)
                    {
                        HttpResponseMessage scrape = await client.GetAsync(new Uri(host.BaseAddress, "/metrics")).ConfigureAwait(false);
                        AssertTrue((int)scrape.StatusCode == 200, "Expected 200 from the scrape endpoint.");
                        body = await scrape.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (body.Contains("http_server_request_duration_seconds")) break;
                        await Task.Delay(150).ConfigureAwait(false);
                    }

                    AssertTrue(body.Contains("watson_server_up"), "Expected the server-up gauge in the scrape.");
                    AssertTrue(body.Contains("# TYPE http_server_request_duration_seconds histogram"), "Expected the histogram TYPE header.");
                    AssertTrue(body.Contains("http_server_request_duration_seconds_bucket"), "Expected histogram bucket lines.");
                    AssertTrue(body.Contains("le=\"+Inf\""), "Expected the +Inf histogram bucket.");
                    AssertTrue(body.Contains("http_server_request_duration_seconds_sum"), "Expected the histogram sum line.");
                    AssertTrue(body.Contains("http_server_request_duration_seconds_count"), "Expected the histogram count line.");
                }
            }
        }

        private static async Task TestDisabledEmitsNothingAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<string> metrics = new ConcurrentBag<string>();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();

            using (MeterListener meterListener = BuildMeterListener(metrics, source))
            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameAnd(source, DisableTelemetry)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    await client.GetAsync(new Uri(host.BaseAddress, "/users/9")).ConfigureAwait(false);
                }

                await Task.Delay(500).ConfigureAwait(false);

                AssertTrue(!ContainsEvent(metrics, "duration:"), "Expected no duration metric when telemetry is disabled.");
                AssertTrue(FindSpanWithRoute(spans, "/users/{id}") == null, "Expected no server span when telemetry is disabled.");
            }
        }

        private static async Task TestErrorPathAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<string> metrics = new ConcurrentBag<string>();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();

            using (MeterListener meterListener = BuildMeterListener(metrics, source))
            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureThrowingRoute, NameOnly(source)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/boom")).ConfigureAwait(false);
                    AssertTrue((int)response.StatusCode == 500, "Expected 500 from the throwing route.");
                }

                await WaitUntilAsync(delegate { return ContainsEvent(metrics, "exception:"); }, 5000).ConfigureAwait(false);

                AssertTrue(ContainsEvent(metrics, "exception:System.InvalidOperationException"), "Expected a server-exception metric carrying the error type.");

                Activity span = FindErrorSpan(spans);
                AssertTrue(span != null, "Expected a server span with Error status.");
            }
        }

        private static async Task TestContextPropagationAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();
            string traceId = "0af7651916cd43dd8448eb211c80319c";
            string parentSpanId = "b7ad6b7169203331";

            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureUserRoute, NameOnly(source)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/users/123")))
                {
                    request.Headers.TryAddWithoutValidation("traceparent", "00-" + traceId + "-" + parentSpanId + "-01");
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    AssertTrue((int)response.StatusCode == 200, "Expected 200 from the user route.");
                }

                await WaitUntilAsync(delegate { return FindSpanWithRoute(spans, "/users/{id}") != null; }, 5000).ConfigureAwait(false);

                Activity span = FindSpanWithRoute(spans, "/users/{id}");
                AssertTrue(span != null, "Expected a server span.");
                AssertTrue(span.TraceId.ToString() == traceId, "Expected the span to adopt the inbound trace id.");
                AssertTrue(span.ParentSpanId.ToString() == parentSpanId, "Expected the span parent to be the inbound span id.");
            }
        }

        private static async Task TestRequestAttributesAndAuthAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<string> metrics = new ConcurrentBag<string>();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();

            using (MeterListener meterListener = BuildMeterListener(metrics, source))
            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureAuthAndEcho, NameOnly(source)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                using (StringContent content = new StringContent("hello", Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = await client.PostAsync(new Uri(host.BaseAddress, "/echo"), content).ConfigureAwait(false);
                    AssertTrue((int)response.StatusCode == 200, "Expected 200 from the echo route.");
                }

                await WaitUntilAsync(delegate { return ContainsEvent(metrics, "auth:api;Success;Permitted"); }, 5000).ConfigureAwait(false);

                AssertTrue(ContainsEvent(metrics, "auth:api;Success;Permitted"), "Expected an auth decision metric.");

                Activity span = FindSpanWithTag(spans, "http.request.method", "POST");
                AssertTrue(span != null, "Expected a server span for the echo route.");
                AssertTrue(GetTag(span, "http.request.header.content-type") == "application/json", "Expected the normalized request content type on the span.");
                AssertTrue(Convert.ToInt64(span.GetTagItem("http.request.body.size")) == 5, "Expected the request body size on the span.");
            }
        }

        private static async Task TestHttp2ProtocolVersionAsync()
        {
            string source = NewSourceName();
            ConcurrentBag<Activity> spans = new ConcurrentBag<Activity>();

            using (ActivityListener activityListener = BuildActivityListener(spans, source))
            using (LoopbackServerHost host = new LoopbackServerHost(false, true, false, ConfigureHttp2StaticGet, NameOnly(source)))
            using (TcpClient client = new TcpClient())
            {
                await host.StartAsync().ConfigureAwait(false);
                await client.ConnectAsync("127.0.0.1", host.Port).ConfigureAwait(false);

                using (NetworkStream stream = client.GetStream())
                {
                    Http2RawFrame serverSettings = await Http2SharedTestUtilities.PerformClientHandshakeAsync(stream).ConfigureAwait(false);
                    Http2RawFrame serverAck = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    AssertTrue(serverSettings.Header.Type == Http2FrameType.Settings && serverAck.Header.Type == Http2FrameType.Settings, "Expected HTTP/2 handshake frames.");

                    byte[] headerBytes = Http2SharedTestUtilities.BuildRequestHeaderBlock("GET", "http", "127.0.0.1:" + host.Port.ToString(), "/test/get");
                    Http2RawFrame requestFrame = new Http2RawFrame(
                        new Http2FrameHeader
                        {
                            Length = headerBytes.Length,
                            Type = Http2FrameType.Headers,
                            Flags = (byte)((byte)Http2FrameFlags.EndHeaders | (byte)Http2FrameFlags.EndStreamOrAck),
                            StreamIdentifier = 1
                        },
                        headerBytes);

                    await stream.WriteAsync(Http2FrameSerializer.SerializeFrame(requestFrame)).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);

                    Http2ResponseEnvelope response = await Http2SharedTestUtilities.ReadResponseAsync(stream).ConfigureAwait(false);
                    AssertTrue(response.Headers.Get(":status") == "200", "Expected a 200 HTTP/2 response.");
                }

                await WaitUntilAsync(delegate { return FindSpanWithTag(spans, "network.protocol.version", "2") != null; }, 5000).ConfigureAwait(false);
                AssertTrue(FindSpanWithTag(spans, "network.protocol.version", "2") != null, "Expected a server span tagged with protocol version 2.");
            }
        }

        private static void ConfigureUserRoute(Webserver server)
        {
            server.Get("/users/{id}", delegate (ApiRequest req)
            {
                return Task.FromResult((object)"ok");
            });
        }

        private static void ConfigureThrowingRoute(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/boom", delegate (HttpContextBase ctx)
            {
                throw new InvalidOperationException("telemetry-boom");
            });
        }

        private static void ConfigureAuthAndEcho(Webserver server)
        {
            server.Routes.AuthenticateApiRequest = delegate (HttpContextBase ctx)
            {
                return Task.FromResult(new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    AuthorizationResult = AuthorizationResultEnum.Permitted
                });
            };

            server.Routes.PostAuthentication.Static.Add(CoreHttpMethod.POST, "/echo", async (HttpContextBase ctx) =>
            {
                await ctx.Response.Send(ctx.Request.DataAsString, ctx.Token).ConfigureAwait(false);
            });
        }

        private static void ConfigureHttp2StaticGet(Webserver server)
        {
            server.Routes.PreAuthentication.Static.Add(CoreHttpMethod.GET, "/test/get", async (HttpContextBase context) =>
            {
                await context.Response.Send("GET response", context.Token).ConfigureAwait(false);
            });
        }

        private static Action<WebserverSettings> NameOnly(string source)
        {
            return delegate (WebserverSettings settings)
            {
                settings.Telemetry.MeterName = source;
                settings.Telemetry.ActivitySourceName = source;
            };
        }

        private static Action<WebserverSettings> NameAnd(string source, Action<WebserverSettings> extra)
        {
            return delegate (WebserverSettings settings)
            {
                settings.Telemetry.MeterName = source;
                settings.Telemetry.ActivitySourceName = source;
                extra(settings);
            };
        }

        private static void EnableForwardedHeaders(WebserverSettings settings)
        {
            settings.Telemetry.TrustForwardedHeaders = true;
        }

        private static void EnableTrustedProxyChain(WebserverSettings settings)
        {
            settings.Telemetry.TrustForwardedHeaders = true;
            settings.Telemetry.ForwardLimit = 2;
            settings.Telemetry.TrustedProxies.Add("10.25.0.0", "255.255.0.0");
        }

        private static void EnablePrometheus(WebserverSettings settings)
        {
            settings.Telemetry.Prometheus.Enable = true;
        }

        private static void DisableTelemetry(WebserverSettings settings)
        {
            settings.Telemetry.Enable = false;
        }

        private static async Task SendWithForwardedForAsync(LoopbackServerHost host, string forwardedFor)
        {
            using (HttpClient client = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(host.BaseAddress, "/users/123")))
            {
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                AssertTrue((int)response.StatusCode == 200, "Expected 200 from the forwarded request.");
            }
        }

        private static MeterListener BuildMeterListener(ConcurrentBag<string> metrics, string source)
        {
            MeterListener listener = new MeterListener();
            listener.InstrumentPublished = delegate (Instrument instrument, MeterListener l)
            {
                if (instrument.Meter.Name == source) l.EnableMeasurementEvents(instrument);
            };

            listener.SetMeasurementEventCallback<double>(delegate (Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            {
                if (instrument.Name == "http.server.request.duration")
                {
                    metrics.Add("duration:route=" + Tag(tags, "http.route") + ";method=" + Tag(tags, "http.request.method") + ";status=" + Tag(tags, "http.response.status_code"));
                }
            });

            listener.SetMeasurementEventCallback<long>(delegate (Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
            {
                if (instrument.Name == "http.server.active_requests")
                {
                    metrics.Add("active:" + measurement.ToString());
                }
                else if (instrument.Name == "watson.route.matches")
                {
                    metrics.Add("routematch:" + Tag(tags, "watson.route.type"));
                }
                else if (instrument.Name == "watson.server.exceptions")
                {
                    metrics.Add("exception:" + Tag(tags, "error.type"));
                }
                else if (instrument.Name == "watson.auth.requests")
                {
                    metrics.Add("auth:" + Tag(tags, "watson.auth.mode") + ";" + Tag(tags, "watson.auth.authn") + ";" + Tag(tags, "watson.auth.authz"));
                }
            });

            listener.Start();
            return listener;
        }

        private static ActivityListener BuildActivityListener(ConcurrentBag<Activity> spans, string source)
        {
            ActivityListener listener = new ActivityListener();
            listener.ShouldListenTo = delegate (ActivitySource activitySource) { return activitySource.Name == source; };
            listener.Sample = delegate (ref ActivityCreationOptions<ActivityContext> options) { return ActivitySamplingResult.AllData; };
            listener.ActivityStopped = delegate (Activity activity) { spans.Add(activity); };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }

        private static Activity FindSpanWithRoute(ConcurrentBag<Activity> spans, string route)
        {
            foreach (Activity activity in spans)
            {
                if (GetTag(activity, "http.route") == route) return activity;
            }
            return null;
        }

        private static Activity FindSpanWithTag(ConcurrentBag<Activity> spans, string key, string value)
        {
            foreach (Activity activity in spans)
            {
                if (GetTag(activity, key) == value) return activity;
            }
            return null;
        }

        private static Activity FindErrorSpan(ConcurrentBag<Activity> spans)
        {
            foreach (Activity activity in spans)
            {
                if (activity.Status == ActivityStatusCode.Error) return activity;
            }
            return null;
        }

        private static string GetTag(Activity activity, string key)
        {
            if (activity == null) return null;
            foreach (KeyValuePair<string, string> tag in activity.Tags)
            {
                if (tag.Key == key) return tag.Value;
            }
            return null;
        }

        private static string Tag(ReadOnlySpan<KeyValuePair<string, object>> tags, string key)
        {
            foreach (KeyValuePair<string, object> tag in tags)
            {
                if (tag.Key == key) return tag.Value != null ? tag.Value.ToString() : null;
            }
            return null;
        }

        private static bool ContainsEvent(ConcurrentBag<string> events, string prefix)
        {
            foreach (string entry in events)
            {
                if (entry != null && entry.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (condition()) return;
                await Task.Delay(100).ConfigureAwait(false);
                waited += 100;
            }
        }

        private static string NewSourceName()
        {
            return "Watson.Test." + Guid.NewGuid().ToString("N");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
