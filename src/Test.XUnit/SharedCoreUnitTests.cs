namespace Test.XUnit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Xunit;

    /// <summary>
    /// xUnit coverage for the shared core unit-test subset.
    /// </summary>
    public class SharedCoreUnitTests
    {
        /// <summary>
        /// Execute each shared core unit test case.
        /// </summary>
        /// <param name="testCase">Shared test case.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(GetTests))]
        public async Task SharedCoreUnitCasePasses(SharedNamedTestCase testCase)
        {
            Assert.NotNull(testCase);
            await testCase.ExecuteAsync();
        }

        /// <summary>
        /// Get the shared core unit tests for xUnit.
        /// </summary>
        /// <returns>Shared test case data.</returns>
        public static IEnumerable<object[]> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();
            tests.AddRange(Test.Shared.SharedCoreUnitTests.GetTests());
            tests.AddRange(Test.Shared.SharedRequestParametersTests.GetTests());
            tests.AddRange(Test.Shared.SharedMiddlewarePipelineTests.GetTests());
            tests.AddRange(Test.Shared.SharedWebSocketTests.GetTests());
            tests.AddRange(Test.Shared.SharedNetstandard21CompatTests.GetTests());

            for (int i = 0; i < tests.Count; i++)
            {
                yield return new object[] { tests[i] };
            }
        }
    }
}
