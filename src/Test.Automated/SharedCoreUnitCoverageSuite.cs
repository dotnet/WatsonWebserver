namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Executes shared core unit tests inside the automated console runner.
    /// </summary>
    public class SharedCoreUnitCoverageSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared core unit-test subset.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();
            await ExecuteTestsAsync(SharedCoreUnitTests.GetTests()).ConfigureAwait(false);
            await ExecuteTestsAsync(SharedRequestParametersTests.GetTests()).ConfigureAwait(false);
            await ExecuteTestsAsync(SharedMiddlewarePipelineTests.GetTests()).ConfigureAwait(false);
            await ExecuteTestsAsync(SharedWebSocketTests.GetTests()).ConfigureAwait(false);
            await ExecuteTestsAsync(SharedNetstandard21CompatTests.GetTests()).ConfigureAwait(false);
            await ExecuteTestsAsync(SharedOpenApiCompositionTests.GetTests()).ConfigureAwait(false);

            return _Results.ToArray();
        }

        private async Task ExecuteTestsAsync(IReadOnlyList<SharedNamedTestCase> tests)
        {
            if (tests == null) throw new ArgumentNullException(nameof(tests));

            for (int i = 0; i < tests.Count; i++)
            {
                await ExecuteTestAsync(tests[i]).ConfigureAwait(false);
            }
        }

        private async Task ExecuteTestAsync(SharedNamedTestCase test)
        {
            if (test == null) throw new ArgumentNullException(nameof(test));

            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = String.Empty;
            result.TestName = test.Name;

            try
            {
                await test.ExecuteAsync().ConfigureAwait(false);
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
