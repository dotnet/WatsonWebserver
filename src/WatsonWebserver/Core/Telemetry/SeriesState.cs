namespace WatsonWebserver.Core.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Mutable aggregation state for a single Prometheus time series (one instrument plus one tag set).
    /// Used internally by <see cref="PrometheusScrapeCollector"/>.
    /// </summary>
    internal sealed class SeriesState
    {
        /// <summary>
        /// Instrument (dotted) name.
        /// </summary>
        internal string InstrumentName;

        /// <summary>
        /// Aggregation kind: counter, updown, observable, gauge, or histogram.
        /// </summary>
        internal string Kind;

        /// <summary>
        /// UCUM unit string, or null.
        /// </summary>
        internal string Unit;

        /// <summary>
        /// Tag set for this series.
        /// </summary>
        internal KeyValuePair<string, object>[] Tags;

        /// <summary>
        /// Running value for counter, updown, observable, and gauge kinds.
        /// </summary>
        internal double Value;

        /// <summary>
        /// Histogram observation count.
        /// </summary>
        internal long Count;

        /// <summary>
        /// Histogram observation sum.
        /// </summary>
        internal double Sum;

        /// <summary>
        /// Histogram bucket upper bounds.
        /// </summary>
        internal double[] BucketBounds;

        /// <summary>
        /// Per-bucket observation counts; length is BucketBounds.Length + 1 (the last is the overflow bucket).
        /// </summary>
        internal long[] BucketCounts;
    }
}
