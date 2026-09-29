namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.Routing;
    using WatsonWebserver.Core.WebSockets;
    using WatsonWebserver.Extensions.HostBuilderExtension;
    using HttpMethod = WatsonWebserver.Core.HttpMethod;

    /// <summary>
    /// Coverage for catch-all ({*name}) routing and the UrlMatcher 3.1.0 capabilities as Watson exposes them:
    /// parameter route and WebSocket route managers, route precedence, registration-time validation,
    /// API routes, low-level routes, authentication groups, HostBuilder, OpenAPI, and end-to-end HTTP and WebSocket requests.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class SharedCatchAllRoutingTests
    {
        #region Public-Methods

        /// <summary>
        /// Get the catch-all routing tests.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            // ParameterRouteManager matching
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all captures several segments", delegate { AssertParameterMatch("/files/{*path}", "/files/a/b/c.txt", "path", "a/b/c.txt"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all captures one segment", delegate { AssertParameterMatch("/files/{*path}", "/files/a", "path", "a"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all matches the prefix with an empty value", delegate { AssertParameterMatch("/files/{*path}", "/files", "path", ""); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all matches the prefix with a trailing slash", delegate { AssertParameterMatch("/files/{*path}", "/files/", "path", ""); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all keeps repeated slashes", delegate { AssertParameterMatch("/files/{*path}", "/files/a//b", "path", "a//b"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all keeps a trailing slash", delegate { AssertParameterMatch("/files/{*path}", "/files/a/b/", "path", "a/b/"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all does not decode", delegate { AssertParameterMatch("/files/{*path}", "/files/a%2Fb/c%20d", "path", "a%2Fb/c%20d"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all excludes the query string", delegate { AssertParameterMatch("/files/{*path}", "/files/a/b?x=1", "path", "a/b"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Root catch-all matches the root", delegate { AssertParameterMatch("/{*path}", "/", "path", ""); }));
            tests.Add(CreateSync("ParameterRouteManager :: Root catch-all matches any path", delegate { AssertParameterMatch("/{*path}", "/x/y/z", "path", "x/y/z"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Parameters before a catch-all are captured", TestParameterThenCatchAll));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all name lookup is case-insensitive", TestCatchAllNameCaseInsensitive));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all prefix is case-sensitive", delegate { AssertParameterNoMatch("/files/{*path}", HttpMethod.GET, "/FILES/a"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all prefix must match exactly", delegate { AssertParameterNoMatch("/files/{*path}", HttpMethod.GET, "/filesx/a"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all needs its fixed segments", delegate { AssertParameterNoMatch("/{v}/files/{*path}", HttpMethod.GET, "/v1"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all does not match another method", delegate { AssertParameterNoMatch("/files/{*path}", HttpMethod.POST, "/files/a"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Failed match leaves no partial values", TestFailedMatchLeavesNoPartialValues));

            // precedence
            tests.Add(CreateSync("ParameterRouteManager :: Specific route registered after a catch-all wins", TestSpecificRouteAfterCatchAllWins));
            tests.Add(CreateSync("ParameterRouteManager :: Literal route registered after a catch-all wins", TestLiteralRouteAfterCatchAllWins));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all handles what specific routes do not", TestCatchAllHandlesRemainder));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all routes are evaluated in registration order", TestCatchAllsInRegistrationOrder));
            tests.Add(CreateSync("ParameterRouteManager :: Non-catch-all routes keep registration order", TestNonCatchAllsInRegistrationOrder));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-alls for different methods are independent", TestCatchAllPerMethod));

            // registration and route lifecycle
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all not last is rejected at Add", delegate { AssertAddRejected("/{*rest}/edit", "must be the last segment"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Two catch-alls are rejected at Add", delegate { AssertAddRejected("/{*a}/{*b}", "only one catch-all is allowed"); }));
            tests.Add(CreateSync("ParameterRouteManager :: Catch-all inside a segment is rejected at Add", delegate { AssertAddRejected("/files/v{*x}", "must be the entire segment"); }));
            tests.Add(CreateSync("ParameterRouteManager :: {*} is a literal, not a catch-all", TestStarBracesLiteral));
            tests.Add(CreateSync("ParameterRouteManager :: Get, Exists, GetAll, and Remove work for catch-all routes", TestCatchAllLifecycle));
            tests.Add(CreateSync("ParameterRoute :: Constructor rejects an invalid catch-all", TestParameterRouteConstructorRejects));
            tests.Add(CreateSync("ParameterRoute :: Changing Path recompiles the pattern", TestParameterRoutePathChange));
            tests.Add(CreateSync("ParameterRoute :: Changing Method recompiles the pattern", TestParameterRouteMethodChange));
            tests.Add(CreateSync("ParameterRoute :: Setting an invalid Path throws and keeps the old path", TestParameterRouteInvalidPathKeepsOld));
            tests.Add(CreateSync("ParameterRoute :: Compiled pattern is not serialized", TestParameterRouteSerialization));
            tests.Add(CreateSync("Webserver :: API route with an invalid catch-all is rejected", TestApiRouteRegistrationRejected));
            tests.Add(CreateSync("HostBuilder :: MapParameterRoute supports catch-all routes", TestHostBuilderCatchAll));
            tests.Add(CreateSync("HostBuilder :: MapParameterRoute rejects an invalid catch-all", TestHostBuilderRejects));

            // WebSocketRouteManager
            tests.Add(CreateSync("WebSocketRouteManager :: Catch-all captures the normalized remainder", TestWebSocketCatchAll));
            tests.Add(CreateSync("WebSocketRouteManager :: Catch-all matches the prefix with an empty value", TestWebSocketCatchAllEmpty));
            tests.Add(CreateSync("WebSocketRouteManager :: Parameter route registered after a catch-all wins", TestWebSocketPrecedence));
            tests.Add(CreateSync("WebSocketRouteManager :: Static route wins over a catch-all", TestWebSocketStaticWins));
            tests.Add(CreateSync("WebSocketRouteManager :: Invalid catch-all is rejected at Add", TestWebSocketRejects));
            tests.Add(CreateSync("WebSocketRoute :: Changing Path recompiles the pattern", TestWebSocketRoutePathChange));

            // OpenAPI
            tests.Add(CreateSync("OpenAPI :: Catch-all route documented as a path parameter", TestOpenApiCatchAll));

            // end-to-end HTTP
            tests.Add(CreateAsync("HTTP :: Catch-all captures several segments", delegate { return AssertHttpAsync("/files/a/b/c.txt", HttpStatusCode.OK, "files:[a/b/c.txt]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all captures one segment", delegate { return AssertHttpAsync("/files/a", HttpStatusCode.OK, "files:[a]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all matches the prefix with an empty value", delegate { return AssertHttpAsync("/files", HttpStatusCode.OK, "files:[]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all matches the prefix with a trailing slash", delegate { return AssertHttpAsync("/files/", HttpStatusCode.OK, "files:[]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all keeps repeated slashes", delegate { return AssertHttpAsync("/files/a//b", HttpStatusCode.OK, "files:[a//b]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all keeps a trailing slash", delegate { return AssertHttpAsync("/files/a/b/", HttpStatusCode.OK, "files:[a/b/]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all does not decode", delegate { return AssertHttpAsync("/files/a%2Fb/c%20d", HttpStatusCode.OK, "files:[a%2Fb/c%20d]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all excludes the query string", delegate { return AssertHttpAsync("/files/a/b?x=1&y=/z", HttpStatusCode.OK, "files:[a/b]"); }));
            // this host enables API authentication, so a request no pre-authentication route matches falls through to the auth phase (HTTP 401)
            tests.Add(CreateAsync("HTTP :: Catch-all prefix is case-sensitive", delegate { return AssertHttpAsync("/FILES/a", HttpStatusCode.Unauthorized, null); }));
            tests.Add(CreateAsync("HTTP :: Specific route registered after a catch-all wins", delegate { return AssertHttpAsync("/files/special/42", HttpStatusCode.OK, "special:42"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all handles what the specific route does not", delegate { return AssertHttpAsync("/files/special/42/more", HttpStatusCode.OK, "files:[special/42/more]"); }));
            tests.Add(CreateAsync("HTTP :: Catch-all per method", TestHttpCatchAllPerMethodAsync));
            tests.Add(CreateAsync("HTTP :: API route catch-all", delegate { return AssertHttpAsync("/api/x/y", HttpStatusCode.OK, "api:x/y"); }));
            tests.Add(CreateAsync("HTTP :: API route registered after an API catch-all wins", delegate { return AssertHttpAsync("/api/users/7", HttpStatusCode.OK, "user:7"); }));
            tests.Add(CreateAsync("HTTP :: Parameter and catch-all in one route", delegate { return AssertHttpAsync("/v2/docs/guide/intro.md", HttpStatusCode.OK, "docs:v2|guide/intro.md"); }));
            tests.Add(CreateAsync("HTTP :: Parameter and catch-all with an empty remainder", delegate { return AssertHttpAsync("/v2/docs", HttpStatusCode.OK, "docs:v2|"); }));
            tests.Add(CreateAsync("HTTP :: Authenticated catch-all requires authentication", TestHttpAuthenticatedCatchAllAsync));
            tests.Add(CreateAsync("HTTP :: Root catch-all serves unmatched paths and static routes still win", TestHttpRootCatchAllAsync));

            // end-to-end WebSocket
            tests.Add(CreateAsync("WebSocket :: Catch-all route receives the remainder", TestWebSocketEndToEndCatchAllAsync));

            return tests.ToArray();
        }

        #endregion

        #region Private-Methods

        private static SharedNamedTestCase CreateSync(string name, Action action)
        {
            return new SharedNamedTestCase(name, delegate
            {
                action();
                return Task.CompletedTask;
            });
        }

        private static SharedNamedTestCase CreateAsync(string name, Func<Task> func)
        {
            return new SharedNamedTestCase(name, func);
        }

        private static Task Handler(HttpContextBase ctx)
        {
            return Task.CompletedTask;
        }

        private static Task WebSocketHandler(HttpContextBase ctx, WebSocketSession session)
        {
            return Task.CompletedTask;
        }

        #region Manager-Tests

        private static void AssertParameterMatch(string pattern, string url, string name, string expected)
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, pattern, Handler);

            Func<HttpContextBase, Task> handler = manager.Match(HttpMethod.GET, url, out NameValueCollection vals, out ParameterRoute route);
            AssertTrue(handler != null, "Expected " + url + " to match " + pattern + ".");
            AssertEquals(pattern, route.Path, "Unexpected matched route.");
            AssertEquals(1, vals.Count, "Unexpected captured key count for " + url + ".");
            AssertEquals(expected, vals[name], "Unexpected value of '" + name + "' for " + url + ".");
        }

        private static void AssertParameterNoMatch(string pattern, HttpMethod method, string url)
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, pattern, Handler);

            Func<HttpContextBase, Task> handler = manager.Match(method, url, out NameValueCollection _, out ParameterRoute route);
            AssertTrue(handler == null, "Expected " + method + " " + url + " not to match " + pattern + ".");
            AssertTrue(route == null, "Expected no matched route.");
        }

        private static void TestParameterThenCatchAll()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/{v}/files/{*path}", Handler);

            manager.Match(HttpMethod.GET, "/v1/files/a/b.txt", out NameValueCollection vals, out ParameterRoute route);
            AssertTrue(route != null, "Expected a match.");
            AssertEquals(2, vals.Count, "Unexpected captured key count.");
            AssertEquals("v1", vals["v"], "Unexpected version.");
            AssertEquals("a/b.txt", vals["path"], "Unexpected path.");
        }

        private static void TestCatchAllNameCaseInsensitive()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*Path}", Handler);

            manager.Match(HttpMethod.GET, "/files/a/b", out NameValueCollection vals, out ParameterRoute _);
            AssertEquals("a/b", vals["path"], "Lowercase lookup failed.");
            AssertEquals("a/b", vals["PATH"], "Uppercase lookup failed.");
        }

        private static void TestFailedMatchLeavesNoPartialValues()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/{v}/admins/{id}", Handler);
            manager.Add(HttpMethod.GET, "/{v}/files/{*path}", Handler);

            Func<HttpContextBase, Task> handler = manager.Match(HttpMethod.GET, "/v1/users/42", out NameValueCollection vals, out ParameterRoute route);
            AssertTrue(handler == null, "Expected no match.");
            AssertTrue(vals == null || vals.Count == 0, "A failed match must not leave values captured before the failing segment.");
        }

        private static void TestSpecificRouteAfterCatchAllWins()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/api/{*rest}", Handler);
            manager.Add(HttpMethod.GET, "/api/users/{id}", Handler);

            manager.Match(HttpMethod.GET, "/api/users/42", out NameValueCollection vals, out ParameterRoute route);
            AssertEquals("/api/users/{id}", route.Path, "The specific route should win over the earlier catch-all.");
            AssertEquals("42", vals["id"], "Unexpected id.");
        }

        private static void TestLiteralRouteAfterCatchAllWins()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/api/{*rest}", Handler);
            manager.Add(HttpMethod.GET, "/api/health", Handler);

            manager.Match(HttpMethod.GET, "/api/health", out NameValueCollection vals, out ParameterRoute route);
            AssertEquals("/api/health", route.Path, "The literal route should win over the earlier catch-all.");
            AssertEquals(0, vals.Count, "A literal route captures nothing.");
        }

        private static void TestCatchAllHandlesRemainder()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/api/{*rest}", Handler);
            manager.Add(HttpMethod.GET, "/api/users/{id}", Handler);

            manager.Match(HttpMethod.GET, "/api/users/42/orders", out NameValueCollection vals, out ParameterRoute route);
            AssertEquals("/api/{*rest}", route.Path, "The catch-all should handle paths the specific route does not.");
            AssertEquals("users/42/orders", vals["rest"], "Unexpected remainder.");
        }

        private static void TestCatchAllsInRegistrationOrder()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/api/{*a}", Handler);
            manager.Add(HttpMethod.GET, "/api/v1/{*b}", Handler);

            manager.Match(HttpMethod.GET, "/api/v1/x", out NameValueCollection vals, out ParameterRoute route);
            AssertEquals("/api/{*a}", route.Path, "Catch-all routes should be evaluated in registration order.");
            AssertEquals("v1/x", vals["a"], "Unexpected remainder.");
        }

        private static void TestNonCatchAllsInRegistrationOrder()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/{a}/x", Handler);
            manager.Add(HttpMethod.GET, "/y/{b}", Handler);

            manager.Match(HttpMethod.GET, "/y/x", out NameValueCollection vals, out ParameterRoute route);
            AssertEquals("/{a}/x", route.Path, "Non-catch-all routes should keep registration order.");
            AssertEquals("y", vals["a"], "Unexpected value.");
        }

        private static void TestCatchAllPerMethod()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*path}", Handler);
            manager.Add(HttpMethod.POST, "/files/{*path}", Handler);

            manager.Match(HttpMethod.GET, "/files/a", out NameValueCollection _, out ParameterRoute getRoute);
            manager.Match(HttpMethod.POST, "/files/a", out NameValueCollection _, out ParameterRoute postRoute);
            Func<HttpContextBase, Task> put = manager.Match(HttpMethod.PUT, "/files/a", out NameValueCollection _, out ParameterRoute _);

            AssertEquals(HttpMethod.GET, getRoute.Method, "Unexpected GET route.");
            AssertEquals(HttpMethod.POST, postRoute.Method, "Unexpected POST route.");
            AssertTrue(put == null, "PUT should not match.");
        }

        private static void AssertAddRejected(string path, string messageFragment)
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            ArgumentException e = AssertThrows<ArgumentException>(delegate { manager.Add(HttpMethod.GET, path, Handler); }, "Add " + path);

            AssertContains(e.Message, "'" + path + "'", "The message should name the registered path, not the method-prefixed form.");
            AssertContains(e.Message, messageFragment, "Unexpected message.");
            AssertEquals(0, manager.GetAll().Count, "A rejected route must not be added.");
            AssertTrue(!manager.Exists(HttpMethod.GET, path), "A rejected route must not exist.");
        }

        private static void TestStarBracesLiteral()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*}", Handler);

            Func<HttpContextBase, Task> value = manager.Match(HttpMethod.GET, "/files/42", out NameValueCollection _, out ParameterRoute _);
            Func<HttpContextBase, Task> literal = manager.Match(HttpMethod.GET, "/files/{*}", out NameValueCollection vals, out ParameterRoute _);

            AssertTrue(value == null, "{*} must not capture a value.");
            AssertTrue(literal != null, "{*} must match itself literally.");
            AssertEquals(0, vals.Count, "A literal captures nothing.");
        }

        private static void TestCatchAllLifecycle()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*path}", Handler);

            AssertTrue(manager.Exists(HttpMethod.GET, "/files/{*path}"), "Exists should find the catch-all route.");
            AssertEquals("/files/{*path}", manager.Get(HttpMethod.GET, "/files/{*path}").Path, "Get should return the catch-all route.");
            AssertEquals(1, manager.GetAll().Count, "GetAll count.");
            AssertEquals("/files/{*path}", manager.GetAll()[0].Path, "GetAll should report the original path.");

            manager.Remove(HttpMethod.GET, "/files/{*path}");
            Func<HttpContextBase, Task> handler = manager.Match(HttpMethod.GET, "/files/a", out NameValueCollection _, out ParameterRoute _);
            AssertTrue(handler == null, "A removed catch-all must not match.");
            AssertEquals(0, manager.GetAll().Count, "GetAll count after Remove.");
        }

        private static void TestParameterRouteConstructorRejects()
        {
            ArgumentException e = AssertThrows<ArgumentException>(delegate { new ParameterRoute(HttpMethod.GET, "/{*a}/b", Handler); }, "new ParameterRoute");
            AssertContains(e.Message, "'/{*a}/b'", "The message should name the path.");
        }

        private static void TestParameterRoutePathChange()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/old/{id}", Handler);
            ParameterRoute route = manager.Get(HttpMethod.GET, "/old/{id}");
            route.Path = "/new/{*rest}";

            Func<HttpContextBase, Task> oldMatch = manager.Match(HttpMethod.GET, "/old/1", out NameValueCollection _, out ParameterRoute _);
            manager.Match(HttpMethod.GET, "/new/a/b", out NameValueCollection vals, out ParameterRoute matched);

            AssertTrue(oldMatch == null, "The old path must no longer match.");
            AssertTrue(matched != null, "The new path must match.");
            AssertEquals("a/b", vals["rest"], "Unexpected remainder.");
        }

        private static void TestParameterRouteMethodChange()
        {
            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*path}", Handler);
            manager.Get(HttpMethod.GET, "/files/{*path}").Method = HttpMethod.DELETE;

            Func<HttpContextBase, Task> get = manager.Match(HttpMethod.GET, "/files/a", out NameValueCollection _, out ParameterRoute _);
            Func<HttpContextBase, Task> delete = manager.Match(HttpMethod.DELETE, "/files/a", out NameValueCollection _, out ParameterRoute _);

            AssertTrue(get == null, "GET must no longer match.");
            AssertTrue(delete != null, "DELETE must match.");
        }

        private static void TestParameterRouteInvalidPathKeepsOld()
        {
            ParameterRoute route = new ParameterRoute(HttpMethod.GET, "/files/{*path}", Handler);
            AssertThrows<ArgumentException>(delegate { route.Path = "/{*path}/x"; }, "set Path");
            AssertEquals("/files/{*path}", route.Path, "An invalid Path must not replace the old one.");

            ParameterRouteManager manager = new ParameterRouteManager();
            manager.Add(HttpMethod.GET, "/files/{*path}", Handler);
            ParameterRoute registered = manager.Get(HttpMethod.GET, "/files/{*path}");
            AssertThrows<ArgumentException>(delegate { registered.Path = "/{*a}/{*b}"; }, "set Path on a registered route");
            Func<HttpContextBase, Task> handler = manager.Match(HttpMethod.GET, "/files/a", out NameValueCollection vals, out ParameterRoute _);
            AssertTrue(handler != null, "The registered route must keep matching its old path.");
            AssertEquals("a", vals["path"], "Unexpected remainder.");
        }

        private static void TestParameterRouteSerialization()
        {
            ParameterRoute route = new ParameterRoute(HttpMethod.GET, "/files/{*path}", Handler);
            string json = JsonSerializer.Serialize(route);
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                AssertEquals("/files/{*path}", doc.RootElement.GetProperty("Path").GetString(), "Serialized Path.");
                AssertTrue(!doc.RootElement.TryGetProperty("Pattern", out JsonElement _), "The compiled pattern must not be serialized.");
            }
        }

        private static void TestApiRouteRegistrationRejected()
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", 1, false);
            using (Webserver server = new Webserver(settings, Handler))
            {
                AssertThrows<ArgumentException>(delegate { server.Get("/{*rest}/edit", req => Task.FromResult<object>(null)); }, "server.Get");
                AssertThrows<ArgumentException>(delegate { server.Post("/a/{*x}/{*y}", req => Task.FromResult<object>(null), auth: true); }, "server.Post auth");
                AssertEquals(0, server.Routes.PreAuthentication.Parameter.GetAll().Count, "No pre-authentication route should be added.");
                AssertEquals(0, server.Routes.PostAuthentication.Parameter.GetAll().Count, "No post-authentication route should be added.");

                server.Get("/files/{*path}", req => Task.FromResult<object>(null));
                AssertEquals(1, server.Routes.PreAuthentication.Parameter.GetAll().Count, "A valid catch-all API route should be added.");
            }
        }

        private static void TestHostBuilderCatchAll()
        {
            HostBuilder builder = new HostBuilder("127.0.0.1", 1, false, Handler)
                .MapParameterRoute(HttpMethod.GET, "/files/{*path}", Handler)
                .MapParameterRoute(HttpMethod.GET, "/secure/{*path}", Handler, null, true);

            builder.Server.Routes.PreAuthentication.Parameter.Match(HttpMethod.GET, "/files/a/b", out NameValueCollection pre, out ParameterRoute preRoute);
            builder.Server.Routes.PostAuthentication.Parameter.Match(HttpMethod.GET, "/secure/x", out NameValueCollection post, out ParameterRoute postRoute);

            AssertTrue(preRoute != null, "Pre-authentication catch-all should match.");
            AssertEquals("a/b", pre["path"], "Unexpected pre-authentication remainder.");
            AssertTrue(postRoute != null, "Post-authentication catch-all should match.");
            AssertEquals("x", post["path"], "Unexpected post-authentication remainder.");
            builder.Server.Dispose();
        }

        private static void TestHostBuilderRejects()
        {
            HostBuilder builder = new HostBuilder("127.0.0.1", 1, false, Handler);
            AssertThrows<ArgumentException>(delegate { builder.MapParameterRoute(HttpMethod.GET, "/{*a}/b", Handler); }, "MapParameterRoute");
            AssertEquals(0, builder.Server.Routes.PreAuthentication.Parameter.GetAll().Count, "No route should be added.");
            builder.Server.Dispose();
        }

        private static void TestWebSocketCatchAll()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/files/{*path}", WebSocketHandler);

            Func<HttpContextBase, WebSocketSession, Task> handler = manager.Match("/files/A/B", out NameValueCollection vals, out WebSocketRoute route);
            AssertTrue(handler != null, "Expected the catch-all WebSocket route to match.");
            AssertEquals("/files/{*path}", route.Path, "Unexpected route.");
            AssertEquals("a/b/", vals["path"], "WebSocket paths are normalized (lowercased, trailing slash) before matching.");
        }

        private static void TestWebSocketCatchAllEmpty()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/files/{*path}", WebSocketHandler);

            manager.Match("/files", out NameValueCollection vals, out WebSocketRoute route);
            AssertTrue(route != null, "Expected the catch-all WebSocket route to match the prefix.");
            AssertEquals("", vals["path"], "Unexpected remainder.");
        }

        private static void TestWebSocketPrecedence()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/chat/{*rest}", WebSocketHandler);
            manager.Add("/chat/{room}", WebSocketHandler);

            manager.Match("/chat/general", out NameValueCollection vals, out WebSocketRoute route);
            AssertEquals("/chat/{room}", route.Path, "The parameter route should win over the earlier catch-all.");
            AssertEquals("general", vals["room"], "Unexpected room.");

            manager.Match("/chat/general/sub", out NameValueCollection rest, out WebSocketRoute restRoute);
            AssertEquals("/chat/{*rest}", restRoute.Path, "The catch-all should handle deeper paths.");
            AssertEquals("general/sub/", rest["rest"], "Unexpected remainder.");
        }

        private static void TestWebSocketStaticWins()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/{*path}", WebSocketHandler);
            manager.Add("/status", WebSocketHandler);

            manager.Match("/status", out NameValueCollection vals, out WebSocketRoute route);
            AssertEquals("/status/", route.Path, "The static route should win.");
            AssertEquals(0, vals.Count, "A static route captures nothing.");
        }

        private static void TestWebSocketRejects()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            ArgumentException e = AssertThrows<ArgumentException>(delegate { manager.Add("/{*path}/x", WebSocketHandler); }, "Add");
            AssertContains(e.Message, "must be the last segment", "Unexpected message.");
            AssertEquals(0, manager.GetAll().Count, "A rejected route must not be added.");
        }

        private static void TestWebSocketRoutePathChange()
        {
            WebSocketRouteManager manager = new WebSocketRouteManager();
            manager.Add("/old/{id}", WebSocketHandler);
            WebSocketRoute route = manager.GetAll()[0];
            route.Path = "/new/{*rest}";

            Func<HttpContextBase, WebSocketSession, Task> oldMatch = manager.Match("/old/1", out NameValueCollection _, out WebSocketRoute _);
            manager.Match("/new/a", out NameValueCollection vals, out WebSocketRoute matched);
            AssertTrue(oldMatch == null, "The old path must no longer match.");
            AssertTrue(matched != null, "The new path must match.");
            AssertEquals("a/", vals["rest"], "Unexpected remainder.");
            AssertThrows<ArgumentException>(delegate { route.Path = "/{*a}/b"; }, "set invalid Path");
            AssertEquals("/new/{*rest}", route.Path, "An invalid Path must not replace the old one.");
        }

        private static void TestOpenApiCatchAll()
        {
            WebserverRoutes routes = new WebserverRoutes();
            routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/files/{*path}", Handler);
            routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{v}/docs/{*rest}", Handler);

            OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();
            string json = generator.Generate(routes, new OpenApiSettings("Catch-All", "1.0.0"));

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement paths = doc.RootElement.GetProperty("paths");
                AssertTrue(!paths.TryGetProperty("/files/{*path}", out JsonElement _), "The catch-all syntax must not appear in the OpenAPI path.");
                AssertTrue(paths.TryGetProperty("/files/{path}", out JsonElement files), "Expected /files/{path}.");
                AssertPathParameters(files, "path");
                AssertTrue(paths.TryGetProperty("/{v}/docs/{rest}", out JsonElement docs), "Expected /{v}/docs/{rest}.");
                AssertPathParameters(docs, "v", "rest");
            }
        }

        private static void AssertPathParameters(JsonElement pathItem, params string[] expected)
        {
            JsonElement parameters = pathItem.GetProperty("get").GetProperty("parameters");
            List<string> names = new List<string>();
            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                AssertEquals("path", parameter.GetProperty("in").GetString(), "Parameter location.");
                names.Add(parameter.GetProperty("name").GetString());
            }

            AssertEquals(expected.Length, names.Count, "Path parameter count.");
            foreach (string name in expected)
                AssertTrue(names.Contains(name), "Expected path parameter '" + name + "' but got [" + String.Join(", ", names) + "].");
        }

        #endregion

        #region End-to-End-Tests

        private static async Task AssertHttpAsync(string path, HttpStatusCode expectedStatus, string expectedBody)
        {
            await RunHttpAsync(async delegate (HttpClient client)
            {
                await AssertResponseAsync(client, HttpMethod.GET, path, null, expectedStatus, expectedBody).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpCatchAllPerMethodAsync()
        {
            await RunHttpAsync(async delegate (HttpClient client)
            {
                await AssertResponseAsync(client, HttpMethod.POST, "/files/a/b", null, HttpStatusCode.OK, "post:[a/b]").ConfigureAwait(false);
                // no PUT route matches; with API authentication enabled the request falls through to the auth phase (HTTP 401)
                await AssertResponseAsync(client, HttpMethod.PUT, "/files/a/b", null, HttpStatusCode.Unauthorized, null).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpAuthenticatedCatchAllAsync()
        {
            await RunHttpAsync(async delegate (HttpClient client)
            {
                await AssertResponseAsync(client, HttpMethod.GET, "/secure/a/b", null, HttpStatusCode.Unauthorized, null).ConfigureAwait(false);
                await AssertResponseAsync(client, HttpMethod.GET, "/secure/a/b", "Bearer valid-token", HttpStatusCode.OK, "secure:a/b").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static async Task TestHttpRootCatchAllAsync()
        {
            LoopbackServerHost host = new LoopbackServerHost(false, false, false, delegate (Webserver server)
            {
                server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{*path}", async ctx =>
                {
                    await ctx.Response.Send("root:[" + ctx.Request.Url.Parameters["path"] + "]").ConfigureAwait(false);
                });
                server.Routes.PreAuthentication.Static.Add(HttpMethod.GET, "/hello", async ctx =>
                {
                    await ctx.Response.Send("static").ConfigureAwait(false);
                });
                server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/users/{id}", async ctx =>
                {
                    await ctx.Response.Send("user:" + ctx.Request.Url.Parameters["id"]).ConfigureAwait(false);
                });
            });

            try
            {
                await host.StartAsync().ConfigureAwait(false);
                using (HttpClient client = CreateClient(host))
                {
                    await AssertResponseAsync(client, HttpMethod.GET, "/hello", null, HttpStatusCode.OK, "static").ConfigureAwait(false);
                    await AssertResponseAsync(client, HttpMethod.GET, "/users/9", null, HttpStatusCode.OK, "user:9").ConfigureAwait(false);
                    await AssertResponseAsync(client, HttpMethod.GET, "/x/y/z", null, HttpStatusCode.OK, "root:[x/y/z]").ConfigureAwait(false);
                    await AssertResponseAsync(client, HttpMethod.GET, "/", null, HttpStatusCode.OK, "root:[]").ConfigureAwait(false);
                }
            }
            finally
            {
                host.Dispose();
            }
        }

        private static async Task TestWebSocketEndToEndCatchAllAsync()
        {
            TaskCompletionSource<string> captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (LoopbackServerHost host = new LoopbackServerHost(false, false, false, server =>
            {
                server.Settings.WebSockets.Enable = true;
                server.WebSocket("/ws/{*path}", (ctx, session) =>
                {
                    captured.TrySetResult(ctx.Request.Url.Parameters["path"]);
                    return Task.CompletedTask;
                });
            }))
            {
                await host.StartAsync().ConfigureAwait(false);

                using (ClientWebSocket client = new ClientWebSocket())
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    await client.ConnectAsync(new Uri("ws://127.0.0.1:" + host.Port.ToString() + "/ws/rooms/general"), timeout.Token).ConfigureAwait(false);
                    Task completed = await Task.WhenAny(captured.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
                    AssertTrue(completed == captured.Task, "The catch-all WebSocket handler was not invoked.");
                    AssertEquals("rooms/general/", captured.Task.Result, "Unexpected WebSocket catch-all value.");
                }
            }
        }

        private static async Task RunHttpAsync(Func<HttpClient, Task> body)
        {
            LoopbackServerHost host = new LoopbackServerHost(false, false, false, ConfigureRoutes);

            try
            {
                await host.StartAsync().ConfigureAwait(false);
                using (HttpClient client = CreateClient(host))
                {
                    await body(client).ConfigureAwait(false);
                }
            }
            finally
            {
                host.Dispose();
            }
        }

        private static HttpClient CreateClient(LoopbackServerHost host)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = host.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }

        private static void ConfigureRoutes(Webserver server)
        {
            server.Routes.Default = async ctx =>
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("notfound").ConfigureAwait(false);
            };

            server.Routes.AuthenticateApiRequest = delegate (HttpContextBase ctx)
            {
                string auth = ctx.Request.RetrieveHeaderValue("Authorization");
                if (String.Equals(auth, "Bearer valid-token", StringComparison.Ordinal))
                {
                    return Task.FromResult(new AuthResult
                    {
                        AuthenticationResult = AuthenticationResultEnum.Success,
                        AuthorizationResult = AuthorizationResultEnum.Permitted
                    });
                }

                return Task.FromResult(new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.NotFound,
                    AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
                });
            };

            // catch-all registered before the specific route on purpose
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/files/{*path}", async ctx =>
            {
                await ctx.Response.Send("files:[" + ctx.Request.Url.Parameters["path"] + "]").ConfigureAwait(false);
            });
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/files/special/{id}", async ctx =>
            {
                await ctx.Response.Send("special:" + ctx.Request.Url.Parameters["id"]).ConfigureAwait(false);
            });
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.POST, "/files/{*path}", async ctx =>
            {
                await ctx.Response.Send("post:[" + ctx.Request.Url.Parameters["path"] + "]").ConfigureAwait(false);
            });
            server.Routes.PreAuthentication.Parameter.Add(HttpMethod.GET, "/{v}/docs/{*path}", async ctx =>
            {
                await ctx.Response.Send("docs:" + ctx.Request.Url.Parameters["v"] + "|" + ctx.Request.Url.Parameters["path"]).ConfigureAwait(false);
            });

            server.Get("/api/{*rest}", req => Task.FromResult<object>("api:" + req.Parameters.GetValueOrDefault("rest", "")));
            server.Get("/api/users/{id}", req => Task.FromResult<object>("user:" + req.Parameters.GetValueOrDefault("id", "")));
            server.Get("/secure/{*rest}", req => Task.FromResult<object>("secure:" + req.Parameters.GetValueOrDefault("rest", "")), auth: true);
        }

        private static async Task AssertResponseAsync(HttpClient client, HttpMethod method, string path, string authorization, HttpStatusCode expectedStatus, string expectedBody)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(new System.Net.Http.HttpMethod(method.ToString()), path))
            {
                if (authorization != null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
                if (method == HttpMethod.POST || method == HttpMethod.PUT) request.Content = new StringContent("", Encoding.UTF8, "text/plain");

                using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (response.StatusCode != expectedStatus)
                        throw new InvalidOperationException(method + " " + path + ": expected HTTP " + (int)expectedStatus + " but got HTTP " + (int)response.StatusCode + ". Body: " + body);
                    if (expectedBody != null && !String.Equals(expectedBody, body, StringComparison.Ordinal))
                        throw new InvalidOperationException(method + " " + path + ": expected body '" + expectedBody + "' but got '" + body + "'.");
                }
            }
        }

        #endregion

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected: [" + expected + "] Actual: [" + actual + "]");
        }

        private static void AssertContains(string haystack, string needle, string message)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(message + " Expected [" + (haystack ?? "<null>") + "] to contain [" + needle + "].");
        }

        private static T AssertThrows<T>(Action action, string message) where T : Exception
        {
            try
            {
                action();
            }
            catch (Exception e) when (e.GetType() == typeof(T))
            {
                return (T)e;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(message + ": expected " + typeof(T).Name + " but got " + e.GetType().Name + ": " + e.Message);
            }

            throw new InvalidOperationException(message + ": expected " + typeof(T).Name + " but nothing was thrown.");
        }

        #endregion
    }
}
