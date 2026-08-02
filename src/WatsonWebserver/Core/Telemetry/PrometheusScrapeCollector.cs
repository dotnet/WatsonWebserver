namespace WatsonWebserver.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// In-process aggregator that listens to a single meter by name and renders the current metric
    /// state as Prometheus exposition text. Used by the optional scrape endpoint, which is served on the
    /// main Watson listener so no additional TCP port is opened.
    /// </summary>
    /// <remarks>
    /// This aggregator is intentionally simple. Histogram bucket boundaries use a fixed default set per
    /// unit; a full-featured deployment should prefer an external OpenTelemetry collector and treat this
    /// endpoint as a zero-infrastructure convenience.
    /// </remarks>
    public sealed class PrometheusScrapeCollector : IDisposable
    {
        #region Private-Members

        private static readonly double[] _SecondsBuckets =
            { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0 };
        private static readonly double[] _BytesBuckets =
            { 100, 1000, 10000, 100000, 1000000, 10000000 };

        private readonly string _MeterName;
        private readonly MeterListener _Listener;
        private readonly object _Lock = new object();
        private readonly Dictionary<string, SeriesState> _Series = new Dictionary<string, SeriesState>();
        private readonly Dictionary<string, string> _Kinds = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _Units = new Dictionary<string, string>();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the collector and begin listening to the supplied meter.
        /// </summary>
        /// <param name="meterName">Meter name to observe.</param>
        public PrometheusScrapeCollector(string meterName)
        {
            if (String.IsNullOrEmpty(meterName)) throw new ArgumentNullException(nameof(meterName));
            _MeterName = meterName;

            _Listener = new MeterListener();
            _Listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == _MeterName)
                {
                    _Kinds[instrument.Name] = ResolveKind(instrument);
                    _Units[instrument.Name] = instrument.Unit;
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _Listener.SetMeasurementEventCallback<long>(OnMeasurementLong);
            _Listener.SetMeasurementEventCallback<double>(OnMeasurementDouble);
            _Listener.Start();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Render the current metric state as Prometheus exposition text.
        /// </summary>
        /// <returns>Prometheus exposition document.</returns>
        public string Render()
        {
            _Listener.RecordObservableInstruments();

            StringBuilder sb = new StringBuilder();
            HashSet<string> headerEmitted = new HashSet<string>();

            lock (_Lock)
            {
                foreach (KeyValuePair<string, SeriesState> entry in _Series)
                {
                    SeriesState series = entry.Value;
                    string family = BuildFamilyName(series.InstrumentName, series.Kind, series.Unit);

                    if (!headerEmitted.Contains(family))
                    {
                        headerEmitted.Add(family);
                        sb.Append("# TYPE ").Append(family).Append(' ').Append(PromType(series.Kind)).Append('\n');
                    }

                    if (series.Kind == "histogram")
                    {
                        RenderHistogram(sb, family, series);
                    }
                    else
                    {
                        sb.Append(family).Append(FormatLabels(series.Tags)).Append(' ')
                          .Append(FormatValue(series.Value)).Append('\n');
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Dispose of the underlying listener.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            try { _Listener.Dispose(); } catch (Exception) { }
        }

        #endregion

        #region Private-Methods

        private void OnMeasurementLong(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
        {
            Accumulate(instrument, measurement, tags);
        }

        private void OnMeasurementDouble(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
        {
            Accumulate(instrument, measurement, tags);
        }

        private void Accumulate(Instrument instrument, double measurement, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            string kind = _Kinds.TryGetValue(instrument.Name, out string k) ? k : "gauge";
            string unit = _Units.TryGetValue(instrument.Name, out string u) ? u : null;
            KeyValuePair<string, object>[] tagArray = tags.ToArray();
            string tagKey = BuildTagKey(tagArray);
            string seriesKey = instrument.Name + "|" + tagKey;

            lock (_Lock)
            {
                if (!_Series.TryGetValue(seriesKey, out SeriesState series))
                {
                    series = new SeriesState
                    {
                        InstrumentName = instrument.Name,
                        Kind = kind,
                        Unit = unit,
                        Tags = tagArray
                    };

                    if (kind == "histogram")
                    {
                        series.BucketBounds = SelectBuckets(unit);
                        series.BucketCounts = new long[series.BucketBounds.Length + 1];
                    }

                    _Series[seriesKey] = series;
                }

                switch (kind)
                {
                    case "counter":
                    case "updown":
                        series.Value += measurement;
                        break;
                    case "observable":
                    case "gauge":
                        series.Value = measurement;
                        break;
                    case "histogram":
                        series.Count += 1;
                        series.Sum += measurement;
                        int index = series.BucketBounds.Length;
                        for (int i = 0; i < series.BucketBounds.Length; i++)
                        {
                            if (measurement <= series.BucketBounds[i])
                            {
                                index = i;
                                break;
                            }
                        }
                        series.BucketCounts[index] += 1;
                        break;
                }
            }
        }

        private void RenderHistogram(StringBuilder sb, string family, SeriesState series)
        {
            long cumulative = 0;
            for (int i = 0; i < series.BucketBounds.Length; i++)
            {
                cumulative += series.BucketCounts[i];
                sb.Append(family).Append("_bucket")
                  .Append(FormatLabels(series.Tags, "le", series.BucketBounds[i].ToString(CultureInfo.InvariantCulture)))
                  .Append(' ').Append(cumulative).Append('\n');
            }

            cumulative += series.BucketCounts[series.BucketBounds.Length];
            sb.Append(family).Append("_bucket")
              .Append(FormatLabels(series.Tags, "le", "+Inf"))
              .Append(' ').Append(cumulative).Append('\n');

            sb.Append(family).Append("_sum").Append(FormatLabels(series.Tags)).Append(' ')
              .Append(FormatValue(series.Sum)).Append('\n');
            sb.Append(family).Append("_count").Append(FormatLabels(series.Tags)).Append(' ')
              .Append(series.Count).Append('\n');
        }

        private static string ResolveKind(Instrument instrument)
        {
            string typeName = instrument.GetType().Name;
            if (typeName.StartsWith("ObservableCounter")) return "observable";
            if (typeName.StartsWith("ObservableUpDownCounter")) return "gauge";
            if (typeName.StartsWith("ObservableGauge")) return "gauge";
            if (typeName.StartsWith("UpDownCounter")) return "updown";
            if (typeName.StartsWith("Counter")) return "counter";
            if (typeName.StartsWith("Histogram")) return "histogram";
            return "gauge";
        }

        private static string PromType(string kind)
        {
            switch (kind)
            {
                case "counter":
                case "observable":
                    return "counter";
                case "histogram":
                    return "histogram";
                default:
                    return "gauge";
            }
        }

        private static double[] SelectBuckets(string unit)
        {
            if (unit == "By") return _BytesBuckets;
            return _SecondsBuckets;
        }

        private static string BuildFamilyName(string instrumentName, string kind, string unit)
        {
            string baseName = Sanitize(instrumentName);
            string unitSuffix = UnitSuffix(unit);
            if (!String.IsNullOrEmpty(unitSuffix)) baseName += unitSuffix;
            if (kind == "counter" || kind == "observable") baseName += "_total";
            return baseName;
        }

        private static string UnitSuffix(string unit)
        {
            if (unit == "s") return "_seconds";
            if (unit == "By") return "_bytes";
            return "";
        }

        private static string Sanitize(string name)
        {
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == ':')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private static string BuildTagKey(KeyValuePair<string, object>[] tags)
        {
            if (tags == null || tags.Length == 0) return "";
            List<string> parts = new List<string>(tags.Length);
            foreach (KeyValuePair<string, object> tag in tags)
            {
                parts.Add(tag.Key + "=" + (tag.Value != null ? tag.Value.ToString() : ""));
            }
            parts.Sort(StringComparer.Ordinal);
            return String.Join(",", parts);
        }

        private static string FormatLabels(KeyValuePair<string, object>[] tags, string extraKey = null, string extraValue = null)
        {
            if ((tags == null || tags.Length == 0) && extraKey == null) return "";

            StringBuilder sb = new StringBuilder();
            sb.Append('{');
            bool first = true;

            if (tags != null)
            {
                foreach (KeyValuePair<string, object> tag in tags)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Sanitize(tag.Key)).Append("=\"").Append(EscapeLabel(tag.Value != null ? tag.Value.ToString() : "")).Append('"');
                }
            }

            if (extraKey != null)
            {
                if (!first) sb.Append(',');
                sb.Append(Sanitize(extraKey)).Append("=\"").Append(EscapeLabel(extraValue)).Append('"');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string EscapeLabel(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private static string FormatValue(double value)
        {
            return value.ToString("G", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
