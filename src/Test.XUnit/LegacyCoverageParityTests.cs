namespace Test.XUnit
{
    using System;
    using System.Collections.Generic;
    using global::Test.Automated;
    using Xunit;

    /// <summary>
    /// xUnit parity coverage for every named legacy automated test.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    [Collection("LegacyCoverageParity")]
    public class LegacyCoverageParityTests
    {
        private readonly LegacyCoverageParityFixture _Fixture;

        /// <summary>
        /// Construct the parity test class.
        /// </summary>
        /// <param name="fixture">Shared cached fixture.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fixture"/> is null.</exception>
        public LegacyCoverageParityTests(LegacyCoverageParityFixture fixture)
        {
            _Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        }

        /// <summary>
        /// Enumerates shared legacy parity cases.
        /// </summary>
        public static IEnumerable<object[]> LegacyCoverageCases
        {
            get
            {
                return LegacyCoverageParityData.Cases;
            }
        }

        /// <summary>
        /// Verify the named legacy automated case passed when executed from the shared suite.
        /// </summary>
        /// <param name="testName">Legacy automated test name.</param>
        [Theory]
        [MemberData(nameof(LegacyCoverageCases), DisableDiscoveryEnumeration = true)]
        public void LegacyCoverageCasePasses(string testName)
        {
            bool found = _Fixture.Results.TryGetValue(testName, out AutomatedTestResult result);

            if (!found && IsSkippableLiveHttp3Case(testName))
            {
                return;
            }

            Assert.True(found, "Legacy coverage result not found for '" + testName + "'.");
            Assert.True(result.Passed, result.ErrorMessage ?? ("Legacy coverage test failed for '" + testName + "'."));
        }

        private static bool IsSkippableLiveHttp3Case(string testName)
        {
            if (String.IsNullOrEmpty(testName))
            {
                return false;
            }

            return testName.StartsWith("HTTP/3 QUIC Transport -", StringComparison.Ordinal)
                || String.Equals(testName, "Alt-Svc End-To-End - HTTP/3 Response Header", StringComparison.Ordinal);
        }
    }
}
