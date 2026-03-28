namespace Test.Automated
{
    using System;

    /// <summary>
    /// Provides an optional callback for reporting test completion as results are recorded.
    /// </summary>
    public static class AutomatedTestReporter
    {
        /// <summary>
        /// Optional callback invoked when a test result is recorded.
        /// </summary>
        public static Action<AutomatedTestResult> ResultRecorded { get; set; } = null;
    }
}
