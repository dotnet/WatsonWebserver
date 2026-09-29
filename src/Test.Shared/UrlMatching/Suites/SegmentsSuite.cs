#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Touchstone.Core;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Segment splitting, counts, and slash handling.
    /// </summary>
    public static class SegmentsSuite
    {
        private const string Id = "UrlMatcher.Segments";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            string manyUrl = "/" + String.Join("/", Enumerable.Range(0, 200).Select(i => "s" + i));
            string manyPattern = "/" + String.Join("/", Enumerable.Range(0, 199).Select(i => "s" + i)) + "/{last}";
            string manyPatternShort = "/" + String.Join("/", Enumerable.Range(0, 199).Select(i => "s" + i));
            string longSegment = new string('x', 10000);

            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Segments",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    NoMatch(Id, "DifferentPartCountsMoreUrlParts", "URL with more segments than the pattern", "/users/42/extra", "/users/{id}"),
                    NoMatch(Id, "DifferentPartCountsMorePatternParts", "Pattern with more segments than the URL", "/users/42", "/users/{id}/extra"),
                    Match(Id, "LeadingSlashHandling", "Leading slash on both", "/users/42", "/{resource}/{id}", "resource", "users", "id", "42"),
                    Match(Id, "TrailingSlashOnUrl", "Trailing slash on the URL is ignored", "/users/42/", "/users/{id}", "id", "42"),
                    Match(Id, "TrailingSlashOnPattern", "Trailing slash on the pattern is ignored", "/users/42", "/users/{id}/", "id", "42"),
                    Match(Id, "NoSlashes", "No slashes on either side", "users", "{resource}", "resource", "users"),
                    Match(Id, "RootPath", "Root pattern matches the root URL", "/", "/"),
                    Match(Id, "SinglePartUrl", "Single literal segment", "/users", "/users"),
                    Match(Id, "ManyPartsUrl", "Eight literal segments", "/a/b/c/d/e/f/g/h", "/a/b/c/d/e/f/g/h"),
                    Match(Id, "LongUrl", "Twenty-six segments ending in a parameter",
                        "/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/z",
                        "/a/b/c/d/e/f/g/h/i/j/k/l/m/n/o/p/q/r/s/t/u/v/w/x/y/{letter}",
                        "letter", "z"),
                    Match(Id, "ConsecutiveSlashesCollapsed", "Repeated slashes in the URL collapse", "/users//42", "/users/{id}", "id", "42"),
                    Match(Id, "DotSegmentsAsLiterals", "Single-dot segments are literals, not normalized", "/files/./config", "/files/./config"),
                    NoMatch(Id, "LongerPatternNegative", "Three parameters against two segments", "/a/b", "/{one}/{two}/{three}"),
                    NoMatch(Id, "ParameterCannotMatchMissingPart", "A trailing parameter with no URL segment", "/users", "/users/{id}"),

                    // additional slash handling
                    Match(Id, "UrlWithoutLeadingSlash", "URL without a leading slash", "users/42", "/users/{id}", "id", "42"),
                    Match(Id, "PatternWithoutLeadingSlash", "Pattern without a leading slash", "/users/42", "users/{id}", "id", "42"),
                    Match(Id, "LeadingRepeatedSlashesOnUrl", "Repeated leading slashes on the URL", "///users/42", "/users/{id}", "id", "42"),
                    Match(Id, "TrailingRepeatedSlashesOnUrl", "Repeated trailing slashes on the URL", "/users/42///", "/users/{id}", "id", "42"),
                    Match(Id, "ConsecutiveSlashesInPattern", "Repeated slashes in the pattern collapse", "/users/42", "/users//{id}", "id", "42"),
                    Match(Id, "OnlySlashesUrl", "URL of only slashes matches the root pattern", "///", "/"),
                    Match(Id, "OnlySlashesPattern", "Pattern of only slashes matches the root URL", "/", "///"),
                    Match(Id, "OnlySlashesBoth", "Only slashes on both sides", "//", "////"),
                    NoMatch(Id, "OnlySlashesPatternVsSegment", "Pattern of only slashes does not match a segment", "/a", "///"),
                    Match(Id, "DoubleDotSegmentsAsLiterals", "Double-dot segments are literals, not normalized", "/a/../b", "/a/../b"),
                    NoMatch(Id, "DoubleDotNotResolved", "a/../b is not resolved to b", "/a/../b", "/b"),
                    NoMatch(Id, "SingleDotNotRemoved", "a/./b is not reduced to a/b", "/a/./b", "/a/b"),
                    Match(Id, "BackslashNotSeparator", "Backslash does not split segments", "/a\\b", "/{x}", "x", "a\\b"),
                    NoMatch(Id, "BackslashNotSeparatorNegative", "Backslash does not split into two segments", "/a\\b", "/a/b"),
                    Match(Id, "WhitespaceOnlyUrl", "A URL that is only a space is one segment", " ", "/{x}", "x", " "),
                    Match(Id, "WhitespaceOnlyPattern", "A pattern that is only a space is one literal segment", " ", " "),
                    NoMatch(Id, "WhitespaceOnlyPatternVsRoot", "A space segment does not match the root", "/", " "),
                    Match(Id, "TwoHundredSegments", "Two hundred segments ending in a parameter", manyUrl, manyPattern, "last", "s199"),
                    NoMatch(Id, "TwoHundredSegmentsOffByOne", "Two hundred segments against one hundred ninety-nine", manyUrl, manyPatternShort),
                    Match(Id, "VeryLongSegmentLiteral", "Ten-thousand character literal segment", "/" + longSegment, "/" + longSegment),
                    NoMatch(Id, "VeryLongSegmentLiteralOffByOne", "Ten-thousand character literal differing in the last character", "/" + longSegment, "/" + longSegment.Substring(1) + "y")
                });
        }
    }
}
