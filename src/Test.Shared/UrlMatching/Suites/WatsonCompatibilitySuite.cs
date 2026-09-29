#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// The input shapes Watson Webserver passes to Matcher.Match.  Parameter routes prefix the HTTP method
    /// ("GET /users/{id}"), and WebSocket routes pass lowercased paths with a trailing slash.
    /// </summary>
    public static class WatsonCompatibilitySuite
    {
        private const string Id = "UrlMatcher.WatsonCompatibility";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Watson Compatibility",
                cases: new List<TestCaseDescriptor>
                {
                    Match(Id, "MethodPrefixedParameter", "GET /users/{id} against GET /users/42", "GET /users/42", "GET /users/{id}", "id", "42"),
                    Match(Id, "MethodPrefixedLiteral", "GET /health against GET /health", "GET /health", "GET /health"),
                    Match(Id, "MethodPrefixedRoot", "GET / against GET /", "GET /", "GET /"),
                    NoMatch(Id, "MethodMismatch", "POST does not match GET", "POST /users/42", "GET /users/{id}"),
                    NoMatch(Id, "MethodCaseSensitive", "get does not match GET", "get /users/42", "GET /users/{id}"),
                    NoMatch(Id, "MethodPrefixedExtraSegment", "An extra segment does not match", "GET /users/42/x", "GET /users/{id}"),
                    Match(Id, "MethodPrefixedQueryStripped", "The query is removed from a method-prefixed path", "GET /users/42?x=1", "GET /users/{id}", "id", "42"),
                    Match(Id, "MethodPrefixedMultipleParameters", "Several parameters after a method prefix", "PUT /v1/users/42", "PUT /{v}/users/{id}", "v", "v1", "id", "42"),
                    Match(Id, "MethodPrefixedCatchAll", "GET /api/{*rest} against GET /api/a/b", "GET /api/a/b", "GET /api/{*rest}", "rest", "a/b"),
                    Match(Id, "MethodPrefixedCatchAllEmpty", "GET /api/{*rest} against GET /api", "GET /api", "GET /api/{*rest}", "rest", ""),
                    Match(Id, "MethodPrefixedRootCatchAll", "GET /{*path} against GET /", "GET /", "GET /{*path}", "path", ""),
                    Match(Id, "MethodPrefixedCatchAllRaw", "The remainder is raw after a method prefix", "GET /api/a//b/", "GET /api/{*rest}", "rest", "a//b/"),
                    NoMatch(Id, "MethodPrefixedCatchAllMethodMismatch", "A catch-all does not match another method", "POST /api/a", "GET /api/{*rest}"),
                    Match(Id, "WebSocketNormalizedParameter", "Lowercased path with a trailing slash", "/chat/general/", "/chat/{room}", "room", "general"),
                    Match(Id, "WebSocketNormalizedParameterTrailingPattern", "Trailing slash on both", "/chat/general/", "/chat/{room}/", "room", "general"),
                    NoMatch(Id, "WebSocketNormalizedCaseMismatch", "A lowercased path does not match an uppercase literal", "/chat/general/", "/Chat/{room}"),
                    Match(Id, "WebSocketNormalizedCatchAll", "Catch-all keeps the trailing slash Watson adds", "/files/a/b/", "/files/{*path}", "path", "a/b/")
                });
        }
    }
}
