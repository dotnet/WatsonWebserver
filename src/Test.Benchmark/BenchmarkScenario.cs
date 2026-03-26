namespace Test.Benchmark
{
    /// <summary>
    /// Workload shape for benchmark execution.
    /// </summary>
    internal enum BenchmarkScenario
    {
        Hello,
        Echo,
        ChunkedEcho,
        ChunkedResponse,
        ServerSentEvents,
        Json,
        SerializeJson,
        JsonEcho
    }
}
