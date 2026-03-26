namespace Test.CurlInterop
{
    /// <summary>
    /// Result from invoking curl.
    /// </summary>
    internal class CurlCommandResult
    {
        /// <summary>
        /// Process exit code.
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Standard output.
        /// </summary>
        public string StandardOutput { get; set; } = string.Empty;

        /// <summary>
        /// Standard error.
        /// </summary>
        public string StandardError { get; set; } = string.Empty;
    }
}
