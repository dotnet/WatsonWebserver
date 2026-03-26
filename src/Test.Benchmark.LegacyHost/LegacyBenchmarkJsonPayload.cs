namespace Test.Benchmark.LegacyHost
{
    /// <summary>
    /// Deterministic JSON payload used by legacy benchmark scenarios.
    /// </summary>
    internal class LegacyBenchmarkJsonPayload
    {
        /// <summary>
        /// Message name.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Category name.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Sequence value.
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Variable-size content body.
        /// </summary>
        public string Content { get; set; }
    }
}
