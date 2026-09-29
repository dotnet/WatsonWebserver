#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using Touchstone.Core;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Encoded, special, whitespace, and Unicode characters.
    /// </summary>
    public static class CharactersSuite
    {
        private const string Id = "UrlMatcher.Characters";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Characters",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Match(Id, "UrlEncodedValues", "Percent-encoded values are not decoded", "/hello%20world", "/{greeting}", "greeting", "hello%20world"),
                    Match(Id, "SpecialCharactersInLiteral", "Hyphens and underscores in literals", "/api-v2/user_profile", "/api-v2/user_profile"),
                    Match(Id, "SpecialCharactersInParameter", "Hyphens and underscores in values", "/user-123_test", "/{userId}", "userId", "user-123_test"),
                    Match(Id, "PatternWithSpaces", "Spaces in a literal", "/hello world", "/hello world"),
                    Match(Id, "UrlWithSpaces", "Spaces in a value", "/hello world", "/{greeting}", "greeting", "hello world"),
                    Match(Id, "UnicodeCharactersInLiteral", "Unicode literal", "/用户/列表", "/用户/列表"),
                    Match(Id, "UnicodeCharactersInParameterValue", "Unicode value", "/users/用户123", "/users/{id}", "id", "用户123"),

                    // additional cases
                    Match(Id, "EncodedSlashNotSplit", "%2F does not split segments", "/a%2Fb", "/{x}", "x", "a%2Fb"),
                    NoMatch(Id, "EncodedSlashNotSplitNegative", "%2F does not count as a separator", "/a%2Fb", "/a/b"),
                    NoMatch(Id, "EncodedLiteralNotDecoded", "An encoded URL does not match a decoded literal", "/hello%20world", "/hello world"),
                    NoMatch(Id, "DecodedUrlVsEncodedLiteral", "A decoded URL does not match an encoded literal", "/hello world", "/hello%20world"),
                    Match(Id, "EncodedLiteralMatchesEncoded", "Encoded literal matches identical encoded URL", "/hello%20world", "/hello%20world"),
                    NoMatch(Id, "PercentEncodingCaseSensitive", "%2f and %2F are different literals", "/a%2fb", "/a%2Fb"),
                    Match(Id, "EncodedQuestionMarkNotStripped", "%3F is not treated as a query", "/a%3Fb", "/{x}", "x", "a%3Fb"),
                    Match(Id, "EncodedHashNotStripped", "%23 is not treated as a fragment", "/a%23b", "/{x}", "x", "a%23b"),
                    Match(Id, "PlusSignKept", "Plus sign is not converted to a space", "/a+b", "/{x}", "x", "a+b"),
                    Match(Id, "TildeAndDollar", "Tilde and dollar characters", "/~user/$var", "/~user/{v}", "v", "$var"),
                    Match(Id, "ExclamationAndParens", "Sub-delimiter characters", "/!(a)'*", "/{x}", "x", "!(a)'*"),
                    Match(Id, "TabCharacter", "Tab character in a value", "/a\tb", "/{x}", "x", "a\tb"),
                    Match(Id, "NewlineCharacter", "Newline character in a value", "/a\nb", "/{x}", "x", "a\nb"),
                    Match(Id, "EmojiValue", "Surrogate-pair characters in a value", "/u/😀", "/u/{x}", "x", "😀"),
                    Match(Id, "EmojiLiteral", "Surrogate-pair characters in a literal", "/😀", "/😀"),
                    NoMatch(Id, "UnicodeNormalizationNotApplied", "Composed and decomposed forms are different literals", "/café", "/café"),
                    NoMatch(Id, "UnicodeCaseSensitive", "Unicode literals are case-sensitive", "/É", "/é"),
                    Match(Id, "NullCharacterInValue", "A NUL character is an ordinary character", "/a\u0000b", "/{x}", "x", "a\u0000b"),
                    Match(Id, "BraceCharactersInUrlLiteral", "Braces in the URL match a literal only through {} or unclosed forms", "/{id", "/{id")
                });
        }
    }
}
