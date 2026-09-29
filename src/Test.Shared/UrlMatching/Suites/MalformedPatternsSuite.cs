#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Brace forms that are literals rather than parameters.
    /// </summary>
    public static class MalformedPatternsSuite
    {
        private const string Id = "UrlMatcher.MalformedPatterns";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Malformed Patterns",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Match(Id, "EmptyBracesAreLiteral", "{} is a literal", "/{}", "/{}"),
                    Match(Id, "MalformedPatternMissingCloseBrace", "{id without a closing brace is a literal", "/{id", "/{id"),
                    Match(Id, "MalformedPatternMissingOpenBrace", "id} without an opening brace is a literal", "/id}", "/id}"),

                    // {} literal
                    NoMatch(Id, "EmptyBracesDoNotCapture", "{} does not match an arbitrary value", "/42", "/{}"),
                    Match(Id, "EmptyBracesInsideSegment", "a{}b is a literal", "/a{}b", "/a{}b"),
                    NoMatch(Id, "EmptyBracesInsideSegmentNegative", "a{}b does not match other text", "/axb", "/a{}b"),
                    Match(Id, "EmptyBracesWithLiteralSegments", "{} among other segments", "/files/{}/x", "/files/{}/x"),

                    // {*} literal (changed from 3.0.2, where it was a parameter named *)
                    Match(Id, "StarBracesAreLiteral", "{*} is a literal", "/files/{*}", "/files/{*}"),
                    NoMatch(Id, "StarBracesDoNotCapture", "{*} does not match an arbitrary value", "/files/42", "/files/{*}"),
                    NoMatch(Id, "StarBracesDoNotCatchAll", "{*} does not match several segments", "/files/a/b", "/files/{*}"),
                    Match(Id, "StarBracesInsideSegmentLiteral", "v{*} is a literal", "/v{*}", "/v{*}"),
                    NoMatch(Id, "StarBracesInsideSegmentNegative", "v{*} does not match other text", "/v1", "/v{*}"),
                    Match(Id, "StarBracesNotLastIsLiteral", "{*} before other segments does not throw", "/{*}/edit", "/{*}/edit"),

                    // bare asterisks
                    Match(Id, "BareStarIsLiteral", "* is a literal", "/files/*", "/files/*"),
                    NoMatch(Id, "BareStarDoesNotCapture", "* does not match a value", "/files/a", "/files/*"),
                    NoMatch(Id, "BareStarDoesNotCatchAll", "* does not match several segments", "/files/a/b", "/files/*"),
                    Match(Id, "DoubleStarIsLiteral", "** is a literal", "/files/**", "/files/**"),
                    NoMatch(Id, "DoubleStarDoesNotCatchAll", "** does not match several segments", "/files/a/b", "/files/**"),
                    Match(Id, "StarNameWithoutBracesIsLiteral", "*rest is a literal", "/files/*rest", "/files/*rest"),

                    // unbalanced and reversed braces
                    NoMatch(Id, "MissingCloseBraceDoesNotCapture", "{id does not match a value", "/42", "/{id"),
                    NoMatch(Id, "MissingOpenBraceDoesNotCapture", "id} does not match a value", "/42", "/id}"),
                    Match(Id, "UnclosedCatchAllIsLiteral", "{*rest without a closing brace is a literal", "/{*rest", "/{*rest"),
                    NoMatch(Id, "UnclosedCatchAllDoesNotCatchAll", "{*rest without a closing brace does not match several segments", "/a/b", "/{*rest"),
                    Match(Id, "UnopenedCatchAllIsLiteral", "*rest} without an opening brace is a literal", "/*rest}", "/*rest}"),
                    Match(Id, "ReversedBracesAreLiteral", "}{ is a literal", "/}{", "/}{"),
                    NoMatch(Id, "ReversedBracesDoNotCapture", "}{ does not match a value", "/42", "/}{"),
                    Match(Id, "ReversedBracesWithNameAreLiteral", "}id{ is a literal", "/}id{", "/}id{"),
                    Match(Id, "OpenBraceOnlyIsLiteral", "{ is a literal", "/{", "/{"),
                    Match(Id, "CloseBraceOnlyIsLiteral", "} is a literal", "/}", "/}")
                });
        }
    }
}
