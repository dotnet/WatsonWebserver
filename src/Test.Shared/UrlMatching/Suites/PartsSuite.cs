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
    /// The Parts and Url properties.
    /// </summary>
    public static class PartsSuite
    {
        private const string Id = "UrlMatcher.Parts";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Parts",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Case(Id, "PartsPropertyReturnsCopy", "Parts returns a new array each time", () =>
                    {
                        Matcher matcher = new Matcher("/foo/bar/baz");
                        Check.False(Object.ReferenceEquals(matcher.Parts, matcher.Parts), "distinct arrays");
                    }),
                    Case(Id, "PartsPropertyArrayImmutability", "Changing the returned array does not change the matcher", () =>
                    {
                        Matcher matcher = new Matcher("/foo/bar/baz");
                        string[] parts = matcher.Parts;
                        parts[0] = "changed";
                        Check.Equal("foo", matcher.Parts[0], "internal part 0");
                        Check.True(matcher.Match("/foo/{b}/{c}", out NameValueCollection _), "matching uses the original parts");
                    }),

                    // additional cases
                    Case(Id, "PartsValues", "Parts contains each segment in order", () =>
                    {
                        string[] parts = new Matcher("/foo/bar/baz").Parts;
                        Check.Equal(3, parts.Length, "count");
                        Check.Equal("foo", parts[0], "0");
                        Check.Equal("bar", parts[1], "1");
                        Check.Equal("baz", parts[2], "2");
                    }),
                    Case(Id, "PartsRootIsEmpty", "Root URL has zero parts", () =>
                    {
                        Check.Equal(0, new Matcher("/").Parts.Length, "count");
                    }),
                    Case(Id, "PartsCollapseSlashes", "Parts discards empty segments", () =>
                    {
                        string[] parts = new Matcher("//a///b//").Parts;
                        Check.Equal(2, parts.Length, "count");
                        Check.Equal("a", parts[0], "0");
                        Check.Equal("b", parts[1], "1");
                    }),
                    Case(Id, "PartsExcludeQueryAndFragment", "Parts excludes the query and fragment", () =>
                    {
                        string[] parts = new Matcher("/a/b?c=/d#/e").Parts;
                        Check.Equal(2, parts.Length, "count");
                        Check.Equal("b", parts[1], "1");
                    }),
                    Case(Id, "PartsUnaffectedByCatchAllMatch", "A catch-all match does not change Parts or Url", () =>
                    {
                        Matcher matcher = new Matcher("/api/a//b/");
                        Check.True(matcher.Match("/api/{*rest}", out NameValueCollection vals), "match");
                        Check.Equal("a//b/", vals["rest"], "rest");
                        string[] parts = matcher.Parts;
                        Check.Equal(3, parts.Length, "count");
                        Check.Equal("api", parts[0], "0");
                        Check.Equal("a", parts[1], "1");
                        Check.Equal("b", parts[2], "2");
                        Check.Equal("/api/a//b/", matcher.Url, "Url");
                    }),
                    Case(Id, "UrlPropertyString", "Url returns the URL supplied to the constructor", () =>
                    {
                        Check.Equal("/v1/users", new Matcher("/v1/users").Url, "Url");
                    }),
                    Case(Id, "UrlPropertyUri", "Url returns the path of a Uri", () =>
                    {
                        Check.Equal("/v1/users", new Matcher(new Uri("http://127.0.0.1:8000/v1/users?x=1")).Url, "Url");
                    })
                });
        }
    }
}
