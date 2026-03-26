namespace Test.Benchmark
{
    /// <summary>
    /// Watson 6.6 benchmark host.
    /// </summary>
    internal sealed class Watson6BenchmarkHost : LegacyProcessBenchmarkHost
    {
        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="protocol">Protocol.</param>
        /// <param name="options">Options.</param>
        public Watson6BenchmarkHost(BenchmarkProtocol protocol, BenchmarkOptions options) : base(BenchmarkTarget.Watson6, options)
        {
        }

        /// <inheritdoc />
        public override string Name
        {
            get
            {
                return "Watson6";
            }
        }
    }
}
