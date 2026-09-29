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
    /// Uri overloads and constructors.
    /// </summary>
    public static class UriSuite
    {
        private const string Id = "UrlMatcher.Uri";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Uri",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Case(Id, "UriConstructor", "Matcher(Uri) then Match(string)", () =>
                    {
                        Matcher matcher = new Matcher(new Uri("http://127.0.0.1:8000/v1.0/users/42"));
                        Check.True(matcher.Match("/{version}/users/{id}", out NameValueCollection vals), "match");
                        Check.Equal("v1.0", vals["version"], "version");
                        Check.Equal("42", vals["id"], "id");
                    }),
                    Case(Id, "UriStaticMethod", "Matcher.Match(Uri, string)", () =>
                    {
                        Check.True(Matcher.Match(new Uri("http://127.0.0.1:8000/v1.0/users/42"), "/{version}/users/{id}", out NameValueCollection vals), "match");
                        Check.Equal("v1.0", vals["version"], "version");
                        Check.Equal("42", vals["id"], "id");
                    }),
                    Case(Id, "UriWithQueryAndFragment", "Query and fragment are removed from a Uri", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1:8000/v1.0/users/42?active=true#top"), "/{version}/users/{id}", true, "version", "v1.0", "id", "42")),
                    Case(Id, "InstanceUriPartsExtraction", "Matcher(Uri) splits the path only", () =>
                    {
                        Matcher matcher = new Matcher(new Uri("http://127.0.0.1:9000/a/b/c?x=1"));
                        string[] parts = matcher.Parts;
                        Check.Equal(3, parts.Length, "part count");
                        Check.Equal("a", parts[0], "part 0");
                        Check.Equal("b", parts[1], "part 1");
                        Check.Equal("c", parts[2], "part 2");
                        Check.Equal("/a/b/c", matcher.Url, "Url");
                    }),

                    // additional cases, each through all four Uri entry points
                    Case(Id, "UriAllOverloadsMatch", "All Uri overloads agree on a match", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/v1/users/42"), "/{v}/users/{id}", true, "v", "v1", "id", "42")),
                    Case(Id, "UriAllOverloadsNoMatch", "All Uri overloads agree on no match", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/v1/posts/42"), "/{v}/users/{id}", false)),
                    Case(Id, "UriRootPath", "Uri with no path matches the root pattern", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1"), "/", true)),
                    Case(Id, "UriRootPathWithSlash", "Uri with a slash path matches the root pattern", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/"), "/", true)),
                    Case(Id, "UriRootNotParameter", "Uri root does not match a parameter", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/"), "/{id}", false)),
                    Case(Id, "UriHostAndPortIgnored", "Host, port, and scheme are not part of the match", () =>
                        MatchVerifier.VerifyUri(new Uri("https://example.com:8443/a"), "/a", true)),
                    Case(Id, "UriUserInfoIgnored", "User info is not part of the match", () =>
                        MatchVerifier.VerifyUri(new Uri("http://user:pass@127.0.0.1/a"), "/a", true)),
                    Case(Id, "UriHostIsNotASegment", "The host cannot be matched as a segment", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/a"), "/127.0.0.1/a", false)),
                    Case(Id, "UriSpacesAreEscaped", "A Uri escapes spaces, so the value is percent-encoded", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/hello world"), "/{greeting}", true, "greeting", "hello%20world")),
                    Case(Id, "UriDotSegmentsNormalized", "A Uri removes dot segments before matching", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/a/./b/../c"), "/a/c", true)),
                    Case(Id, "UriCatchAll", "Catch-all through the Uri overloads", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/api/a/b/c?x=1#f"), "/api/{*rest}", true, "rest", "a/b/c")),
                    Case(Id, "UriCatchAllEmpty", "Catch-all through the Uri overloads with no remainder", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/api"), "/api/{*rest}", true, "rest", "")),
                    Case(Id, "UriCatchAllTrailingSlashKept", "Catch-all keeps a trailing slash from a Uri", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/api/a/b/"), "/api/{*rest}", true, "rest", "a/b/")),
                    Case(Id, "UriCatchAllNoMatch", "Catch-all through the Uri overloads with the wrong prefix", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/other/a"), "/api/{*rest}", false)),
                    Case(Id, "UriEncodedSlashPreserved", "An encoded slash in a Uri stays encoded and does not split", () =>
                        MatchVerifier.VerifyUri(new Uri("http://127.0.0.1/a%2Fb"), "/{x}", true, "x", "a%2Fb"))
                });
        }
    }
}
