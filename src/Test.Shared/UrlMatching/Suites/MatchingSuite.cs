#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using Touchstone.Core;
    using UrlMatcher;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Core literal and parameter matching.
    /// </summary>
    public static class MatchingSuite
    {
        private const string Id = "UrlMatcher.Matching";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Matching",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Case(Id, "BasicStaticMatchSuccess", "Static match succeeds and captures values", () =>
                    {
                        bool result = Matcher.Match("/v1.0/users/42", "/{version}/users/{id}", out NameValueCollection vals);
                        Check.True(result, "static match of /v1.0/users/42");
                        Check.Equal("v1.0", vals["version"], "version");
                        Check.Equal("42", vals["id"], "id");
                        Check.Equal(2, vals.Count, "captured key count");
                    }),
                    Case(Id, "BasicStaticMatchFailure", "Static match fails when a literal differs", () =>
                    {
                        bool result = Matcher.Match("/v1.0/posts/42", "/{version}/users/{id}", out NameValueCollection vals);
                        Check.False(result, "posts does not equal users");
                        Check.Equal(0, vals.Count, "vals empty on failure");
                    }),
                    Case(Id, "BasicInstanceMatchSuccess", "Instance match succeeds and captures values", () =>
                    {
                        Matcher matcher = new Matcher("/v1.0/users/42");
                        bool result = matcher.Match("/{version}/users/{id}", out NameValueCollection vals);
                        Check.True(result, "instance match of /v1.0/users/42");
                        Check.Equal("v1.0", vals["version"], "version");
                        Check.Equal("42", vals["id"], "id");
                    }),
                    Case(Id, "BasicInstanceMatchFailure", "Instance match fails when a literal differs", () =>
                    {
                        Matcher matcher = new Matcher("/v1.0/posts/42");
                        bool result = matcher.Match("/{version}/users/{id}", out NameValueCollection vals);
                        Check.False(result, "posts does not equal users");
                        Check.Equal(0, vals.Count, "vals empty on failure");
                    }),
                    Case(Id, "MultiplePatternsInstanceMethod", "One instance matched against several patterns", () =>
                    {
                        Matcher matcher = new Matcher("/v1.0/users/42");
                        Check.True(matcher.Match("/{version}/users/{id}", out NameValueCollection vals1), "users pattern matches");
                        Check.Equal("42", vals1["id"], "id from first pattern");
                        Check.False(matcher.Match("/{version}/posts/{id}", out NameValueCollection vals2), "posts pattern does not match");
                        Check.Equal(0, vals2.Count, "vals empty for second pattern");
                        Check.Equal("42", vals1["id"], "first collection unaffected by second match");
                    }),
                    Match(Id, "NoParameters", "All-literal pattern matches with no captures", "/users/list/all", "/users/list/all"),
                    NoMatch(Id, "LiteralMismatchMiddlePart", "Literal mismatch in a middle segment", "/api/v1/users/42", "/api/v2/users/{id}"),
                    NoMatch(Id, "NumericVsLiteralNoMatch", "Numeric literals compare exactly", "/orders/2024", "/orders/2023"),
                    NoMatch(Id, "MixedLiteralAndParamNegative", "Failure after a captured parameter leaves vals empty", "/v1.0/users/42", "/{version}/admins/{id}"),
                    NoMatch(Id, "CaseSensitivityLiteralParts", "Literal segments are case-sensitive (URL uppercase)", "/Users/42", "/users/{id}"),
                    Match(Id, "ComplexRealWorldExample", "Complex real-world API URL",
                        "/api/v3/organizations/acme-corp/repositories/my-repo/pull-requests/42",
                        "/api/{version}/organizations/{org}/repositories/{repo}/pull-requests/{prId}",
                        "version", "v3", "org", "acme-corp", "repo", "my-repo", "prId", "42"),

                    // additional positive cases
                    Match(Id, "SingleLiteral", "Single literal segment matches itself", "/health", "/health"),
                    Match(Id, "LiteralAndParameterAlternate", "Alternating literal and parameter segments", "/a/1/b/2/c/3", "/a/{x}/b/{y}/c/{z}", "x", "1", "y", "2", "z", "3"),
                    Match(Id, "ParameterMatchesLiteralLookingValue", "A parameter captures a value that looks like a literal", "/users/users", "/users/{id}", "id", "users"),
                    Match(Id, "ParameterMatchesBraceValue", "A parameter captures a URL value that contains braces", "/{y}", "/{x}", "x", "{y}"),
                    Match(Id, "ParameterMatchesStarValue", "A parameter captures a literal asterisk from the URL", "/*", "/{x}", "x", "*"),
                    Case(Id, "MatchIsRepeatable", "Repeated matches on one instance give identical results", () =>
                    {
                        Matcher matcher = new Matcher("/v1/users/42");
                        for (int i = 0; i < 100; i++)
                        {
                            Check.True(matcher.Match("/{v}/users/{id}", out NameValueCollection vals), "iteration " + i);
                            Check.Equal("42", vals["id"], "id on iteration " + i);
                            Check.False(matcher.Match("/{v}/posts/{id}", out NameValueCollection none), "negative on iteration " + i);
                            Check.Equal(0, none.Count, "vals empty on iteration " + i);
                        }
                    }),
                    Case(Id, "EachMatchReturnsNewCollection", "Each call returns a distinct collection", () =>
                    {
                        Matcher matcher = new Matcher("/a/1");
                        matcher.Match("/a/{id}", out NameValueCollection first);
                        matcher.Match("/a/{id}", out NameValueCollection second);
                        Check.False(Object.ReferenceEquals(first, second), "collections are distinct instances");
                        first.Add("extra", "x");
                        Check.Null(second["extra"], "mutating one collection does not affect another");
                    }),

                    // additional negative cases
                    NoMatch(Id, "LiteralMismatchFirstSegment", "Literal mismatch in the first segment", "/x/users/42", "/api/users/{id}"),
                    NoMatch(Id, "LiteralMismatchLastSegment", "Literal mismatch in the last segment", "/api/users/list", "/api/users/all"),
                    NoMatch(Id, "LiteralPrefixOfSegment", "URL segment that is a prefix of the literal does not match", "/user", "/users"),
                    NoMatch(Id, "LiteralSuperstringOfSegment", "URL segment that extends the literal does not match", "/users2", "/users"),
                    NoMatch(Id, "CaseSensitivityPatternUppercase", "Literal segments are case-sensitive (pattern uppercase)", "/users/42", "/Users/{id}"),
                    NoMatch(Id, "OrdinalNotCultureComparison", "Literals compare ordinally, not by culture", "/strasse", "/straße"),
                    NoMatch(Id, "OrdinalDottedI", "Turkish dotted I does not equal i", "/İ", "/i"),
                    NoMatch(Id, "LeadingWhitespaceSignificant", "Whitespace in a literal is significant", "/ users", "/users"),
                    NoMatch(Id, "RootPatternVsNonRootUrl", "Root pattern does not match a non-root URL", "/a", "/"),
                    NoMatch(Id, "NonRootPatternVsRootUrl", "Non-root pattern does not match the root URL", "/", "/a"),
                    NoMatch(Id, "ParameterPatternVsRootUrl", "Parameter pattern does not match the root URL", "/", "/{id}"),
                    NoMatch(Id, "PartialCaptureNotLeaked", "Two captured parameters then a failing literal leaves vals empty", "/1/2/y", "/{a}/{b}/x")
                });
        }
    }
}
