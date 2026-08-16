namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Versioning;

    /// <summary>
    /// Executes the comprehensive <see cref="LegacyCoverageSuite"/> exactly once per process and
    /// caches its recorded, per-assertion results. The suite drives many assertions across a small
    /// number of shared server lifecycles (HTTP/1.1, HTTP/2, HTTP/3, TLS, and raw wire protocol), so
    /// it cannot be re-entered per assertion. Running it once here and exposing the individual results
    /// lets every Touchstone runner surface each legacy assertion as its own test case.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    public static class LegacyCoverageResults
    {
        private static readonly object _Sync = new object();
        private static IReadOnlyList<AutomatedTestResult> _Cached = null;

        /// <summary>
        /// The recorded results of the comprehensive legacy coverage suite, in execution order.
        /// The suite is executed on first access and the results are cached for the process lifetime.
        /// </summary>
        public static IReadOnlyList<AutomatedTestResult> Results
        {
            get
            {
                return EnsureResults();
            }
        }

        private static IReadOnlyList<AutomatedTestResult> EnsureResults()
        {
            lock (_Sync)
            {
                if (_Cached != null)
                {
                    return _Cached;
                }

                TextWriter standardOutput = Console.Out;
                TextWriter standardError = Console.Error;

                try
                {
                    Console.SetOut(TextWriter.Null);
                    Console.SetError(TextWriter.Null);

                    LegacyCoverageSuite suite = new LegacyCoverageSuite();
                    IReadOnlyList<AutomatedTestResult> results = suite.RunAsync().GetAwaiter().GetResult();
                    _Cached = results;
                    return _Cached;
                }
                finally
                {
                    Console.SetOut(standardOutput);
                    Console.SetError(standardError);
                }
            }
        }
    }
}
