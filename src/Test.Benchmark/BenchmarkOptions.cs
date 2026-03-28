namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Benchmark execution options.
    /// </summary>
    internal class BenchmarkOptions
    {
        /// <summary>
        /// Targets to benchmark.
        /// </summary>
        public List<BenchmarkTarget> Targets { get; } = new List<BenchmarkTarget>
        {
            BenchmarkTarget.Watson6,
            BenchmarkTarget.WatsonLite6,
            BenchmarkTarget.Watson7,
            BenchmarkTarget.Kestrel
        };

        /// <summary>
        /// Protocols to benchmark.
        /// </summary>
        public List<BenchmarkProtocol> Protocols { get; } = new List<BenchmarkProtocol>
        {
            BenchmarkProtocol.Http11,
            BenchmarkProtocol.Http2,
            BenchmarkProtocol.Http3
        };

        /// <summary>
        /// Scenarios to benchmark.
        /// </summary>
        public List<BenchmarkScenario> Scenarios { get; } = new List<BenchmarkScenario>
        {
            BenchmarkScenario.Hello,
            BenchmarkScenario.Echo,
            BenchmarkScenario.ChunkedEcho,
            BenchmarkScenario.ChunkedResponse,
            BenchmarkScenario.ServerSentEvents,
            BenchmarkScenario.Json,
            BenchmarkScenario.SerializeJson,
            BenchmarkScenario.JsonEcho
        };

        /// <summary>
        /// Warmup duration in seconds.
        /// </summary>
        public int WarmupSeconds { get; set; } = 2;

        /// <summary>
        /// Measurement duration in seconds.
        /// </summary>
        public int DurationSeconds { get; set; } = 5;

        /// <summary>
        /// Number of repeated measurements per combination.
        /// </summary>
        public int Repetitions { get; set; } = 1;

        /// <summary>
        /// Request concurrency.
        /// </summary>
        public int Concurrency { get; set; } = 32;

        /// <summary>
        /// Payload size for response and echo scenarios.
        /// </summary>
        public int PayloadBytes { get; set; } = 4096;

        /// <summary>
        /// Number of server-sent events per response.
        /// </summary>
        public int ServerSentEventCount { get; set; } = 8;

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Whether to use TLS for HTTP/1.1 runs.
        /// </summary>
        public bool UseTlsForHttp11 { get; set; } = false;

        /// <summary>
        /// Parse options from command line arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Parsed options.</returns>
        public static BenchmarkOptions Parse(string[] args)
        {
            BenchmarkOptions options = new BenchmarkOptions();
            if (args == null || args.Length < 1) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (String.IsNullOrEmpty(argument)) continue;

                if (String.Equals(argument, "--targets", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.Targets.Clear();
                    ParseTargets(args[++i], options.Targets);
                }
                else if (String.Equals(argument, "--protocols", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.Protocols.Clear();
                    ParseProtocols(args[++i], options.Protocols);
                }
                else if (String.Equals(argument, "--scenarios", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.Scenarios.Clear();
                    ParseScenarios(args[++i], options.Scenarios);
                }
                else if (String.Equals(argument, "--warmup-seconds", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.WarmupSeconds = ParseInt(args[++i], 0, 3600, options.WarmupSeconds);
                }
                else if (String.Equals(argument, "--duration-seconds", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.DurationSeconds = ParseInt(args[++i], 1, 3600, options.DurationSeconds);
                }
                else if (String.Equals(argument, "--repetitions", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.Repetitions = ParseInt(args[++i], 1, 100, options.Repetitions);
                }
                else if (String.Equals(argument, "--concurrency", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.Concurrency = ParseInt(args[++i], 1, 4096, options.Concurrency);
                }
                else if (String.Equals(argument, "--payload-bytes", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.PayloadBytes = ParseInt(args[++i], 1, 1024 * 1024, options.PayloadBytes);
                }
                else if (String.Equals(argument, "--sse-events", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.ServerSentEventCount = ParseInt(args[++i], 1, 1024, options.ServerSentEventCount);
                }
                else if (String.Equals(argument, "--timeout-seconds", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.RequestTimeoutSeconds = ParseInt(args[++i], 1, 3600, options.RequestTimeoutSeconds);
                }
                else if (String.Equals(argument, "--use-tls-http1", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                {
                    options.UseTlsForHttp11 = ParseBool(args[++i], options.UseTlsForHttp11);
                }
            }

            return options;
        }

        private static void ParseTargets(string value, List<BenchmarkTarget> targets)
        {
            string[] parts = SplitList(value);
            for (int i = 0; i < parts.Length; i++)
            {
                string current = parts[i];
                if (String.Equals(current, "all", StringComparison.OrdinalIgnoreCase))
                {
                    AddTargetIfMissing(targets, BenchmarkTarget.Watson6);
                    AddTargetIfMissing(targets, BenchmarkTarget.WatsonLite6);
                    AddTargetIfMissing(targets, BenchmarkTarget.Watson7);
                    AddTargetIfMissing(targets, BenchmarkTarget.Kestrel);
                }
                else if (String.Equals(current, "watson", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "watson7", StringComparison.OrdinalIgnoreCase))
                {
                    AddTargetIfMissing(targets, BenchmarkTarget.Watson7);
                }
                else if (String.Equals(current, "watson6", StringComparison.OrdinalIgnoreCase))
                {
                    AddTargetIfMissing(targets, BenchmarkTarget.Watson6);
                }
                else if (String.Equals(current, "watsonlite6", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "watson.lite6", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "watsonlite", StringComparison.OrdinalIgnoreCase))
                {
                    AddTargetIfMissing(targets, BenchmarkTarget.WatsonLite6);
                }
                else if (String.Equals(current, "kestrel", StringComparison.OrdinalIgnoreCase))
                {
                    AddTargetIfMissing(targets, BenchmarkTarget.Kestrel);
                }
            }
        }

        private static void ParseProtocols(string value, List<BenchmarkProtocol> protocols)
        {
            string[] parts = SplitList(value);
            for (int i = 0; i < parts.Length; i++)
            {
                string current = parts[i];
                if (String.Equals(current, "all", StringComparison.OrdinalIgnoreCase))
                {
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http11);
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http2);
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http3);
                }
                else if (String.Equals(current, "http1", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "http11", StringComparison.OrdinalIgnoreCase))
                {
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http11);
                }
                else if (String.Equals(current, "http2", StringComparison.OrdinalIgnoreCase))
                {
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http2);
                }
                else if (String.Equals(current, "http3", StringComparison.OrdinalIgnoreCase))
                {
                    AddProtocolIfMissing(protocols, BenchmarkProtocol.Http3);
                }
            }
        }

        private static void ParseScenarios(string value, List<BenchmarkScenario> scenarios)
        {
            string[] parts = SplitList(value);
            for (int i = 0; i < parts.Length; i++)
            {
                string current = parts[i];
                if (String.Equals(current, "all", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Hello);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Echo);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ChunkedEcho);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ChunkedResponse);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ServerSentEvents);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Json);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.SerializeJson);
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.JsonEcho);
                }
                else if (String.Equals(current, "hello", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Hello);
                }
                else if (String.Equals(current, "echo", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Echo);
                }
                else if (String.Equals(current, "chunkedecho", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "chunked-echo", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ChunkedEcho);
                }
                else if (String.Equals(current, "chunkedresponse", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "chunked-response", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ChunkedResponse);
                }
                else if (String.Equals(current, "sse", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "serversentevents", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.ServerSentEvents);
                }
                else if (String.Equals(current, "json", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.Json);
                }
                else if (String.Equals(current, "serializejson", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "serialize-json", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.SerializeJson);
                }
                else if (String.Equals(current, "jsonecho", StringComparison.OrdinalIgnoreCase) || String.Equals(current, "json-echo", StringComparison.OrdinalIgnoreCase))
                {
                    AddScenarioIfMissing(scenarios, BenchmarkScenario.JsonEcho);
                }
            }
        }

        private static string[] SplitList(string value)
        {
            if (String.IsNullOrEmpty(value)) return Array.Empty<string>();
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static int ParseInt(string value, int minimum, int maximum, int fallback)
        {
            int parsed;
            if (!Int32.TryParse(value, out parsed)) return fallback;
            if (parsed < minimum) return minimum;
            if (parsed > maximum) return maximum;
            return parsed;
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            if (!bool.TryParse(value, out parsed)) return fallback;
            return parsed;
        }

        private static void AddTargetIfMissing(List<BenchmarkTarget> targets, BenchmarkTarget value)
        {
            if (!targets.Contains(value)) targets.Add(value);
        }

        private static void AddProtocolIfMissing(List<BenchmarkProtocol> protocols, BenchmarkProtocol value)
        {
            if (!protocols.Contains(value)) protocols.Add(value);
        }

        private static void AddScenarioIfMissing(List<BenchmarkScenario> scenarios, BenchmarkScenario value)
        {
            if (!scenarios.Contains(value)) scenarios.Add(value);
        }
    }
}
