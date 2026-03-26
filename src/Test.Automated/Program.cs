namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Entry point for the automated console test runner.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Execute the automated test suites.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            string resultsPath = ParseResultsPath(args);
            AutomatedConsoleRunner runner = new AutomatedConsoleRunner(resultsPath);
            AutomatedRunSummary summary = await runner.RunAsync().ConfigureAwait(false);
            return summary.FailedCount > 0 ? 1 : 0;
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
