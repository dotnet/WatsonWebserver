namespace Test.Nunit
{
    using System.Collections;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit adapter over the shared Touchstone suites. Every non-skipped test case defined in
    /// <see cref="WatsonTestSuites"/> is projected into a distinct NUnit test case via
    /// <see cref="TouchstoneTestCaseSource"/> so the entire shared suite runs under <c>dotnet test</c>
    /// without duplicating any test logic.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [TestFixture]
    public sealed class WatsonTouchstoneNunitTests
    {
        /// <summary>
        /// Projects the shared suites into NUnit test-case data, one entry per non-skipped case.
        /// </summary>
        /// <returns>Enumerable of shared test-case descriptors.</returns>
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(WatsonTestSuites.All);
        }

        /// <summary>
        /// Execute a single shared test case.
        /// </summary>
        /// <param name="testCase">Shared test-case descriptor.</param>
        /// <returns>Task.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            Assert.That(testCase, Is.Not.Null);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
