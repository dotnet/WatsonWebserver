namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Shared protocol gap coverage executed in the console smoke test runner.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class SharedProtocolGapSuite
    {
        private readonly List<AutomatedTestResult> _Results = new List<AutomatedTestResult>();

        /// <summary>
        /// Execute the shared protocol gap coverage.
        /// </summary>
        /// <returns>Recorded results.</returns>
        public async Task<IReadOnlyList<AutomatedTestResult>> RunAsync()
        {
            _Results.Clear();

            await ExecuteTestAsync("HTTP/2 :: Writer Serialization Correctness", ProtocolGapSharedTests.RunHttp2WriterSerializationCorrectnessAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/3 :: Transport Backpressure Behavior", ProtocolGapSharedTests.RunHttp3TransportBackpressureAsync).ConfigureAwait(false);
            await ExecuteTestAsync("HTTP/3 :: Sibling Stream Survival After Abort", ProtocolGapSharedTests.RunHttp3SiblingStreamSurvivalAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Cross-Protocol :: Auth, Session, And Event Parity", ProtocolGapSharedTests.RunCrossProtocolAuthSessionEventParityAsync).ConfigureAwait(false);
            await ExecuteTestAsync("Interop :: Mixed-Version Client Interoperability", ProtocolGapSharedTests.RunMixedVersionClientInteroperabilityAsync).ConfigureAwait(false);

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
