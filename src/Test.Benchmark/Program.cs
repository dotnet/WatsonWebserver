namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Benchmark harness entry point.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Exit code.</returns>
        private static async Task<int> Main(string[] args)
        {
            BenchmarkOptions options = BenchmarkOptions.Parse(args);
            BenchmarkRunner runner = new BenchmarkRunner(options);
            List<BenchmarkCombination> combinations = BuildCombinations(options);
            List<BenchmarkResult> results = new List<BenchmarkResult>();

            Console.WriteLine("WatsonWebserver Benchmark Harness");
            Console.WriteLine("Runtime: " + RuntimeInformation.FrameworkDescription);
            Console.WriteLine("OS: " + RuntimeInformation.OSDescription);
            Console.WriteLine("Warmup: " + options.WarmupSeconds.ToString() + "s  Duration: " + options.DurationSeconds.ToString() + "s  Concurrency: " + options.Concurrency.ToString() + "  Repetitions: " + options.Repetitions.ToString());
            Console.WriteLine("PayloadBytes: " + options.PayloadBytes.ToString() + "  SseEvents: " + options.ServerSentEventCount.ToString());
            Console.WriteLine();
            WriteLiveHeader();

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellationTokenSource.Cancel();
                };

                for (int i = 0; i < combinations.Count; i++)
                {
                    BenchmarkCombination combination = combinations[i];

                    try
                    {
                        BenchmarkResult result = await RunCombinationAsync(runner, combination, options, cancellationTokenSource.Token).ConfigureAwait(false);
                        results.Add(result);
                        WriteLiveResult(result, "PASS", null);
                    }
                    catch (OperationCanceledException)
                    {
                        WriteLiveResult(CreateFailedResult(combination), "CANCEL", "Canceled");
                        break;
                    }
                    catch (Exception exception)
                    {
                        WriteLiveResult(CreateFailedResult(combination), "FAIL", exception.Message);
                    }
                }
            }

            Console.WriteLine();
            WriteSummary(results);
            WriteComparisonTables(results);
            return 0;
        }

        private static List<BenchmarkCombination> BuildCombinations(BenchmarkOptions options)
        {
            List<BenchmarkCombination> results = new List<BenchmarkCombination>();

            for (int targetIndex = 0; targetIndex < options.Targets.Count; targetIndex++)
            {
                BenchmarkTarget target = options.Targets[targetIndex];

                for (int protocolIndex = 0; protocolIndex < options.Protocols.Count; protocolIndex++)
                {
                    BenchmarkProtocol protocol = options.Protocols[protocolIndex];
                    if (!BenchmarkHostFactory.Supports(target, protocol))
                    {
                        Console.WriteLine("Skipping unsupported combination: " + target.ToString() + " / " + protocol.ToString());
                        continue;
                    }

                    for (int scenarioIndex = 0; scenarioIndex < options.Scenarios.Count; scenarioIndex++)
                    {
                        BenchmarkScenario scenario = options.Scenarios[scenarioIndex];
                        if (!BenchmarkHostFactory.Supports(target, protocol, scenario))
                        {
                            Console.WriteLine("Skipping unsupported combination: " + target.ToString() + " / " + protocol.ToString() + " / " + scenario.ToString());
                            continue;
                        }

                        BenchmarkCombination combination = new BenchmarkCombination();
                        combination.Target = target;
                        combination.Protocol = protocol;
                        combination.Scenario = scenario;
                        results.Add(combination);
                    }
                }
            }

            return results;
        }

        private static async Task<BenchmarkResult> RunCombinationAsync(BenchmarkRunner runner, BenchmarkCombination combination, BenchmarkOptions options, CancellationToken token)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (combination == null) throw new ArgumentNullException(nameof(combination));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Repetitions < 1) throw new ArgumentOutOfRangeException(nameof(options.Repetitions));

            List<BenchmarkResult> samples = new List<BenchmarkResult>();
            using (IBenchmarkHost host = BenchmarkHostFactory.Create(combination.Target, combination.Protocol, options))
            {
                await host.StartAsync(token).ConfigureAwait(false);
                try
                {
                    for (int i = 0; i < options.Repetitions; i++)
                    {
                        BenchmarkResult sample = await runner.RunAsync(host, combination, token).ConfigureAwait(false);
                        samples.Add(sample);
                    }
                }
                finally
                {
                    await host.StopAsync(token).ConfigureAwait(false);
                }
            }

            return BenchmarkResultAggregator.AggregateMedian(samples);
        }

        private static void WriteLiveHeader()
        {
            string header =
                PadRight("Target", 14)
                + PadRight("Protocol", 10)
                + PadRight("Scenario", 18)
                + PadRight("Status", 8)
                + PadLeft("Req/s", 12)
                + PadLeft("P50", 10)
                + PadLeft("P99", 10)
                + PadLeft("Total/s", 14)
                + PadLeft("Success", 10)
                + PadLeft("Failure", 10)
                + PadLeft("Alloc", 12)
                + "  Notes";

            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));
        }

        private static void WriteLiveResult(BenchmarkResult result, string status, string notes)
        {
            BenchmarkResult currentResult = result ?? throw new ArgumentNullException(nameof(result));
            string currentStatus = status ?? String.Empty;
            string statusCell = PadRight(currentStatus, 8);
            string notesText = notes ?? String.Empty;

            Console.Write(
                PadRight(GetTargetText(currentResult), 14)
                + PadRight(GetProtocolText(currentResult), 10)
                + PadRight(GetScenarioText(currentResult), 18));

            WriteStatus(statusCell, currentStatus);

            Console.WriteLine(
                PadLeft(GetRequestsPerSecondText(currentResult), 12)
                + PadLeft(GetP50Text(currentResult), 10)
                + PadLeft(GetP99Text(currentResult), 10)
                + PadLeft(GetTotalThroughputText(currentResult), 14)
                + PadLeft(GetSuccessText(currentResult), 10)
                + PadLeft(GetFailureText(currentResult), 10)
                + PadLeft(FormatBytes(currentResult.ManagedBytesAllocated), 12)
                + "  " + notesText);
        }

        private static void WriteStatus(string statusCell, string status)
        {
            if (Console.IsOutputRedirected)
            {
                Console.Write(statusCell);
                return;
            }

            ConsoleColor originalColor = Console.ForegroundColor;

            if (StringComparer.OrdinalIgnoreCase.Equals(status, "PASS"))
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(status, "FAIL"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(status, "CANCEL"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }

            Console.Write(statusCell);
            Console.ForegroundColor = originalColor;
        }

        private static BenchmarkResult CreateFailedResult(BenchmarkCombination combination)
        {
            BenchmarkResult result = new BenchmarkResult();
            result.Combination = combination;
            return result;
        }

        private static void WriteSummary(List<BenchmarkResult> results)
        {
            if (results == null || results.Count < 1)
            {
                Console.WriteLine("No benchmark results collected.");
                return;
            }

            Console.WriteLine("Summary");
            int targetWidth = GetColumnWidth(results, GetTargetText, "Target", 2);
            int protocolWidth = GetColumnWidth(results, GetProtocolText, "Protocol", 2);
            int scenarioWidth = GetColumnWidth(results, GetScenarioText, "Scenario", 2);
            int rpsWidth = GetColumnWidth(results, GetRequestsPerSecondText, "Req/s", 2);
            int p50Width = GetColumnWidth(results, GetP50Text, "P50", 2);
            int p95Width = GetColumnWidth(results, GetP95Text, "P95", 2);
            int p99Width = GetColumnWidth(results, GetP99Text, "P99", 2);
            int responseWidth = GetColumnWidth(results, GetResponseThroughputText, "Response/s", 2);
            int totalWidth = GetColumnWidth(results, GetTotalThroughputText, "Total/s", 2);
            int successWidth = GetColumnWidth(results, GetSuccessText, "Success", 2);
            int failureWidth = GetColumnWidth(results, GetFailureText, "Failure", 2);

            string header =
                PadRight("Target", targetWidth)
                + PadRight("Protocol", protocolWidth)
                + PadRight("Scenario", scenarioWidth)
                + PadLeft("Req/s", rpsWidth)
                + PadLeft("P50", p50Width)
                + PadLeft("P95", p95Width)
                + PadLeft("P99", p99Width)
                + PadLeft("Response/s", responseWidth)
                + PadLeft("Total/s", totalWidth)
                + PadLeft("Success", successWidth)
                + PadLeft("Failure", failureWidth);

            Console.WriteLine(header);
            Console.WriteLine(new string('-', header.Length));

            for (int i = 0; i < results.Count; i++)
            {
                BenchmarkResult result = results[i];
                Console.WriteLine(
                    PadRight(GetTargetText(result), targetWidth)
                    + PadRight(GetProtocolText(result), protocolWidth)
                    + PadRight(GetScenarioText(result), scenarioWidth)
                    + PadLeft(GetRequestsPerSecondText(result), rpsWidth)
                    + PadLeft(GetP50Text(result), p50Width)
                    + PadLeft(GetP95Text(result), p95Width)
                    + PadLeft(GetP99Text(result), p99Width)
                    + PadLeft(GetResponseThroughputText(result), responseWidth)
                    + PadLeft(GetTotalThroughputText(result), totalWidth)
                    + PadLeft(GetSuccessText(result), successWidth)
                    + PadLeft(GetFailureText(result), failureWidth));
            }
        }

        private static void WriteComparisonTables(List<BenchmarkResult> results)
        {
            if (results == null || results.Count < 1) return;

            Console.WriteLine();
            Console.WriteLine("Comparison Tables");

            List<BenchmarkProtocol> protocols = GetOrderedProtocols(results);
            for (int protocolIndex = 0; protocolIndex < protocols.Count; protocolIndex++)
            {
                BenchmarkProtocol protocol = protocols[protocolIndex];
                List<BenchmarkResult> protocolResults = results.FindAll(result => result.Combination.Protocol == protocol);
                if (protocolResults.Count < 1) continue;

                List<BenchmarkTarget> targets = GetOrderedTargets(protocolResults);
                List<BenchmarkScenario> scenarios = GetOrderedScenarios(protocolResults);
                int scenarioWidth = Math.Max("Scenario".Length, GetMaxScenarioWidth(scenarios)) + 2;
                int targetWidth = Math.Max(20, GetMaxTargetWidth(targets, protocolResults)) + 2;

                Console.WriteLine("  --- " + GetProtocolText(protocol) + " ---");
                Console.WriteLine();

                Console.Write(PadRight("Scenario", scenarioWidth));
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    Console.Write("| ");
                    Console.Write(PadRight(GetTargetText(targets[targetIndex]), targetWidth));
                }
                Console.WriteLine();

                Console.Write(new string('-', scenarioWidth));
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    Console.Write("+-");
                    Console.Write(new string('-', targetWidth));
                }
                Console.WriteLine();

                for (int scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
                {
                    BenchmarkScenario scenario = scenarios[scenarioIndex];
                    Console.Write(PadRight(GetScenarioText(scenario), scenarioWidth));

                    for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                    {
                        BenchmarkResult result = FindResult(protocolResults, targets[targetIndex], scenario);
                        Console.Write("| ");
                        Console.Write(PadRight(FormatComparisonCell(result), targetWidth));
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
        }

        private static int GetColumnWidth(List<BenchmarkResult> results, Func<BenchmarkResult, string> selector, string header, int padding)
        {
            int width = header.Length + padding;
            for (int i = 0; i < results.Count; i++)
            {
                string value = selector(results[i]) ?? String.Empty;
                if ((value.Length + padding) > width)
                {
                    width = value.Length + padding;
                }
            }

            return width;
        }

        private static string GetTargetText(BenchmarkResult result)
        {
            return result.Combination.Target.ToString();
        }

        private static string GetTargetText(BenchmarkTarget target)
        {
            return target.ToString();
        }

        private static string GetProtocolText(BenchmarkResult result)
        {
            return result.Combination.Protocol.ToString();
        }

        private static string GetProtocolText(BenchmarkProtocol protocol)
        {
            return protocol.ToString();
        }

        private static string GetScenarioText(BenchmarkResult result)
        {
            return GetScenarioText(result.Combination.Scenario);
        }

        private static string GetScenarioText(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.Hello) return "Hello";
            if (scenario == BenchmarkScenario.Echo) return "Echo";
            if (scenario == BenchmarkScenario.ChunkedEcho) return "ChunkedEcho";
            if (scenario == BenchmarkScenario.ChunkedResponse) return "ChunkedResponse";
            if (scenario == BenchmarkScenario.ServerSentEvents) return "SSE";
            if (scenario == BenchmarkScenario.Json) return "Json";
            if (scenario == BenchmarkScenario.SerializeJson) return "SerializeJson";
            if (scenario == BenchmarkScenario.JsonEcho) return "JsonEcho";
            if (scenario == BenchmarkScenario.WebSocketEcho) return "WebSocketEcho";
            if (scenario == BenchmarkScenario.WebSocketConnectClose) return "WebSocketConnectClose";
            if (scenario == BenchmarkScenario.WebSocketClientText) return "WebSocketClientText";
            if (scenario == BenchmarkScenario.WebSocketServerText) return "WebSocketServerText";
            return scenario.ToString();
        }

        private static string GetRequestsPerSecondText(BenchmarkResult result)
        {
            return result.RequestsPerSecond.ToString("N2");
        }

        private static string GetP50Text(BenchmarkResult result)
        {
            return FormatMilliseconds(result.P50LatencyMs);
        }

        private static string GetP95Text(BenchmarkResult result)
        {
            return FormatMilliseconds(result.P95LatencyMs);
        }

        private static string GetP99Text(BenchmarkResult result)
        {
            return FormatMilliseconds(result.P99LatencyMs);
        }

        private static string GetResponseThroughputText(BenchmarkResult result)
        {
            return FormatBytesPerSecond(result.ResponseBytesPerSecond);
        }

        private static string GetTotalThroughputText(BenchmarkResult result)
        {
            return FormatBytesPerSecond(result.TotalBytesPerSecond);
        }

        private static string GetSuccessText(BenchmarkResult result)
        {
            return result.SuccessCount.ToString("N0");
        }

        private static string GetFailureText(BenchmarkResult result)
        {
            return result.FailureCount.ToString("N0");
        }

        private static string FormatMilliseconds(double milliseconds)
        {
            return milliseconds.ToString("N2") + " ms";
        }

        private static string FormatBytes(long bytes)
        {
            return FormatByteValue(bytes) + "B";
        }

        private static string FormatBytesPerSecond(double bytesPerSecond)
        {
            return FormatByteValue(bytesPerSecond) + "B/s";
        }

        private static string FormatByteValue(double bytes)
        {
            string[] suffixes = new string[] { "", "Ki", "Mi", "Gi", "Ti" };
            double currentValue = bytes < 0 ? 0 : bytes;
            int suffixIndex = 0;

            while (currentValue >= 1024 && suffixIndex < (suffixes.Length - 1))
            {
                currentValue /= 1024;
                suffixIndex++;
            }

            return currentValue.ToString("N2") + " " + suffixes[suffixIndex];
        }

        private static string PadRight(string value, int width)
        {
            string currentValue = value ?? String.Empty;
            return currentValue.PadRight(width);
        }

        private static string PadLeft(string value, int width)
        {
            string currentValue = value ?? String.Empty;
            return currentValue.PadLeft(width);
        }

        private static List<BenchmarkProtocol> GetOrderedProtocols(List<BenchmarkResult> results)
        {
            List<BenchmarkProtocol> protocols = new List<BenchmarkProtocol>();
            BenchmarkProtocol[] preferredOrder = new[] { BenchmarkProtocol.Http11, BenchmarkProtocol.Http2, BenchmarkProtocol.Http3 };

            for (int i = 0; i < preferredOrder.Length; i++)
            {
                BenchmarkProtocol protocol = preferredOrder[i];
                if (results.Exists(result => result.Combination.Protocol == protocol))
                {
                    protocols.Add(protocol);
                }
            }

            return protocols;
        }

        private static List<BenchmarkTarget> GetOrderedTargets(List<BenchmarkResult> results)
        {
            List<BenchmarkTarget> targets = new List<BenchmarkTarget>();
            BenchmarkTarget[] preferredOrder = new[] { BenchmarkTarget.Watson6, BenchmarkTarget.WatsonLite6, BenchmarkTarget.Watson7, BenchmarkTarget.Kestrel };

            for (int i = 0; i < preferredOrder.Length; i++)
            {
                BenchmarkTarget target = preferredOrder[i];
                if (results.Exists(result => result.Combination.Target == target))
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        private static List<BenchmarkScenario> GetOrderedScenarios(List<BenchmarkResult> results)
        {
            List<BenchmarkScenario> scenarios = new List<BenchmarkScenario>();
            BenchmarkScenario[] preferredOrder = new[]
            {
                BenchmarkScenario.Hello,
                BenchmarkScenario.Echo,
                BenchmarkScenario.ChunkedEcho,
                BenchmarkScenario.ChunkedResponse,
                BenchmarkScenario.ServerSentEvents,
                BenchmarkScenario.Json,
                BenchmarkScenario.SerializeJson,
                BenchmarkScenario.JsonEcho,
                BenchmarkScenario.WebSocketEcho,
                BenchmarkScenario.WebSocketConnectClose,
                BenchmarkScenario.WebSocketClientText,
                BenchmarkScenario.WebSocketServerText
            };

            for (int i = 0; i < preferredOrder.Length; i++)
            {
                BenchmarkScenario scenario = preferredOrder[i];
                if (results.Exists(result => result.Combination.Scenario == scenario))
                {
                    scenarios.Add(scenario);
                }
            }

            return scenarios;
        }

        private static int GetMaxScenarioWidth(List<BenchmarkScenario> scenarios)
        {
            int width = 0;
            for (int i = 0; i < scenarios.Count; i++)
            {
                string text = GetScenarioText(scenarios[i]);
                if (text.Length > width)
                {
                    width = text.Length;
                }
            }

            return width;
        }

        private static int GetMaxTargetWidth(List<BenchmarkTarget> targets, List<BenchmarkResult> results)
        {
            int width = 0;
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                int currentWidth = GetTargetText(targets[targetIndex]).Length;

                for (int resultIndex = 0; resultIndex < results.Count; resultIndex++)
                {
                    BenchmarkResult result = results[resultIndex];
                    if (result.Combination.Target != targets[targetIndex]) continue;

                    string cell = FormatComparisonCell(result);
                    if (cell.Length > currentWidth)
                    {
                        currentWidth = cell.Length;
                    }
                }

                if (currentWidth > width)
                {
                    width = currentWidth;
                }
            }

            return width;
        }

        private static BenchmarkResult FindResult(List<BenchmarkResult> results, BenchmarkTarget target, BenchmarkScenario scenario)
        {
            for (int i = 0; i < results.Count; i++)
            {
                BenchmarkResult result = results[i];
                if (result.Combination.Target == target && result.Combination.Scenario == scenario)
                {
                    return result;
                }
            }

            return null;
        }

        private static string FormatComparisonCell(BenchmarkResult result)
        {
            if (result == null) return "N/A";

            string cell = result.RequestsPerSecond.ToString("N0") + " r/s, " + result.P50LatencyMs.ToString("N2") + " ms";
            if (result.FailureCount > 0)
            {
                cell += ", F:" + result.FailureCount.ToString("N0");
            }

            return cell;
        }
    }
}
