namespace Test.Benchmark
{
    /// <summary>
    /// Combination of server, protocol, and scenario.
    /// </summary>
    internal class BenchmarkCombination
    {
        /// <summary>
        /// Benchmark target.
        /// </summary>
        public BenchmarkTarget Target { get; set; }

        /// <summary>
        /// Benchmark protocol.
        /// </summary>
        public BenchmarkProtocol Protocol { get; set; }

        /// <summary>
        /// Benchmark scenario.
        /// </summary>
        public BenchmarkScenario Scenario { get; set; }
    }
}
