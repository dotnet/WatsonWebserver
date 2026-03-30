namespace Test.Automated
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Summary for an automated test run.
    /// </summary>
    public class AutomatedRunSummary
    {
        /// <summary>
        /// Full ordered result set.
        /// </summary>
        public IReadOnlyList<AutomatedTestResult> Results { get; set; } = Array.Empty<AutomatedTestResult>();

        /// <summary>
        /// Total number of tests.
        /// </summary>
        public int TotalCount { get; set; } = 0;

        /// <summary>
        /// Total number of passed tests.
        /// </summary>
        public int PassedCount { get; set; } = 0;

        /// <summary>
        /// Total number of failed tests.
        /// </summary>
        public int FailedCount { get; set; } = 0;

        /// <summary>
        /// Total runtime.
        /// </summary>
        public TimeSpan TotalRuntime { get; set; } = TimeSpan.Zero;
    }
}
