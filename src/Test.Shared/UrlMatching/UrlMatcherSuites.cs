#nullable enable
namespace Test.Shared.UrlMatching
{
    using System;
    using System.Collections.Generic;
    using Test.Shared.UrlMatching.Suites;
    using Touchstone.Core;

    /// <summary>
    /// Every UrlMatcher test suite, copied from the UrlMatcher repository (src/Test.Shared, v3.1.0) so Watson
    /// verifies the exact matching behavior it depends on.  Keep in sync when the UrlMatcher dependency changes:
    /// namespaces are rewritten to Test.Shared.UrlMatching and suite ids are prefixed with "UrlMatcher.".
    /// </summary>
    public static class UrlMatcherSuites
    {
        /// <summary>
        /// All suites, in execution order.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    MatchingSuite.Create(),
                    ParametersSuite.Create(),
                    SegmentsSuite.Create(),
                    QueryAndFragmentSuite.Create(),
                    UriSuite.Create(),
                    CharactersSuite.Create(),
                    PartsSuite.Create(),
                    ArgumentValidationSuite.Create(),
                    MalformedPatternsSuite.Create(),
                    CatchAllSuite.Create(),
                    UrlPatternSuite.Create(),
                    WatsonCompatibilitySuite.Create(),
                    ConcurrencySuite.Create()
                };
            }
        }
    }
}
