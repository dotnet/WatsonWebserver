namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Shared legacy smoke coverage executed in the console smoke runner.
    /// </summary>
    public class SharedLegacySmokeSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared legacy smoke coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();
            await ExecuteTestAsync("HTTP/1.1 basic GET request", SharedLegacySmokeTests.TestHttp11BasicGetAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 basic POST request", SharedLegacySmokeTests.TestHttp11BasicPostAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 basic DELETE request", SharedLegacySmokeTests.TestHttp11BasicDeleteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 parameter route request", SharedLegacySmokeTests.TestHttp11ParameterRouteAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/1.1 query-string route request", SharedLegacySmokeTests.TestHttp11QueryStringRouteAsync).ConfigureAwait(false);
            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = "Shared Legacy Smoke Coverage";
            result.TestName = testName;

            try
            {
                await test().ConfigureAwait(false);
                result.Passed = true;
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                _Results.Add(result);
                AutomatedTestReporter.ResultRecorded?.Invoke(result);
            }
        }
    }
}
