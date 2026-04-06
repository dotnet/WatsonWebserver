namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Cross-protocol route method parity coverage executed in the console smoke test runner.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class SharedRouteMethodParitySuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the cross-protocol route method parity coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();

            await ExecuteTestAsync("Parity :: GET Static Route", SharedRouteMethodParityTests.RunGetStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: POST Static Route", SharedRouteMethodParityTests.RunPostStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: PUT Static Route", SharedRouteMethodParityTests.RunPutStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: DELETE Static Route", SharedRouteMethodParityTests.RunDeleteStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: PATCH Static Route", SharedRouteMethodParityTests.RunPatchStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: HEAD Static Route", SharedRouteMethodParityTests.RunHeadStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: OPTIONS Static Route", SharedRouteMethodParityTests.RunOptionsStaticRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: GET Parameter Route", SharedRouteMethodParityTests.RunGetParameterRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: POST Parameter Route", SharedRouteMethodParityTests.RunPostParameterRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: GET Dynamic Route", SharedRouteMethodParityTests.RunGetDynamicRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: GET Content Route", SharedRouteMethodParityTests.RunGetContentRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: GET API Route", SharedRouteMethodParityTests.RunGetApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: POST API Route", SharedRouteMethodParityTests.RunPostApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: PUT API Route", SharedRouteMethodParityTests.RunPutApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: PATCH API Route", SharedRouteMethodParityTests.RunPatchApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: DELETE API Route", SharedRouteMethodParityTests.RunDeleteApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: HEAD API Route", SharedRouteMethodParityTests.RunHeadApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: OPTIONS API Route", SharedRouteMethodParityTests.RunOptionsApiRouteParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Parity :: Not Found", SharedRouteMethodParityTests.RunNotFoundParityAsync).ConfigureAwait(false);

            return _Results.ToArray();
        }

        private async Task ExecuteTestAsync(string testName, Func<Task> test)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            AutomatedTestResult result = new AutomatedTestResult();
            result.SuiteName = String.Empty;
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
