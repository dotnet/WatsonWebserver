namespace Test.Automated
{
    using System;

    /// <summary>
    /// Represents the result of a single automated test.
    /// </summary>
    public class AutomatedTestResult
    {
        /// <summary>
        /// Suite name.
        /// </summary>
        public string SuiteName { get; set; } = String.Empty;

        /// <summary>
        /// Test name.
        /// </summary>
        public string TestName { get; set; } = String.Empty;

        /// <summary>
        /// Indicates whether the test passed.
        /// </summary>
        public bool Passed { get; set; } = false;

        /// <summary>
        /// Test runtime in milliseconds.
        /// </summary>
        public long ElapsedMilliseconds { get; set; } = 0;

        /// <summary>
        /// Failure detail when the test does not pass.
        /// </summary>
        public string ErrorMessage { get; set; } = null;

        /// <summary>
        /// Display name combining suite and test.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return TestName;
            }
        }
    }
}
