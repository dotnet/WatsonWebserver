namespace Test.Benchmark
{
    /// <summary>
    /// Watson.Lite 6.6 benchmark host.
    /// </summary>
    internal sealed class WatsonLite6BenchmarkHost : LegacyProcessBenchmarkHost
    {
        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="protocol">Protocol.</param>
        /// <param name="options">Options.</param>
        public WatsonLite6BenchmarkHost(BenchmarkProtocol protocol, BenchmarkOptions options) : base(BenchmarkTarget.WatsonLite6, options)
        {
        }

        /// <inheritdoc />
        public override string Name
        {
            get
            {
                return "WatsonLite6";
            }
        }
    }
}
