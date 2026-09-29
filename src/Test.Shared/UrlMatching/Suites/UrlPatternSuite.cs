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
    /// UrlPattern parsing, properties, and segment metadata.
    /// Equivalence between the string and UrlPattern overloads is verified by every Match and NoMatch case in the other suites.
    /// </summary>
    public static class UrlPatternSuite
    {
        private const string Id = "UrlMatcher.UrlPattern";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: UrlPattern",
                cases: new List<TestCaseDescriptor>
                {
                    Shape(Id, "LiteralOnly", "/users/list/all", 3, 3, 0, 3, false, null),
                    Shape(Id, "ParametersOnly", "/{a}/{b}", 2, 0, 2, 0, false, null),
                    Shape(Id, "Mixed", "/api/v1/{id}/orders", 4, 3, 1, 2, false, null),
                    Shape(Id, "ParameterFirst", "/{v}/users", 2, 1, 1, 0, false, null),
                    Shape(Id, "Root", "/", 0, 0, 0, 0, false, null),
                    Shape(Id, "OnlySlashes", "///", 0, 0, 0, 0, false, null),
                    Shape(Id, "CatchAllWithPrefix", "/api/{*rest}", 2, 1, 0, 1, true, "rest"),
                    Shape(Id, "CatchAllRoot", "/{*path}", 1, 0, 0, 0, true, "path"),
                    Shape(Id, "ParameterAndCatchAll", "/{v}/files/{*path}", 3, 1, 1, 0, true, "path"),
                    Shape(Id, "LiteralPrefixThenCatchAll", "/a/b/c/{*rest}", 4, 3, 0, 3, true, "rest"),
                    Shape(Id, "EmptyBracesCountAsLiteral", "/{}/x", 2, 2, 0, 2, false, null),
                    Shape(Id, "StarBracesCountAsLiteral", "/{*}/x", 2, 2, 0, 2, false, null),
                    Shape(Id, "PrefixedParameterCountsAsParameter", "/v{version}/x", 2, 1, 1, 0, false, null),

                    Case(Id, "SegmentMetadata", "Segments expose text, type, and name", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("/api/{id}/v{ver}/{}/{*}/*/{*rest}");
                        Check.Equal(7, p.Segments.Count, "segment count");
                        Segment(p.Segments[0], "api", SegmentTypeEnum.Literal, null);
                        Segment(p.Segments[1], "{id}", SegmentTypeEnum.Parameter, "id");
                        Segment(p.Segments[2], "v{ver}", SegmentTypeEnum.Parameter, "ver");
                        Segment(p.Segments[3], "{}", SegmentTypeEnum.Literal, null);
                        Segment(p.Segments[4], "{*}", SegmentTypeEnum.Literal, null);
                        Segment(p.Segments[5], "*", SegmentTypeEnum.Literal, null);
                        Segment(p.Segments[6], "{*rest}", SegmentTypeEnum.CatchAll, "rest");
                    }),
                    Case(Id, "SegmentQuirkNames", "Quirk names are reported as matched", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("/{ id }/{a}{b}/{a{b}/{* rest}");
                        Segment(p.Segments[0], "{ id }", SegmentTypeEnum.Parameter, " id ");
                        Segment(p.Segments[1], "{a}{b}", SegmentTypeEnum.Parameter, "a");
                        Segment(p.Segments[2], "{a{b}", SegmentTypeEnum.Parameter, "a{b");
                        Segment(p.Segments[3], "{* rest}", SegmentTypeEnum.CatchAll, " rest");
                    }),
                    Case(Id, "SegmentsAreReadOnly", "The Segments list cannot be modified", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("/a/{b}");
                        IList<UrlPatternSegment>? list = p.Segments as IList<UrlPatternSegment>;
                        Check.NotNull(list, "Segments implements IList");
                        Check.True(list!.IsReadOnly, "IsReadOnly");
                        Check.Throws<NotSupportedException>(() => list.Add(new UrlPatternSegment("x", SegmentTypeEnum.Literal, null)), "Add");
                        Check.Throws<NotSupportedException>(() => list[0] = new UrlPatternSegment("x", SegmentTypeEnum.Literal, null), "indexer set");
                        Check.Equal(2, p.Segments.Count, "count unchanged");
                    }),
                    Case(Id, "PatternTextPreserved", "Pattern and ToString return the original text", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("//api//{*rest}/");
                        Check.Equal("//api//{*rest}/", p.Pattern, "Pattern");
                        Check.Equal("//api//{*rest}/", p.ToString(), "ToString");
                    }),
                    Case(Id, "SegmentToString", "UrlPatternSegment.ToString returns the text", () =>
                    {
                        Check.Equal("{id}", new UrlPatternSegment("{id}", SegmentTypeEnum.Parameter, "id").ToString(), "ToString");
                    }),
                    Case(Id, "ConstructorEqualsParse", "new UrlPattern and Parse produce the same shape", () =>
                    {
                        UrlPattern a = new UrlPattern("/{v}/files/{*path}");
                        UrlPattern b = UrlPattern.Parse("/{v}/files/{*path}");
                        Check.Equal(a.SegmentCount, b.SegmentCount, "SegmentCount");
                        Check.Equal(a.CatchAllName, b.CatchAllName, "CatchAllName");
                        for (int i = 0; i < a.SegmentCount; i++)
                        {
                            Check.Equal(a.Segments[i].Text, b.Segments[i].Text, "Text " + i);
                            Check.Equal(a.Segments[i].Type, b.Segments[i].Type, "Type " + i);
                            Check.Equal(a.Segments[i].Name, b.Segments[i].Name, "Name " + i);
                        }
                    }),
                    Case(Id, "TryParseValid", "TryParse returns true and the parsed pattern", () =>
                    {
                        Check.True(UrlPattern.TryParse("/api/{*rest}", out UrlPattern result), "result");
                        Check.NotNull(result, "out value");
                        Check.True(result.IsCatchAll, "IsCatchAll");
                    }),
                    Case(Id, "TryParseInvalid", "TryParse returns false for a misplaced catch-all", () =>
                    {
                        Check.False(UrlPattern.TryParse("/{*rest}/edit", out UrlPattern result), "result");
                        Check.Null(result, "out value");
                    }),
                    Case(Id, "TryParseLiteralQuirks", "TryParse accepts every literal brace form", () =>
                    {
                        foreach (string pattern in new string[] { "/{}", "/{*}", "/*", "/**", "/{id", "/id}", "/}{", "/{*rest", "/v{*}" })
                        {
                            Check.True(UrlPattern.TryParse(pattern, out UrlPattern result), "TryParse " + pattern);
                            Check.Equal(0, result.ParameterCount, "ParameterCount for " + pattern);
                            Check.False(result.IsCatchAll, "IsCatchAll for " + pattern);
                        }
                    }),
                    Case(Id, "ParsedPatternReusedAcrossUrls", "One parsed pattern matched against many URLs", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("/users/{id}");
                        for (int i = 0; i < 50; i++)
                        {
                            Check.True(Matcher.Match("/users/" + i, p, out NameValueCollection vals), "match " + i);
                            Check.Equal(i.ToString(), vals["id"], "id " + i);
                        }
                        Check.False(Matcher.Match("/posts/1", p, out NameValueCollection none), "no match");
                        Check.Equal(0, none.Count, "empty on failure");
                    }),
                    Case(Id, "ParsedPatternUnchangedByMatching", "Matching does not change a parsed pattern", () =>
                    {
                        UrlPattern p = UrlPattern.Parse("/api/{*rest}");
                        Matcher.Match("/api/a/b", p, out NameValueCollection _);
                        Matcher.Match("/other", p, out NameValueCollection _);
                        Check.Equal(2, p.SegmentCount, "SegmentCount");
                        Check.Equal("rest", p.CatchAllName, "CatchAllName");
                        Check.Equal("/api/{*rest}", p.Pattern, "Pattern");
                    })
                });
        }

        private static TestCaseDescriptor Shape(string suiteId, string caseId, string pattern, int segments, int literals, int parameters, int literalPrefix, bool isCatchAll, string? catchAllName)
        {
            return Case(suiteId, "Shape" + caseId, "Parse " + pattern + " reports its shape", () =>
            {
                UrlPattern p = UrlPattern.Parse(pattern);
                Check.Equal(pattern, p.Pattern, "Pattern");
                Check.Equal(segments, p.SegmentCount, "SegmentCount");
                Check.Equal(segments, p.Segments.Count, "Segments.Count");
                Check.Equal(isCatchAll ? segments - 1 : segments, p.FixedSegmentCount, "FixedSegmentCount");
                Check.Equal(literals, p.LiteralCount, "LiteralCount");
                Check.Equal(parameters, p.ParameterCount, "ParameterCount");
                Check.Equal(literalPrefix, p.LiteralPrefixCount, "LiteralPrefixCount");
                Check.Equal(isCatchAll, p.IsCatchAll, "IsCatchAll");
                Check.Equal(catchAllName, p.CatchAllName, "CatchAllName");
            });
        }

        private static void Segment(UrlPatternSegment segment, string text, SegmentTypeEnum type, string? name)
        {
            Check.Equal(text, segment.Text, "Text of " + text);
            Check.Equal(type, segment.Type, "Type of " + text);
            Check.Equal(name, segment.Name, "Name of " + text);
        }
    }
}
