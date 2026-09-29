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
    /// Null and empty argument handling for every public entry point.
    /// </summary>
    public static class ArgumentValidationSuite
    {
        private const string Id = "UrlMatcher.ArgumentValidation";

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            Uri uri = new Uri("http://127.0.0.1/a");
            UrlPattern pattern = UrlPattern.Parse("/{x}");
            Matcher matcher = new Matcher("/a");

            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Argument Validation",
                cases: new List<TestCaseDescriptor>
                {
                    // ported from AutomatedTest
                    Null(Id, "NullUrl", "Matcher.Match(null, string)", "url", () => Matcher.Match((string)null!, "/{id}", out NameValueCollection _)),
                    Null(Id, "EmptyUrl", "Matcher.Match(empty, string)", "url", () => Matcher.Match("", "/{id}", out NameValueCollection _)),
                    Null(Id, "NullPattern", "Matcher.Match(string, (string)null)", "pattern", () => Matcher.Match("/users", (string)null!, out NameValueCollection _)),
                    Null(Id, "EmptyPattern", "Matcher.Match(string, empty)", "pattern", () => Matcher.Match("/users", "", out NameValueCollection _)),
                    Null(Id, "NullUri", "Matcher.Match((Uri)null, string)", "uri", () => Matcher.Match((Uri)null!, "/{id}", out NameValueCollection _)),

                    // constructors
                    Null(Id, "ConstructorNullString", "new Matcher((string)null)", "url", () => new Matcher((string)null!)),
                    Null(Id, "ConstructorEmptyString", "new Matcher(empty)", "url", () => new Matcher("")),
                    Null(Id, "ConstructorNullUri", "new Matcher((Uri)null)", "uri", () => new Matcher((Uri)null!)),
                    Null(Id, "UrlPatternConstructorNull", "new UrlPattern(null)", "pattern", () => new UrlPattern(null!)),
                    Null(Id, "UrlPatternConstructorEmpty", "new UrlPattern(empty)", "pattern", () => new UrlPattern("")),
                    Null(Id, "UrlPatternParseNull", "UrlPattern.Parse(null)", "pattern", () => UrlPattern.Parse(null!)),
                    Null(Id, "UrlPatternParseEmpty", "UrlPattern.Parse(empty)", "pattern", () => UrlPattern.Parse("")),

                    // instance overloads
                    Null(Id, "InstanceNullPattern", "matcher.Match((string)null)", "pattern", () => matcher.Match((string)null!, out NameValueCollection _)),
                    Null(Id, "InstanceEmptyPattern", "matcher.Match(empty)", "pattern", () => matcher.Match("", out NameValueCollection _)),
                    Null(Id, "InstanceNullUrlPattern", "matcher.Match((UrlPattern)null)", "pattern", () => matcher.Match((UrlPattern)null!, out NameValueCollection _)),

                    // static UrlPattern overloads
                    Null(Id, "StaticStringNullUrlPattern", "Matcher.Match(string, (UrlPattern)null)", "pattern", () => Matcher.Match("/a", (UrlPattern)null!, out NameValueCollection _)),
                    Null(Id, "StaticNullStringUrlPattern", "Matcher.Match((string)null, UrlPattern)", "url", () => Matcher.Match((string)null!, pattern, out NameValueCollection _)),
                    Null(Id, "StaticEmptyStringUrlPattern", "Matcher.Match(empty, UrlPattern)", "url", () => Matcher.Match("", pattern, out NameValueCollection _)),
                    Null(Id, "StaticUriNullUrlPattern", "Matcher.Match(Uri, (UrlPattern)null)", "pattern", () => Matcher.Match(uri, (UrlPattern)null!, out NameValueCollection _)),
                    Null(Id, "StaticNullUriUrlPattern", "Matcher.Match((Uri)null, UrlPattern)", "uri", () => Matcher.Match((Uri)null!, pattern, out NameValueCollection _)),
                    Null(Id, "StaticUriNullStringPattern", "Matcher.Match(Uri, (string)null)", "pattern", () => Matcher.Match(uri, (string)null!, out NameValueCollection _)),
                    Null(Id, "StaticUriEmptyStringPattern", "Matcher.Match(Uri, empty)", "pattern", () => Matcher.Match(uri, "", out NameValueCollection _)),
                    Null(Id, "NullUrlCheckedBeforePattern", "Matcher.Match(null, null) reports url first", "url", () => Matcher.Match((string)null!, (string)null!, out NameValueCollection _)),
                    Null(Id, "NullUriCheckedBeforePattern", "Matcher.Match((Uri)null, null) reports uri first", "uri", () => Matcher.Match((Uri)null!, (string)null!, out NameValueCollection _)),

                    // TryParse
                    Case(Id, "TryParseNull", "UrlPattern.TryParse(null) returns false", () =>
                    {
                        Check.False(UrlPattern.TryParse(null!, out UrlPattern result), "result");
                        Check.Null(result, "out value");
                    }),
                    Case(Id, "TryParseEmpty", "UrlPattern.TryParse(empty) returns false", () =>
                    {
                        Check.False(UrlPattern.TryParse("", out UrlPattern result), "result");
                        Check.Null(result, "out value");
                    }),

                    // UrlPatternSegment
                    Null(Id, "SegmentNullText", "new UrlPatternSegment(null, ...)", "text", () => new UrlPatternSegment(null!, SegmentTypeEnum.Literal, null)),
                    Null(Id, "SegmentEmptyText", "new UrlPatternSegment(empty, ...)", "text", () => new UrlPatternSegment("", SegmentTypeEnum.Literal, null)),
                    Null(Id, "SegmentParameterWithoutName", "Parameter segment without a name", "name", () => new UrlPatternSegment("{x}", SegmentTypeEnum.Parameter, null)),
                    Null(Id, "SegmentParameterEmptyName", "Parameter segment with an empty name", "name", () => new UrlPatternSegment("{x}", SegmentTypeEnum.Parameter, "")),
                    Null(Id, "SegmentCatchAllWithoutName", "Catch-all segment without a name", "name", () => new UrlPatternSegment("{*x}", SegmentTypeEnum.CatchAll, null)),
                    Case(Id, "SegmentLiteralWithName", "Literal segment with a name throws ArgumentException", () =>
                    {
                        ArgumentException e = Check.Throws<ArgumentException>(() => new UrlPatternSegment("users", SegmentTypeEnum.Literal, "x"), "literal with name");
                        Check.Equal("name", e.ParamName, "ParamName");
                        Check.Contains(e.Message, "users", "message");
                    }),

                    // valid edge inputs that must not throw
                    Match(Id, "SingleCharacterUrl", "A one-character URL is valid", "a", "a"),
                    Match(Id, "SingleSlashUrlAndPattern", "A single slash is a valid URL and pattern", "/", "/")
                });
        }

        private static TestCaseDescriptor Null(string suiteId, string caseId, string displayName, string paramName, Action action)
        {
            return Case(suiteId, caseId, displayName + " throws ArgumentNullException", () =>
            {
                ArgumentNullException e = Check.Throws<ArgumentNullException>(action, displayName);
                Check.Equal(paramName, e.ParamName, displayName + " ParamName");
            });
        }
    }
}
