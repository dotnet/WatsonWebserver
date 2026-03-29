namespace Test.Benchmark
{
    using System;

    /// <summary>
    /// Creates benchmark hosts for requested combinations.
    /// </summary>
    internal static class BenchmarkHostFactory
    {
        /// <summary>
        /// Determine whether a target supports a protocol.
        /// </summary>
        /// <param name="target">Benchmark target.</param>
        /// <param name="protocol">Benchmark protocol.</param>
        /// <returns>True if supported.</returns>
        public static bool Supports(BenchmarkTarget target, BenchmarkProtocol protocol)
        {
            if ((target == BenchmarkTarget.Watson6 || target == BenchmarkTarget.WatsonLite6) && protocol != BenchmarkProtocol.Http11)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determine whether a target supports a protocol/scenario combination.
        /// </summary>
        /// <param name="target">Benchmark target.</param>
        /// <param name="protocol">Benchmark protocol.</param>
        /// <param name="scenario">Benchmark scenario.</param>
        /// <returns>True if supported.</returns>
        public static bool Supports(BenchmarkTarget target, BenchmarkProtocol protocol, BenchmarkScenario scenario)
        {
            if (!Supports(target, protocol)) return false;
            if (scenario == BenchmarkScenario.WebSocketEcho
                || scenario == BenchmarkScenario.WebSocketConnectClose
                || scenario == BenchmarkScenario.WebSocketClientText
                || scenario == BenchmarkScenario.WebSocketServerText)
            {
                return target == BenchmarkTarget.Watson7 && protocol == BenchmarkProtocol.Http11;
            }

            if ((scenario == BenchmarkScenario.ChunkedEcho || scenario == BenchmarkScenario.ChunkedResponse) && protocol != BenchmarkProtocol.Http11) return false;
            return true;
        }

        /// <summary>
        /// Create a host for a benchmark combination.
        /// </summary>
        /// <param name="target">Benchmark target.</param>
        /// <param name="protocol">Benchmark protocol.</param>
        /// <param name="options">Benchmark options.</param>
        /// <returns>Host.</returns>
        public static IBenchmarkHost Create(BenchmarkTarget target, BenchmarkProtocol protocol, BenchmarkOptions options)
        {
            if (!Supports(target, protocol)) throw new NotSupportedException("Unsupported benchmark combination.");
            if (options == null) throw new ArgumentNullException(nameof(options));

            if (target == BenchmarkTarget.Watson6) return new Watson6BenchmarkHost(protocol, options);
            if (target == BenchmarkTarget.WatsonLite6) return new WatsonLite6BenchmarkHost(protocol, options);
            if (target == BenchmarkTarget.Watson7) return new WatsonBenchmarkHost(protocol, options);
            return new KestrelBenchmarkHost(protocol, options);
        }
    }
}
