namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using IpMatcher;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Settings;

    /// <summary>
    /// Coverage for IP matcher ownership and disposal across AccessControlManager, TelemetrySettings,
    /// WebserverSettings, and Webserver, and for the IpMatcher match-cache behavior Watson relies on.
    /// </summary>
    public static class SharedMatcherDisposalTests
    {
        private const string _Network = "10.20.0.0";
        private const string _Netmask = "255.255.0.0";
        private const string _InsideAddress = "10.20.30.40";

        /// <summary>
        /// Get the matcher disposal tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateSync("IpMatcher :: Subnet match populates the match cache", TestSubnetMatchPopulatesCache));
            tests.Add(CreateSync("IpMatcher :: Disposed matcher still matches without caching", TestDisposedMatcherStillMatchesWithoutCaching));
            tests.Add(CreateSync("IpMatcher :: Remove clears cached matches", TestRemoveClearsCachedMatches));

            tests.Add(CreateSync("AccessControlManager :: Replacing DenyList disposes the previous matcher", TestReplacingDenyListDisposesPrevious));
            tests.Add(CreateSync("AccessControlManager :: Replacing PermitList disposes the previous matcher", TestReplacingPermitListDisposesPrevious));
            tests.Add(CreateSync("AccessControlManager :: Assigning null disposes the previous matcher", TestAssigningNullDisposesPrevious));
            tests.Add(CreateSync("AccessControlManager :: Reassigning the same matcher does not dispose it", TestReassigningSameMatcherDoesNotDispose));
            tests.Add(CreateSync("AccessControlManager :: Replacing a matcher shared with the other list does not dispose it", TestReplacingSharedMatcherDoesNotDispose));
            tests.Add(CreateSync("AccessControlManager :: Dispose disposes both lists", TestAccessControlDisposeDisposesBothLists));
            tests.Add(CreateSync("AccessControlManager :: Dispose is idempotent and Permit still works", TestAccessControlDisposeIdempotentAndPermitWorks));
            tests.Add(CreateSync("AccessControlManager :: Removing a deny entry stops denying a cached address", TestRemovingDenyEntryStopsDenyingCachedAddress));

            tests.Add(CreateSync("TelemetrySettings :: Replacing TrustedProxies disposes the previous matcher", TestReplacingTrustedProxiesDisposesPrevious));
            tests.Add(CreateSync("TelemetrySettings :: Reassigning the same TrustedProxies does not dispose it", TestReassigningSameTrustedProxiesDoesNotDispose));
            tests.Add(CreateSync("TelemetrySettings :: Dispose disposes TrustedProxies", TestTelemetryDisposeDisposesTrustedProxies));

            tests.Add(CreateSync("WebserverSettings :: Replacing AccessControl disposes the previous manager", TestReplacingAccessControlDisposesPrevious));
            tests.Add(CreateSync("WebserverSettings :: Replacing Telemetry disposes the previous settings", TestReplacingTelemetryDisposesPrevious));
            tests.Add(CreateSync("WebserverSettings :: Reassigning the same AccessControl and Telemetry does not dispose them", TestReassigningSameSettingsDoesNotDispose));
            tests.Add(CreateSync("WebserverSettings :: Dispose disposes all matchers", TestWebserverSettingsDisposeDisposesAllMatchers));

            tests.Add(CreateSync("Webserver :: Dispose disposes settings matchers and keeps the settings instance", TestWebserverDisposeDisposesSettingsMatchers));
            tests.Add(CreateAsync("Webserver :: Removing a permit entry denies a previously permitted client", TestRemovingPermitEntryDeniesPreviouslyPermittedClientAsync));
            tests.Add(CreateAsync("Webserver :: Access control denial returns the default 403 page", TestAccessControlDenialReturnsDefault403PageAsync));
            tests.Add(CreateAsync("Webserver :: Access control denial returns 403 without a configured 403 page", TestAccessControlDenialWithoutPageReturns403Async));

            return tests.ToArray();
        }

        #region IpMatcher

        private static void TestSubnetMatchPopulatesCache()
        {
            Matcher matcher = CreatePrimedMatcher();
            AssertTrue(matcher.CacheCount > 0, "A subnet match should populate the match cache.");
            matcher.Dispose();
        }

        private static void TestDisposedMatcherStillMatchesWithoutCaching()
        {
            Matcher matcher = CreatePrimedMatcher();
            matcher.Dispose();

            AssertMatcherDisposed(matcher, "A disposed matcher");
            AssertTrue(!matcher.MatchExists("10.21.0.1"), "A disposed matcher should still reject addresses outside the network.");
        }

        private static void TestRemoveClearsCachedMatches()
        {
            Matcher matcher = CreatePrimedMatcher();
            matcher.Remove(_Network);

            AssertEquals(0, matcher.CacheCount, "Remove should clear the match cache.");
            AssertTrue(!matcher.MatchExists(_InsideAddress), "An address matched through a removed entry should no longer match.");
            matcher.Dispose();
        }

        #endregion

        #region AccessControlManager

        private static void TestReplacingDenyListDisposesPrevious()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            manager.DenyList.Add(_Network, _Netmask);
            AssertTrue(!manager.Permit(_InsideAddress), "The deny list should deny the address.");

            Matcher previous = manager.DenyList;
            AssertTrue(previous.CacheCount > 0, "The deny-list match should populate the cache.");

            Matcher replacement = new Matcher();
            manager.DenyList = replacement;

            AssertMatcherDisposed(previous, "The replaced deny list");
            AssertTrue(ReferenceEquals(replacement, manager.DenyList), "The replacement deny list should be retained.");
            AssertTrue(manager.Permit(_InsideAddress), "The empty replacement deny list should permit the address.");
            manager.Dispose();
        }

        private static void TestReplacingPermitListDisposesPrevious()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultDeny);
            manager.PermitList.Add(_Network, _Netmask);
            AssertTrue(manager.Permit(_InsideAddress), "The permit list should permit the address.");

            Matcher previous = manager.PermitList;
            manager.PermitList = new Matcher();

            AssertMatcherDisposed(previous, "The replaced permit list");
            AssertTrue(!manager.Permit(_InsideAddress), "The empty replacement permit list should deny the address.");
            manager.Dispose();
        }

        private static void TestAssigningNullDisposesPrevious()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            Matcher previous = CreatePrimedMatcher();
            manager.DenyList = previous;

            manager.DenyList = null;

            AssertMatcherDisposed(previous, "A deny list replaced by null");
            AssertTrue(manager.DenyList != null, "Assigning null should install an empty matcher.");
            AssertTrue(!ReferenceEquals(previous, manager.DenyList), "Assigning null should install a new matcher.");
            manager.Dispose();
        }

        private static void TestReassigningSameMatcherDoesNotDispose()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            Matcher matcher = CreatePrimedMatcher();
            manager.DenyList = matcher;

            manager.DenyList = matcher;

            AssertMatcherLive(matcher, "A deny list reassigned to itself");
            manager.Dispose();
        }

        private static void TestReplacingSharedMatcherDoesNotDispose()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            Matcher shared = CreatePrimedMatcher();
            manager.DenyList = shared;
            manager.PermitList = shared;

            manager.DenyList = new Matcher();

            AssertMatcherLive(shared, "A matcher still held by the permit list");

            manager.PermitList = new Matcher();

            AssertMatcherDisposed(shared, "A shared matcher no longer held by either list");
            manager.Dispose();
        }

        private static void TestAccessControlDisposeDisposesBothLists()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            Matcher deny = CreatePrimedMatcher();
            Matcher permit = CreatePrimedMatcher();
            manager.DenyList = deny;
            manager.PermitList = permit;

            manager.Dispose();

            AssertMatcherDisposed(deny, "The deny list of a disposed manager");
            AssertMatcherDisposed(permit, "The permit list of a disposed manager");
        }

        private static void TestAccessControlDisposeIdempotentAndPermitWorks()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            manager.DenyList.Add(_Network, _Netmask);

            manager.Dispose();
            manager.Dispose();

            AssertTrue(!manager.Permit(_InsideAddress), "A disposed manager should still deny addresses in the deny list.");
            AssertTrue(manager.Permit("10.21.0.1"), "A disposed manager should still permit addresses outside the deny list.");
        }

        private static void TestRemovingDenyEntryStopsDenyingCachedAddress()
        {
            AccessControlManager manager = new AccessControlManager(AccessControlMode.DefaultPermit);
            manager.DenyList.Add(_Network, _Netmask);
            AssertTrue(!manager.Permit(_InsideAddress), "The deny list should deny the address.");

            manager.DenyList.Remove(_Network);

            AssertTrue(manager.Permit(_InsideAddress), "An address cached through a removed deny entry should be permitted.");
            manager.Dispose();
        }

        #endregion

        #region TelemetrySettings

        private static void TestReplacingTrustedProxiesDisposesPrevious()
        {
            TelemetrySettings settings = new TelemetrySettings();
            settings.TrustedProxies.Add(_Network, _Netmask);
            AssertTrue(settings.TrustedProxies.MatchExists(_InsideAddress), "The trusted proxy list should match the address.");

            Matcher previous = settings.TrustedProxies;
            settings.TrustedProxies = new Matcher();

            AssertMatcherDisposed(previous, "The replaced trusted proxy list");
            AssertTrue(!ReferenceEquals(previous, settings.TrustedProxies), "The replacement trusted proxy list should be retained.");
            settings.Dispose();
        }

        private static void TestReassigningSameTrustedProxiesDoesNotDispose()
        {
            TelemetrySettings settings = new TelemetrySettings();
            Matcher matcher = CreatePrimedMatcher();
            settings.TrustedProxies = matcher;

            settings.TrustedProxies = matcher;

            AssertMatcherLive(matcher, "A trusted proxy list reassigned to itself");
            settings.Dispose();
        }

        private static void TestTelemetryDisposeDisposesTrustedProxies()
        {
            TelemetrySettings settings = new TelemetrySettings();
            Matcher matcher = CreatePrimedMatcher();
            settings.TrustedProxies = matcher;

            settings.Dispose();
            settings.Dispose();

            AssertMatcherDisposed(matcher, "The trusted proxy list of disposed telemetry settings");
        }

        #endregion

        #region WebserverSettings

        private static void TestReplacingAccessControlDisposesPrevious()
        {
            WebserverSettings settings = new WebserverSettings();
            Matcher deny = CreatePrimedMatcher();
            Matcher permit = CreatePrimedMatcher();
            settings.AccessControl.DenyList = deny;
            settings.AccessControl.PermitList = permit;

            settings.AccessControl = new AccessControlManager(AccessControlMode.DefaultDeny);

            AssertMatcherDisposed(deny, "The deny list of a replaced access control manager");
            AssertMatcherDisposed(permit, "The permit list of a replaced access control manager");
            settings.Dispose();
        }

        private static void TestReplacingTelemetryDisposesPrevious()
        {
            WebserverSettings settings = new WebserverSettings();
            Matcher proxies = CreatePrimedMatcher();
            settings.Telemetry.TrustedProxies = proxies;

            settings.Telemetry = new TelemetrySettings();

            AssertMatcherDisposed(proxies, "The trusted proxy list of replaced telemetry settings");
            settings.Dispose();
        }

        private static void TestReassigningSameSettingsDoesNotDispose()
        {
            WebserverSettings settings = new WebserverSettings();
            Matcher deny = CreatePrimedMatcher();
            Matcher proxies = CreatePrimedMatcher();
            settings.AccessControl.DenyList = deny;
            settings.Telemetry.TrustedProxies = proxies;

            settings.AccessControl = settings.AccessControl;
            settings.Telemetry = settings.Telemetry;

            AssertMatcherLive(deny, "The deny list of an access control manager reassigned to itself");
            AssertMatcherLive(proxies, "The trusted proxy list of telemetry settings reassigned to themselves");
            settings.Dispose();
        }

        private static void TestWebserverSettingsDisposeDisposesAllMatchers()
        {
            WebserverSettings settings = new WebserverSettings();
            Matcher deny = CreatePrimedMatcher();
            Matcher permit = CreatePrimedMatcher();
            Matcher proxies = CreatePrimedMatcher();
            settings.AccessControl.DenyList = deny;
            settings.AccessControl.PermitList = permit;
            settings.Telemetry.TrustedProxies = proxies;

            settings.Dispose();
            settings.Dispose();

            AssertMatcherDisposed(deny, "The deny list of disposed settings");
            AssertMatcherDisposed(permit, "The permit list of disposed settings");
            AssertMatcherDisposed(proxies, "The trusted proxy list of disposed settings");
        }

        #endregion

        #region Webserver

        private static void TestWebserverDisposeDisposesSettingsMatchers()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 8000, false);
            Matcher deny = CreatePrimedMatcher();
            Matcher proxies = CreatePrimedMatcher();
            settings.AccessControl.DenyList = deny;
            settings.Telemetry.TrustedProxies = proxies;

            Webserver server = new Webserver(settings, ctx => Task.CompletedTask);
            server.Dispose();

            AssertMatcherDisposed(deny, "The deny list of a disposed server");
            AssertMatcherDisposed(proxies, "The trusted proxy list of a disposed server");
            AssertTrue(ReferenceEquals(settings, server.Settings), "Disposing the server should not replace its settings instance.");
        }

        private static async Task TestRemovingPermitEntryDeniesPreviouslyPermittedClientAsync()
        {
            using (LoopbackServerHost host = new LoopbackServerHost(
                false,
                false,
                false,
                server =>
                {
                    server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/ping", async (ctx) =>
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.Send("pong", ctx.Token).ConfigureAwait(false);
                    });
                },
                settings =>
                {
                    settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
                    settings.AccessControl.PermitList.Add("127.0.0.0", "255.0.0.0");
                }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    Uri uri = new Uri(host.BaseAddress, "/ping");

                    HttpResponseMessage permitted = await client.GetAsync(uri).ConfigureAwait(false);
                    AssertEquals(200, (int)permitted.StatusCode, "A loopback client inside the permit list should be permitted.");
                    AssertTrue(host.Server.Settings.AccessControl.PermitList.CacheCount > 0, "The permitted request should populate the permit-list cache.");

                    host.Server.Settings.AccessControl.PermitList.Remove("127.0.0.0");

                    HttpResponseMessage denied = await client.GetAsync(uri).ConfigureAwait(false);
                    AssertEquals(403, (int)denied.StatusCode, "A client permitted through a removed entry should be denied.");
                }
            }
        }

        private static async Task TestAccessControlDenialReturnsDefault403PageAsync()
        {
            using (LoopbackServerHost host = CreateDenyAllHost(null))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/ping")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    AssertEquals(403, (int)response.StatusCode, "A client outside the permit list should be denied with 403.");
                    AssertEquals(WebserverConstants.PageContent403, body, "The denial should send the default 403 page.");
                    AssertEquals(WebserverConstants.ContentTypeHtml, response.Content.Headers.ContentType?.MediaType, "The denial should use the 403 page content type.");
                }
            }
        }

        private static async Task TestAccessControlDenialWithoutPageReturns403Async()
        {
            using (LoopbackServerHost host = CreateDenyAllHost(server => server.DefaultPages.Pages.Remove(403)))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(new Uri(host.BaseAddress, "/ping")).ConfigureAwait(false);
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    AssertEquals(403, (int)response.StatusCode, "A denial without a configured 403 page should still return 403.");
                    AssertEquals(String.Empty, body, "A denial without a configured 403 page should send an empty body.");
                }
            }
        }

        private static LoopbackServerHost CreateDenyAllHost(Action<Webserver> configureServer)
        {
            return new LoopbackServerHost(
                false,
                false,
                false,
                server =>
                {
                    server.Routes.PreAuthentication.Static.Add(WatsonWebserver.Core.HttpMethod.GET, "/ping", async (ctx) =>
                    {
                        ctx.Response.StatusCode = 200;
                        await ctx.Response.Send("pong", ctx.Token).ConfigureAwait(false);
                    });

                    configureServer?.Invoke(server);
                },
                settings =>
                {
                    settings.AccessControl.Mode = AccessControlMode.DefaultDeny;
                });
        }

        #endregion

        #region Helpers

        private static Matcher CreatePrimedMatcher()
        {
            Matcher matcher = new Matcher();
            matcher.Add(_Network, _Netmask);
            AssertTrue(matcher.MatchExists(_InsideAddress), "The primed matcher should match the inside address.");
            return matcher;
        }

        private static void AssertMatcherDisposed(Matcher matcher, string description)
        {
            AssertEquals(0, matcher.CacheCount, description + " should have released its match cache.");
            AssertTrue(matcher.MatchExists(_InsideAddress), description + " should still match the inside address.");
            AssertEquals(0, matcher.CacheCount, description + " should not cache new matches.");
        }

        private static void AssertMatcherLive(Matcher matcher, string description)
        {
            AssertTrue(matcher.MatchExists(_InsideAddress), description + " should still match the inside address.");
            AssertTrue(matcher.CacheCount > 0, description + " should still be caching matches.");
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

        #endregion
    }
}
