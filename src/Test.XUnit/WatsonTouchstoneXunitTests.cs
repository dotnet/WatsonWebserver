namespace Test.XUnit
{
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// xUnit adapter over the shared Touchstone suites. Every non-skipped test case defined in
    /// <see cref="WatsonTestSuites"/> is projected into a single xUnit theory row so the entire
    /// shared suite runs under <c>dotnet test</c> without duplicating any test logic.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public sealed class WatsonTouchstoneXunitTests
    {
        /// <summary>
        /// Projects the shared suites into xUnit theory data, one row per non-skipped case.
        /// </summary>
        /// <returns>Theory data of shared test-case descriptors.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in WatsonTestSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                    {
                        data.Add(testCase);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Execute a single shared test case.
        /// </summary>
        /// <param name="testCase">Shared test-case descriptor.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases), DisableDiscoveryEnumeration = true)]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            Assert.NotNull(testCase);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
