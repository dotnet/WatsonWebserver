namespace Test.Benchmark
{
    /// <summary>
    /// Result from a benchmark run.
    /// </summary>
    internal class BenchmarkResult
    {
        /// <summary>
        /// Combination that was executed.
        /// </summary>
        public BenchmarkCombination Combination { get; set; }

        /// <summary>
        /// Number of repeated measurements aggregated into this result.
        /// </summary>
        public int RepetitionCount { get; set; } = 1;

        /// <summary>
        /// Total successful requests.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// Total failed requests.
        /// </summary>
        public long FailureCount { get; set; }

        /// <summary>
        /// Total bytes read from responses.
        /// </summary>
        public long ResponseBytes { get; set; }

        /// <summary>
        /// Total bytes sent in request bodies.
        /// </summary>
        public long RequestBytes { get; set; }

        /// <summary>
        /// Elapsed measurement duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// First-request latency in milliseconds.
        /// </summary>
        public double HandshakeMs { get; set; }

        /// <summary>
        /// Mean request latency in milliseconds.
        /// </summary>
        public double MeanLatencyMs { get; set; }

        /// <summary>
        /// 50th percentile request latency in milliseconds.
        /// </summary>
        public double P50LatencyMs { get; set; }

        /// <summary>
        /// 95th percentile request latency in milliseconds.
        /// </summary>
        public double P95LatencyMs { get; set; }

        /// <summary>
        /// 99th percentile request latency in milliseconds.
        /// </summary>
        public double P99LatencyMs { get; set; }

        /// <summary>
        /// Throughput in requests per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Throughput in response bytes per second.
        /// </summary>
        public double ResponseBytesPerSecond { get; set; }

        /// <summary>
        /// Throughput in total transferred body bytes per second.
        /// </summary>
        public double TotalBytesPerSecond { get; set; }

        /// <summary>
        /// Managed bytes allocated during measurement.
        /// </summary>
        public long ManagedBytesAllocated { get; set; }
    }
}
