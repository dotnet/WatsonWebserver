namespace Test.XUnit
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Test.Automated;
    using Xunit;

    /// <summary>
    /// Verifies that all shared automated coverage tests pass.
    /// </summary>
    public class AutomatedCoverageTests
    {
        /// <summary>
        /// Assert that all shared automated test results passed.
        /// </summary>
        [Fact]
        public void SharedAutomatedSuitePassed()
        {
            IReadOnlyList<AutomatedTestResult> results = AutomatedCoverageCache.Results;
            StringBuilder failureBuilder = new StringBuilder();
            int failureCount = 0;

            Assert.NotEmpty(results);

            foreach (AutomatedTestResult result in results)
            {
                if (!result.Passed)
                {
                    failureCount += 1;
                    failureBuilder.AppendLine(result.DisplayName + ": " + (result.ErrorMessage ?? "The shared automated test reported failure."));
                }
            }

            Assert.True(failureCount < 1, failureBuilder.ToString());
        }
    }
}
