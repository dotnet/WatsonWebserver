namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the automated console test runner.
    /// When running under xUnit, the test SDK provides the entry point.
    /// This class is used only when running the project directly as a console application.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Execute the automated test suites.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Process exit code.</returns>
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        public static async Task Main(string[] args)
        {
            string resultsPath = ParseResultsPath(args);
            AutomatedConsoleRunner runner = new AutomatedConsoleRunner(resultsPath);
            AutomatedRunSummary summary = await runner.RunAsync().ConfigureAwait(false);
            Environment.Exit(summary.FailedCount > 0 ? 1 : 0);
        }

        private static string ParseResultsPath(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                return null;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (String.Equals(args[i], "--results", StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
