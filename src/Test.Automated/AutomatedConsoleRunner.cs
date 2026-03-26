namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    /// Writes automated test execution output to the console.
    /// </summary>
    public class AutomatedConsoleRunner
    {
        private readonly string _ResultsPath;

        /// <summary>
        /// Initialize the automated console runner.
        /// </summary>
        /// <param name="resultsPath">Optional path to a serialized results file.</param>
        public AutomatedConsoleRunner(string resultsPath = null)
        {
            _ResultsPath = resultsPath;
        }

        /// <summary>
        /// Run the automated suites and print formatted output.
        /// </summary>
        /// <returns>Execution summary.</returns>
        public async System.Threading.Tasks.Task<AutomatedRunSummary> RunAsync()
        {
            bool quietMode = String.Equals(
                Environment.GetEnvironmentVariable("WATSON_TEST_AUTOMATED_QUIET"),
                "1",
                StringComparison.Ordinal);
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<AutomatedTestResult> results = null;
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;

            if (quietMode)
            {
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
            }

            try
            {
                results = await AutomatedTestExecution.RunAllAsync().ConfigureAwait(false);
            }
            finally
            {
                if (quietMode)
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalError);
                }
            }

            stopwatch.Stop();

            AutomatedRunSummary summary = BuildSummary(results, stopwatch.Elapsed);
            string resultsPath = !String.IsNullOrEmpty(_ResultsPath)
                ? _ResultsPath
                : Environment.GetEnvironmentVariable("WATSON_TEST_AUTOMATED_RESULTS_PATH");

            if (!String.IsNullOrEmpty(resultsPath))
            {
                string serializedResults = JsonSerializer.Serialize(results);
                File.WriteAllText(resultsPath, serializedResults);
            }

            Console.WriteLine("PASS/FAIL  Runtime  Test");
            Console.WriteLine("---------  -------  ----");

            foreach (AutomatedTestResult result in results)
            {
                string status = result.Passed ? "PASS" : "FAIL";
                string suffix = String.IsNullOrEmpty(result.ErrorMessage) ? String.Empty : " - " + result.ErrorMessage;
                Console.WriteLine(status.PadRight(9) + result.ElapsedMilliseconds.ToString().PadLeft(7) + "ms  " + result.DisplayName + suffix);
            }

            Console.WriteLine();
            Console.WriteLine("OVERALL " + (summary.FailedCount > 0 ? "FAIL" : "PASS") + "  " + Math.Round(summary.TotalRuntime.TotalMilliseconds).ToString() + "ms");
            Console.WriteLine("Total: " + summary.TotalCount.ToString() + "  Passed: " + summary.PassedCount.ToString() + "  Failed: " + summary.FailedCount.ToString());

            if (summary.FailedCount > 0)
            {
                Console.WriteLine("Failed Tests:");

                foreach (AutomatedTestResult result in summary.Results)
                {
                    if (!result.Passed)
                    {
                        Console.WriteLine(result.DisplayName);
                    }
                }
            }

            return summary;
        }

        private static AutomatedRunSummary BuildSummary(IReadOnlyList<AutomatedTestResult> results, TimeSpan totalRuntime)
        {
            AutomatedRunSummary summary = new AutomatedRunSummary();
            summary.Results = results;
            summary.TotalRuntime = totalRuntime;

            foreach (AutomatedTestResult result in results)
            {
                summary.TotalCount++;

                if (result.Passed)
                {
                    summary.PassedCount++;
                }
                else
                {
                    summary.FailedCount++;
                }
            }

            return summary;
        }
    }
}
