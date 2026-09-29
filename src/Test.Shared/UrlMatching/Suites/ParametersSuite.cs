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
    /// Parameter capture, parameter names, and parameter-name quirks.
    /// </summary>
    public static class ParametersSuite
    {
        private const string Id = "UrlMatcher.Parameters";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Parameters",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Match(Id, "ParameterExtractionSingle", "Single parameter", "/hello", "/{greeting}", "greeting", "hello"),
                    Match(Id, "ParameterExtractionMultiple", "Multiple parameters",
                        "/api/v2/products/123/reviews/456",
                        "/{prefix}/{version}/products/{productId}/reviews/{reviewId}",
                        "prefix", "api", "version", "v2", "productId", "123", "reviewId", "456"),
                    Case(Id, "CaseInsensitivityParameterNames", "Parameter names are looked up case-insensitively", () =>
                    {
                        Check.True(Matcher.Match("/users/42", "/{UserId}/{ID}", out NameValueCollection vals), "match");
                        Check.Equal("users", vals["userid"], "userid");
                        Check.Equal("users", vals["USERID"], "USERID");
                        Check.Equal("users", vals["UserId"], "UserId");
                        Check.Equal("42", vals["id"], "id");
                        Check.Equal("42", vals["Id"], "Id");
                        Check.Equal("42", vals["ID"], "ID");
                    }),
                    Match(Id, "ParameterAtStart", "Parameter as the first segment", "/123/users", "/{id}/users", "id", "123"),
                    Match(Id, "ParameterAtEnd", "Parameter as the last segment", "/users/123", "/users/{id}", "id", "123"),
                    Match(Id, "AllParameters", "Every segment is a parameter", "/foo/bar/baz", "/{a}/{b}/{c}", "a", "foo", "b", "bar", "c", "baz"),
                    Match(Id, "ParameterNameWithUnderscores", "Parameter name with underscores", "/users/42", "/users/{user_id}", "user_id", "42"),
                    Match(Id, "ParameterNameWithNumbers", "Parameter name with digits", "/users/42", "/users/{id1}", "id1", "42"),
                    Case(Id, "DuplicateParameterNames", "Duplicate names join values with a comma", () =>
                    {
                        Check.True(Matcher.Match("/foo/bar", "/{id}/{id}", out NameValueCollection vals), "match");
                        Check.Equal(1, vals.Count, "one distinct key");
                        Check.Equal("foo,bar", vals["id"], "joined value");
                        string[]? values = vals.GetValues("id");
                        Check.NotNull(values, "GetValues");
                        Check.Equal(2, values!.Length, "value count");
                        Check.Equal("foo", values[0], "first value");
                        Check.Equal("bar", values[1], "second value");
                    }),
                    Match(Id, "ParameterValueCasePreserved", "Captured value keeps its original case", "/users/AbC123", "/users/{id}", "id", "AbC123"),
                    Case(Id, "EmptyValuesCollectionOnFailure", "A failed match returns an empty, non-null collection", () =>
                    {
                        bool result = Matcher.Match("/users/42", "/posts/{id}", out NameValueCollection vals);
                        Check.False(result, "no match");
                        Check.NotNull(vals, "vals");
                        Check.Equal(0, vals.Count, "vals count");
                    }),

                    // additional parameter names
                    Match(Id, "ParameterNameWithHyphen", "Parameter name with a hyphen", "/u/42", "/u/{user-id}", "user-id", "42"),
                    Match(Id, "ParameterNameWithDot", "Parameter name with a dot", "/u/42", "/u/{user.id}", "user.id", "42"),
                    Match(Id, "ParameterNameUnicode", "Parameter name with Unicode characters", "/u/42", "/u/{名前}", "名前", "42"),
                    Match(Id, "ParameterNameSingleCharacter", "Single-character parameter name", "/u/42", "/u/{x}", "x", "42"),
                    Match(Id, "ParameterNameLong", "Long parameter name", "/u/42", "/u/{" + new string('n', 500) + "}", new string('n', 500), "42"),
                    Case(Id, "ParameterNameKeyCasePreserved", "The collection key keeps the case used in the pattern", () =>
                    {
                        Check.True(Matcher.Match("/42", "/{UserId}", out NameValueCollection vals), "match");
                        string?[] keys = vals.AllKeys;
                        Check.Equal(1, keys.Length, "key count");
                        Check.Equal("UserId", keys[0], "key text");
                    }),
                    Case(Id, "ParameterNameWithSpacesKept", "Spaces inside braces are part of the name (quirk)", () =>
                    {
                        Check.True(Matcher.Match("/users/42", "/users/{ id }", out NameValueCollection vals), "match");
                        Check.Equal("42", vals[" id "], "name with spaces");
                        Check.Null(vals["id"], "trimmed name is not present");
                    }),
                    Case(Id, "ParameterNameCaseInsensitiveDuplicate", "Names differing only by case are the same key", () =>
                    {
                        Check.True(Matcher.Match("/a/b", "/{Id}/{ID}", out NameValueCollection vals), "match");
                        Check.Equal(1, vals.Count, "one key");
                        Check.Equal("a,b", vals["id"], "joined value");
                    }),
                    Match(Id, "ParameterNameStarInMiddleIsParameter", "An asterisk after the first character does not make a catch-all", "/a", "/{a*}", "a*", "a"),

                    // additional values
                    Match(Id, "ParameterValueNumeric", "Numeric value", "/n/0", "/n/{v}", "v", "0"),
                    Match(Id, "ParameterValueWithDots", "Value with dots", "/f/archive.tar.gz", "/f/{name}", "name", "archive.tar.gz"),
                    Match(Id, "ParameterValueSingleDot", "Value that is a single dot", "/f/.", "/f/{name}", "name", "."),
                    Match(Id, "ParameterValueDoubleDot", "Value that is two dots", "/f/..", "/f/{name}", "name", ".."),
                    Match(Id, "ParameterValueWithEquals", "Value with an equals sign", "/k/a=b", "/k/{kv}", "kv", "a=b"),
                    Match(Id, "ParameterValueWithAmpersand", "Value with an ampersand", "/k/a&b", "/k/{kv}", "kv", "a&b"),
                    Match(Id, "ParameterValueWithColon", "Value with a colon", "/k/a:b", "/k/{kv}", "kv", "a:b"),
                    Match(Id, "ParameterValueWithAt", "Value with an at sign", "/k/user@host", "/k/{kv}", "kv", "user@host"),
                    Match(Id, "ParameterValueWithComma", "Value with a comma", "/k/a,b", "/k/{kv}", "kv", "a,b"),
                    Match(Id, "ParameterValueWithSemicolon", "Value with a semicolon (matrix parameter)", "/k/a;b=c", "/k/{kv}", "kv", "a;b=c"),
                    Match(Id, "ParameterValueWithBackslash", "Backslash is not a separator", "/k/a\\b", "/k/{kv}", "kv", "a\\b"),
                    Match(Id, "ParameterValueWhitespaceOnly", "Value that is only a space", "/k/ ", "/k/{kv}", "kv", " "),
                    Match(Id, "ParameterValueLong", "Very long value", "/k/" + new string('v', 10000), "/k/{kv}", "kv", new string('v', 10000)),
                    NoMatch(Id, "ParameterCannotCaptureEmptySegment", "Empty segments are discarded, so a parameter cannot capture empty text", "/users//", "/users/{id}"),
                    NoMatch(Id, "ParameterCannotCaptureTwoSegments", "A parameter captures exactly one segment", "/users/4/2", "/users/{id}"),

                    // brace-group quirks (documented behavior retained from 3.0.2)
                    Match(Id, "PrefixedParameterCapturesWholeSegment", "v{version} captures the whole segment", "/v1/users", "/v{version}/users", "version", "v1"),
                    Match(Id, "PrefixedParameterIgnoresPrefix", "v{version} matches a segment without the prefix", "/beta/users", "/v{version}/users", "version", "beta"),
                    NoMatch(Id, "PrefixedParameterLiteralStillChecked", "Other literals still apply with v{version}", "/v1/orders", "/v{version}/users"),
                    Match(Id, "SuffixedParameterCapturesWholeSegment", "{name}.json captures the whole segment", "/f/data.json", "/f/{name}.json", "name", "data.json"),
                    Match(Id, "SurroundedParameterCapturesWholeSegment", "pre{x}post captures the whole segment", "/anything", "/pre{x}post", "x", "anything"),
                    Match(Id, "TwoGroupsInSegmentUsesFirst", "{a}{b} captures only a", "/item/xy", "/item/{a}{b}", "a", "xy"),
                    Match(Id, "GroupLiteralGroupUsesFirst", "{a}-{b} captures only a", "/item/x-y", "/item/{a}-{b}", "a", "x-y"),
                    NoMatch(Id, "TwoGroupsInSegmentOneSegmentOnly", "{a}{b} is still one segment", "/item/x/y", "/item/{a}{b}"),
                    Match(Id, "LeadingCloseBraceIgnored", "}{x} captures as x", "/v", "/}{x}", "x", "v"),
                    Match(Id, "TrailingExtraCloseBrace", "{x}} captures as x", "/v", "/{x}}", "x", "v"),
                    Match(Id, "NestedOpenBraceKeptInName", "{a{b} captures as a{b", "/v", "/{a{b}", "a{b", "v"),
                    Match(Id, "DoubleBracesKeptInName", "{{x}} captures as {x", "/v", "/{{x}}", "{x", "v")
                });
        }
    }
}
