namespace Test.Automated
{
    using System;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    /// <summary>
    /// Console entry point for the Touchstone CLI test runner. Executes every suite defined by the
    /// shared source of truth (<see cref="WatsonTestSuites"/>) and returns a process exit code of 0
    /// when all tests pass and 1 when any test fails.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal static class Program
    {
        /// <summary>
        /// Execute the shared test suites through the Touchstone console runner.
        /// </summary>
        /// <param name="args">Command line arguments. Supports "--results &lt;path&gt;" to export JSON.</param>
        /// <returns>Process exit code.</returns>
        public static async Task<int> Main(string[] args)
        {
            string resultsPath = ParseResultsPath(args);
            return await ConsoleRunner.RunAsync(WatsonTestSuites.All, resultsPath: resultsPath).ConfigureAwait(false);
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
