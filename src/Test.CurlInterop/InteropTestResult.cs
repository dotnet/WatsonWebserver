namespace Test.CurlInterop
{
    /// <summary>
    /// Result from one curl interop test.
    /// </summary>
    internal class InteropTestResult
    {
        /// <summary>
        /// Test name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the test passed.
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Whether the test was skipped.
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Detail string.
        /// </summary>
        public string Detail { get; set; } = string.Empty;
    }
}
