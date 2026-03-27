namespace Test.Automated
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Executes all automated test suites.
    /// </summary>
    public static class AutomatedTestExecution
    {
        /// <summary>
        /// Run all automated suites and collect results.
        /// </summary>
        /// <returns>Ordered test results.</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        public static async Task<IReadOnlyList<AutomatedTestResult>> RunAllAsync()
        {
            List<AutomatedTestResult> results = new List<AutomatedTestResult>();
            LegacyCoverageSuite legacyCoverageSuite = new LegacyCoverageSuite();
            OptimizationCoverageSuite optimizationCoverageSuite = new OptimizationCoverageSuite();

            results.AddRange(await legacyCoverageSuite.RunAsync().ConfigureAwait(false));
            results.AddRange(await optimizationCoverageSuite.RunAsync().ConfigureAwait(false));

            return results.ToArray();
        }
    }
}
