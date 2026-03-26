namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Aggregates repeated benchmark samples into a median result.
    /// </summary>
    internal static class BenchmarkResultAggregator
    {
        /// <summary>
        /// Aggregate samples using median values.
        /// </summary>
        /// <param name="samples">Samples.</param>
        /// <returns>Aggregated result.</returns>
        public static BenchmarkResult AggregateMedian(IReadOnlyList<BenchmarkResult> samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (samples.Count < 1) throw new ArgumentOutOfRangeException(nameof(samples));

            BenchmarkResult aggregated = new BenchmarkResult();
            aggregated.Combination = samples[0].Combination;
            aggregated.RepetitionCount = samples.Count;
            aggregated.SuccessCount = GetMedian(samples.Select(i => i.SuccessCount));
            aggregated.FailureCount = GetMedian(samples.Select(i => i.FailureCount));
            aggregated.ResponseBytes = GetMedian(samples.Select(i => i.ResponseBytes));
            aggregated.RequestBytes = GetMedian(samples.Select(i => i.RequestBytes));
            aggregated.DurationMs = GetMedian(samples.Select(i => i.DurationMs));
            aggregated.HandshakeMs = GetMedian(samples.Select(i => i.HandshakeMs));
            aggregated.MeanLatencyMs = GetMedian(samples.Select(i => i.MeanLatencyMs));
            aggregated.P50LatencyMs = GetMedian(samples.Select(i => i.P50LatencyMs));
            aggregated.P95LatencyMs = GetMedian(samples.Select(i => i.P95LatencyMs));
            aggregated.P99LatencyMs = GetMedian(samples.Select(i => i.P99LatencyMs));
            aggregated.RequestsPerSecond = GetMedian(samples.Select(i => i.RequestsPerSecond));
            aggregated.ResponseBytesPerSecond = GetMedian(samples.Select(i => i.ResponseBytesPerSecond));
            aggregated.TotalBytesPerSecond = GetMedian(samples.Select(i => i.TotalBytesPerSecond));
            aggregated.ManagedBytesAllocated = GetMedian(samples.Select(i => i.ManagedBytesAllocated));
            return aggregated;
        }

        private static long GetMedian(IEnumerable<long> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            long[] ordered = values.OrderBy(i => i).ToArray();
            if (ordered.Length < 1) return 0;

            int middleIndex = ordered.Length / 2;
            if ((ordered.Length % 2) == 1)
            {
                return ordered[middleIndex];
            }

            return (ordered[middleIndex - 1] + ordered[middleIndex]) / 2;
        }

        private static double GetMedian(IEnumerable<double> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            double[] ordered = values.OrderBy(i => i).ToArray();
            if (ordered.Length < 1) return 0;

            int middleIndex = ordered.Length / 2;
            if ((ordered.Length % 2) == 1)
            {
                return ordered[middleIndex];
            }

            return (ordered[middleIndex - 1] + ordered[middleIndex]) / 2.0;
        }
    }
}
