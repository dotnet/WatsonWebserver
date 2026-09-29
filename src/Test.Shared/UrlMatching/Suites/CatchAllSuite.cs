#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using Touchstone.Core;
    using UrlMatcher;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Catch-all segments written {*name}.
    /// </summary>
    public static class CatchAllSuite
    {
        private const string Id = "UrlMatcher.CatchAll";
        private const string NotLast = "must be the last segment";
        private const string OnlyOne = "only one catch-all is allowed";
        private const string EntireSegment = "must be the entire segment";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            string deepRemainder = String.Join("/", Enumerable.Range(0, 500).Select(i => "d" + i));

            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Catch-All",
                cases: new List<TestCaseDescriptor>
                {
                    // semantics table
                    Match(Id, "PrefixManySegments", "/api/{*rest} captures several segments", "/api/users/42/orders", "/api/{*rest}", "rest", "users/42/orders"),
                    Match(Id, "PrefixOneSegment", "/api/{*rest} captures one segment", "/api/users", "/api/{*rest}", "rest", "users"),
                    Match(Id, "PrefixZeroSegments", "/api/{*rest} matches /api with an empty value", "/api", "/api/{*rest}", "rest", ""),
                    NoMatch(Id, "WrongPrefix", "/api/{*rest} does not match another prefix", "/other/users", "/api/{*rest}"),
                    NoMatch(Id, "PrefixCaseSensitive", "Literals before a catch-all stay case-sensitive", "/API/users", "/api/{*rest}"),
                    Match(Id, "ParameterThenCatchAll", "/{v}/files/{*path} captures both", "/v1/files/a/b.txt", "/{v}/files/{*path}", "v", "v1", "path", "a/b.txt"),
                    Match(Id, "RootCatchAllMatchesRoot", "/{*path} matches / with an empty value", "/", "/{*path}", "path", ""),
                    Throws(Id, "NotLastThrows", "/{*rest}/edit throws", "/a/edit", "/{*rest}/edit", NotLast, "{*rest}"),

                    // zero-or-more boundaries
                    Match(Id, "PrefixTrailingSlashZeroSegments", "/api/ matches with an empty value", "/api/", "/api/{*rest}", "rest", ""),
                    Match(Id, "PrefixRepeatedTrailingSlashesZeroSegments", "/api// matches with an empty value", "/api//", "/api/{*rest}", "rest", ""),
                    Match(Id, "PrefixQueryOnlyZeroSegments", "/api?x=1 matches with an empty value", "/api?x=1", "/api/{*rest}", "rest", ""),
                    Match(Id, "PrefixFragmentOnlyZeroSegments", "/api#top matches with an empty value", "/api#top", "/api/{*rest}", "rest", ""),
                    Match(Id, "RootCatchAllOneSegment", "/{*path} captures one segment", "/a", "/{*path}", "path", "a"),
                    Match(Id, "RootCatchAllManySegments", "/{*path} captures several segments", "/a/b/c", "/{*path}", "path", "a/b/c"),
                    Match(Id, "RootCatchAllQueryOnly", "/{*path} matches a query-only URL", "?q=1", "/{*path}", "path", ""),
                    Match(Id, "RootCatchAllOnlySlashes", "/{*path} matches a URL of only slashes", "///", "/{*path}", "path", ""),
                    Match(Id, "CatchAllWithoutLeadingSlash", "{*path} without a leading slash", "a/b", "{*path}", "path", "a/b"),
                    Match(Id, "CatchAllPatternTrailingSlash", "A trailing slash after the catch-all is ignored", "/api/a/b", "/api/{*rest}/", "rest", "a/b"),
                    Match(Id, "DeepRemainder", "Five hundred remaining segments", "/api/" + deepRemainder, "/api/{*rest}", "rest", deepRemainder),
                    NoMatch(Id, "FixedSegmentsMissing", "Too few segments for the fixed part", "/v1", "/{v}/files/{*path}"),
                    NoMatch(Id, "FixedSegmentsMissingRoot", "Root URL against a prefix catch-all", "/", "/api/{*rest}"),
                    NoMatch(Id, "PrefixPartialSegment", "/apix does not match /api", "/apix/users", "/api/{*rest}"),
                    NoMatch(Id, "PrefixSecondSegmentMismatch", "Second literal mismatch", "/api/v2/a", "/api/v1/{*rest}"),
                    NoMatch(Id, "LiteralMismatchAfterParameterLeavesEmpty", "A failed literal after a parameter leaves vals empty", "/v1/docs/a", "/{v}/files/{*path}"),

                    // raw remainder
                    Match(Id, "RawRemainderDoubleSlash", "Repeated slashes in the remainder are kept", "/api/a//b", "/api/{*rest}", "rest", "a//b"),
                    Match(Id, "RawRemainderTrailingSlash", "A trailing slash in the remainder is kept", "/api/a/b/", "/api/{*rest}", "rest", "a/b/"),
                    Match(Id, "RawRemainderRepeatedTrailingSlashes", "Repeated trailing slashes in the remainder are kept", "/api/a//", "/api/{*rest}", "rest", "a//"),
                    Match(Id, "RawRemainderEncodedSlash", "An encoded slash in the remainder is not decoded", "/api/a%2Fb/c", "/api/{*rest}", "rest", "a%2Fb/c"),
                    Match(Id, "RawRemainderEncodedSpace", "An encoded space in the remainder is not decoded", "/api/a%20b", "/api/{*rest}", "rest", "a%20b"),
                    Match(Id, "RawRemainderStartsAtFirstSegment", "Slashes before the first remaining segment are not captured", "/api//a", "/api/{*rest}", "rest", "a"),
                    Match(Id, "RawRemainderPrefixSlashesCollapse", "Repeated slashes in the fixed part still collapse", "//api//a/b", "/api/{*rest}", "rest", "a/b"),
                    Match(Id, "RawRemainderDotSegments", "Dot segments in the remainder are kept literally", "/api/../x/./y", "/api/{*rest}", "rest", "../x/./y"),
                    Match(Id, "RawRemainderSpecialCharacters", "Special characters in the remainder", "/api/a;b=c/d@e:f", "/api/{*rest}", "rest", "a;b=c/d@e:f"),
                    Match(Id, "RawRemainderUnicode", "Unicode in the remainder", "/api/用户/😀", "/api/{*rest}", "rest", "用户/😀"),
                    Match(Id, "RawRemainderCasePreserved", "The remainder keeps its case", "/api/AbC/DeF", "/api/{*rest}", "rest", "AbC/DeF"),
                    Match(Id, "RawRemainderBraces", "Braces in the URL remainder are ordinary text", "/api/{x}/{*y}", "/api/{*rest}", "rest", "{x}/{*y}"),

                    // query and fragment
                    Match(Id, "QueryAndFragmentStrippedBeforeCapture", "The query and fragment are removed before capturing", "/api/a/b?x=1#f", "/api/{*rest}", "rest", "a/b"),
                    Match(Id, "QueryStrippedBeforeCapture", "The query is removed before capturing", "/api/a/b?next=/c/d", "/api/{*rest}", "rest", "a/b"),
                    Match(Id, "FragmentStrippedBeforeCapture", "The fragment is removed before capturing", "/api/a/b#/c", "/api/{*rest}", "rest", "a/b"),
                    Match(Id, "TrailingSlashBeforeQueryKept", "A trailing slash before the query is kept", "/api/a/?x=1", "/api/{*rest}", "rest", "a/"),

                    // names
                    Case(Id, "NameCaseInsensitiveLookup", "The catch-all name is looked up case-insensitively", () =>
                    {
                        Check.True(Matcher.Match("/api/a/b", "/api/{*rest}", out NameValueCollection vals), "match");
                        Check.Equal("a/b", vals["rest"], "rest");
                        Check.Equal("a/b", vals["REST"], "REST");
                        Check.Equal("a/b", vals["Rest"], "Rest");
                    }),
                    Case(Id, "NameKeyExcludesAsterisk", "The collection key is the name without the asterisk", () =>
                    {
                        Check.True(Matcher.Match("/api/a", "/api/{*Rest}", out NameValueCollection vals), "match");
                        Check.Equal(1, vals.AllKeys.Length, "key count");
                        Check.Equal("Rest", vals.AllKeys[0], "key text");
                        Check.Null(vals["*rest"], "no key with the asterisk");
                    }),
                    Match(Id, "NameWithSpaceKept", "{* rest} is named ' rest'", "/a/b", "/{* rest}", " rest", "a/b"),
                    Match(Id, "NameWithSecondAsterisk", "{**rest} is named *rest", "/a/b", "/{**rest}", "*rest", "a/b"),
                    Match(Id, "NameDoubleAsteriskOnly", "{**} is a catch-all named *", "/a/b", "/{**}", "*", "a/b"),
                    Match(Id, "NameUnicode", "Unicode catch-all name", "/a/b", "/{*路径}", "路径", "a/b"),
                    Match(Id, "SameNameAsParameterJoins", "A parameter and catch-all with one name join values", "/a/b/c", "/{rest}/{*rest}", "rest", "a,b/c"),
                    Match(Id, "SameNameAsParameterEmptyRemainder", "A parameter and catch-all with one name and no remainder", "/a", "/{rest}/{*rest}", "rest", "a,"),

                    // compatibility with 3.0.2
                    Case(Id, "SingleSegmentNowNamedWithoutAsterisk", "In 3.0.2 {*rest} captured one segment as '*rest'; now it is 'rest'", () =>
                    {
                        Check.True(Matcher.Match("/a", "/{*rest}", out NameValueCollection vals), "match");
                        Check.Equal("a", vals["rest"], "rest");
                        Check.Null(vals["*rest"], "*rest");
                    }),
                    Match(Id, "MultipleSegmentsNowMatch", "In 3.0.2 {*rest} failed on several segments; now it matches", "/a/b/c", "/{*rest}", "rest", "a/b/c"),

                    // invalid catch-all placement
                    Throws(Id, "NotLastThrowsForAnyUrl", "A misplaced catch-all throws even for a URL that could never match", "/", "/{*rest}/edit", NotLast),
                    Throws(Id, "NotLastInMiddleThrows", "A catch-all in the middle throws", "/a/b/c", "/a/{*rest}/c", NotLast, "{*rest}"),
                    Throws(Id, "NotLastBeforeParameterThrows", "A catch-all before a parameter throws", "/a/b", "/{*rest}/{id}", NotLast),
                    Throws(Id, "TwoCatchAllsThrow", "Two catch-alls throw", "/x/y", "/{*a}/{*b}", "2 catch-all segments", OnlyOne),
                    Throws(Id, "ThreeCatchAllsThrow", "Three catch-alls throw", "/x/y/z", "/{*a}/{*b}/{*c}", "3 catch-all segments", OnlyOne),
                    Throws(Id, "TwoCatchAllsSameNameThrow", "Two catch-alls with the same name throw", "/x/y", "/{*a}/{*a}", OnlyOne),
                    Throws(Id, "CatchAllWithPrefixTextThrows", "v{*x} throws", "/files/v1", "/files/v{*x}", EntireSegment, "v{*x}"),
                    Throws(Id, "CatchAllWithSuffixTextThrows", "{*x}.txt throws", "/files/a.txt", "/files/{*x}.txt", EntireSegment),
                    Throws(Id, "CatchAllWithSecondGroupThrows", "{*a}{b} throws", "/files/ab", "/files/{*a}{b}", EntireSegment),
                    Throws(Id, "CatchAllInsideSegmentNotLastThrows", "v{*x}/y throws", "/v1/y", "/v{*x}/y", EntireSegment),
                    Throws(Id, "CatchAllWithTrailingBraceTextThrows", "{*x}} throws", "/a", "/{*x}}", EntireSegment),
                    Case(Id, "ThrowIsExactlyArgumentException", "The exception is ArgumentException, not ArgumentNullException", () =>
                    {
                        ArgumentException e = Check.Throws<ArgumentException>(() => UrlPattern.Parse("/{*a}/b"), "Parse");
                        Check.False(e is ArgumentNullException, "not ArgumentNullException");
                    }),

                    // reuse and overloads
                    Case(Id, "ReusedInstanceWithCatchAll", "One instance against literal, parameter, and catch-all patterns", () =>
                    {
                        Matcher matcher = new Matcher("/api/users/42");
                        Check.True(matcher.Match("/api/users/{id}", out NameValueCollection a), "parameter pattern");
                        Check.Equal("42", a["id"], "id");
                        Check.True(matcher.Match("/api/{*rest}", out NameValueCollection b), "catch-all pattern");
                        Check.Equal("users/42", b["rest"], "rest");
                        Check.False(matcher.Match("/other/{*rest}", out NameValueCollection c), "wrong prefix");
                        Check.Equal(0, c.Count, "empty on failure");
                        Check.True(matcher.Match("/{*all}", out NameValueCollection d), "root catch-all");
                        Check.Equal("users/42", b["rest"], "earlier collection unaffected");
                        Check.Equal("api/users/42", d["all"], "all");
                    }),
                    Case(Id, "ParsedPatternAgainstManyUrls", "One parsed catch-all pattern against several URLs", () =>
                    {
                        UrlPattern pattern = UrlPattern.Parse("/files/{*path}");
                        Check.True(Matcher.Match("/files", pattern, out NameValueCollection a), "zero");
                        Check.Equal("", a["path"], "zero value");
                        Check.True(Matcher.Match("/files/a", pattern, out NameValueCollection b), "one");
                        Check.Equal("a", b["path"], "one value");
                        Check.True(Matcher.Match("/files/a/b", pattern, out NameValueCollection c), "two");
                        Check.Equal("a/b", c["path"], "two value");
                        Check.False(Matcher.Match("/docs/a", pattern, out NameValueCollection d), "wrong prefix");
                        Check.Equal(0, d.Count, "empty on failure");
                    })
                });
        }
    }
}
