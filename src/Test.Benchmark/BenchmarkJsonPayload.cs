namespace Test.Benchmark
{
    /// <summary>
    /// Deterministic JSON payload used by benchmark scenarios.
    /// </summary>
    internal class BenchmarkJsonPayload
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
