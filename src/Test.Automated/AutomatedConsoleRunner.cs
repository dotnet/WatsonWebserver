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
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        [System.Runtime.Versioning.SupportedOSPlatform("linux")]
        [System.Runtime.Versioning.SupportedOSPlatform("macos")]
        public async System.Threading.Tasks.Task<AutomatedRunSummary> RunAsync()
        {
            bool quietMode = !String.Equals(
                Environment.GetEnvironmentVariable("WATSON_TEST_AUTOMATED_VERBOSE"),
                "1",
                StringComparison.Ordinal);
            Stopwatch stopwatch = Stopwatch.StartNew();
            int descriptionWidth = GetMaximumDescriptionWidth();
            IReadOnlyList<AutomatedTestResult> results = null;
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;

            bool useColor = !Console.IsOutputRedirected;
            WriteHeader(originalOut, descriptionWidth);

            AutomatedTestReporter.ResultRecorded = result => WriteResultLine(result, descriptionWidth, originalOut, useColor);

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
                AutomatedTestReporter.ResultRecorded = null;

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

            Console.WriteLine();
            WriteOverallLine(summary, descriptionWidth, useColor);
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

        private static void WriteHeader(TextWriter writer, int descriptionWidth)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine(
                "Test".PadRight(descriptionWidth)
                + "  "
                + "Result".PadRight(6)
                + "  "
                + "Runtime".PadLeft(8));
            writer.WriteLine(
                new string('-', descriptionWidth)
                + "  "
                + new string('-', 6)
                + "  "
                + new string('-', 8));
        }

        private static void WriteOverallLine(AutomatedRunSummary summary, int descriptionWidth, bool useColor)
        {
            bool passed = summary.FailedCount == 0;
            string runtime = FormatRuntime(summary.TotalRuntime);

            Console.Write("OVERALL".PadRight(descriptionWidth));
            Console.Write("  ");
            WriteStatus(passed, Console.Out, useColor);
            Console.Write("  ");
            Console.WriteLine(runtime.PadLeft(8));
        }

        private static void WriteResultLine(AutomatedTestResult result, int descriptionWidth, TextWriter writer, bool useColor)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            string runtime = FormatRuntime(TimeSpan.FromMilliseconds(result.ElapsedMilliseconds)).PadLeft(8);
            string description = FormatDescription(result.DisplayName, descriptionWidth);
            writer.Write(description);
            writer.Write("  ");
            WriteStatus(result.Passed, writer, useColor);
            writer.Write("  ");
            writer.WriteLine(runtime);
        }

        private static void WriteStatus(bool passed, TextWriter writer, bool useColor)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            if (!useColor)
            {
                writer.Write((passed ? "PASS" : "FAIL").PadRight(6));
                return;
            }

            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
            writer.Write((passed ? "PASS" : "FAIL").PadRight(6));
            Console.ForegroundColor = originalColor;
        }

        private static string FormatRuntime(TimeSpan runtime)
        {
            long roundedMilliseconds = (long)Math.Round(runtime.TotalMilliseconds, MidpointRounding.AwayFromZero);
            return roundedMilliseconds.ToString() + "ms";
        }

        private static string FormatDescription(string description, int descriptionWidth)
        {
            if (String.IsNullOrEmpty(description))
            {
                return String.Empty.PadRight(descriptionWidth);
            }

            if (description.Length <= descriptionWidth)
            {
                return description.PadRight(descriptionWidth);
            }

            if (descriptionWidth <= 3)
            {
                return description.Substring(0, descriptionWidth);
            }

            return description.Substring(0, descriptionWidth - 3) + "...";
        }

        private static int GetMaximumDescriptionWidth()
        {
            const int ResultColumnWidth = 6;
            const int RuntimeColumnWidth = 8;
            const int SeparatorWidth = 4;
            const int MinimumDescriptionWidth = 40;
            const int DefaultDescriptionWidth = 100;

            try
            {
                if (Console.IsOutputRedirected)
                {
                    return DefaultDescriptionWidth;
                }

                int availableWidth = Console.WindowWidth - ResultColumnWidth - RuntimeColumnWidth - SeparatorWidth;
                if (availableWidth < MinimumDescriptionWidth)
                {
                    return MinimumDescriptionWidth;
                }

                return availableWidth;
            }
            catch (IOException)
            {
                return DefaultDescriptionWidth;
            }
            catch (ArgumentOutOfRangeException)
            {
                return DefaultDescriptionWidth;
            }
            catch (PlatformNotSupportedException)
            {
                return DefaultDescriptionWidth;
            }
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
