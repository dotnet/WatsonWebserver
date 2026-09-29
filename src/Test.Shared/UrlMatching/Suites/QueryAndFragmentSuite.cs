#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using UrlMatcher;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Removal of query strings and fragments from URLs (and not from patterns).
    /// </summary>
    public static class QueryAndFragmentSuite
    {
        private const string Id = "UrlMatcher.QueryAndFragment";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Query and Fragment",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Match(Id, "QueryStringStripping", "Query string is removed", "/users/42?foo=bar", "/users/{id}", "id", "42"),
                    Match(Id, "FragmentStripping", "Fragment is removed", "/users/42#section", "/users/{id}", "id", "42"),
                    Match(Id, "QueryAndFragmentStripping", "Query and fragment are removed", "/users/42?foo=bar#section", "/users/{id}", "id", "42"),
                    Case(Id, "QueryStringWithoutPath", "A query string alone produces zero segments", () =>
                    {
                        Matcher matcher = new Matcher("?foo=bar");
                        Check.Equal(0, matcher.Parts.Length, "part count");
                        Check.Equal("", matcher.Url, "Url");
                    }),
                    Case(Id, "FragmentWithoutPath", "A fragment alone produces zero segments", () =>
                    {
                        Matcher matcher = new Matcher("#section");
                        Check.Equal(0, matcher.Parts.Length, "part count");
                        Check.Equal("", matcher.Url, "Url");
                    }),

                    // additional cases
                    Match(Id, "QueryStringWithoutPathMatchesRoot", "A query string alone matches the root pattern", "?foo=bar", "/"),
                    Match(Id, "FragmentWithoutPathMatchesRoot", "A fragment alone matches the root pattern", "#section", "/"),
                    NoMatch(Id, "QueryStringWithoutPathVsParameter", "A query string alone does not match a parameter", "?foo=bar", "/{id}"),
                    Match(Id, "FragmentBeforeQuery", "A fragment before a question mark is cut at the fragment", "/a#frag?x=1", "/{x}", "x", "a"),
                    Match(Id, "QueryContainingSlashes", "Slashes in the query do not add segments", "/a?next=/b/c", "/{x}", "x", "a"),
                    Match(Id, "FragmentContainingSlashes", "Slashes in the fragment do not add segments", "/a#/b/c", "/{x}", "x", "a"),
                    Match(Id, "EmptyQuery", "A bare question mark is removed", "/a?", "/{x}", "x", "a"),
                    Match(Id, "EmptyFragment", "A bare hash is removed", "/a#", "/{x}", "x", "a"),
                    Match(Id, "MultipleQuestionMarks", "Only the first question mark matters", "/a?b?c", "/{x}", "x", "a"),
                    Match(Id, "QueryAfterTrailingSlash", "Query after a trailing slash", "/search/?q=1", "/search"),
                    Match(Id, "QueryAttachedToSegment", "Query directly after a segment", "/search?q=1", "/search"),
                    NoMatch(Id, "QueryDoesNotAddSegment", "The query does not count as a segment", "/search/results?q=1", "/search"),
                    Match(Id, "QueryInPatternIsNotStripped", "A query in the pattern is part of the segment (quirk)", "/search?q=1", "/search?q={q}", "q", "search"),
                    NoMatch(Id, "LiteralQueryInPatternNoMatch", "A literal query in the pattern never matches a stripped URL", "/search?q=1", "/search?q=1"),
                    Case(Id, "UrlPropertyExcludesQueryAndFragment", "Url property has the query and fragment removed", () =>
                    {
                        Matcher matcher = new Matcher("/a/b?x=1#frag");
                        Check.Equal("/a/b", matcher.Url, "Url");
                    }),
                    Case(Id, "UrlPropertyUnchangedWithoutQuery", "Url property is unchanged when there is no query", () =>
                    {
                        Matcher matcher = new Matcher("//a//b/");
                        Check.Equal("//a//b/", matcher.Url, "Url");
                    })
                });
        }
    }
}
