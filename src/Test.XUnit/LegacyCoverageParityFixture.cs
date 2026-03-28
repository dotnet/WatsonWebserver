namespace Test.XUnit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using global::Test.Automated;

    /// <summary>
    /// Cached shared legacy coverage results for xUnit parity assertions.
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    public class LegacyCoverageParityFixture
    {
        private static readonly object _Sync = new object();
        private static IReadOnlyDictionary<string, AutomatedTestResult> _CachedResults = null;

        /// <summary>
        /// Shared legacy coverage results keyed by test name.
        /// </summary>
        public IReadOnlyDictionary<string, AutomatedTestResult> Results
        {
            get
            {
                return EnsureResults();
            }
        }

        private static IReadOnlyDictionary<string, AutomatedTestResult> EnsureResults()
        {
            lock (_Sync)
            {
                if (_CachedResults != null)
                {
                    return _CachedResults;
                }

                TextWriter standardOutput = Console.Out;
                TextWriter standardError = Console.Error;

                try
                {
                    Console.SetOut(TextWriter.Null);
                    Console.SetError(TextWriter.Null);

                    LegacyCoverageSuite suite = new LegacyCoverageSuite();
                    IReadOnlyList<AutomatedTestResult> results = suite.RunAsync().GetAwaiter().GetResult();
                    Dictionary<string, AutomatedTestResult> cachedResults = new Dictionary<string, AutomatedTestResult>(StringComparer.Ordinal);

                    for (int i = 0; i < results.Count; i++)
                    {
                        cachedResults[results[i].TestName] = results[i];
                    }

                    _CachedResults = cachedResults;
                    return _CachedResults;
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
