namespace Test.Benchmark
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Host abstraction for benchmark targets.
    /// </summary>
    internal interface IBenchmarkHost : IDisposable
    {
        /// <summary>
        /// Host display name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Protocol exposed by the host.
        /// </summary>
        BenchmarkProtocol Protocol { get; }

        /// <summary>
        /// Base address for the host.
        /// </summary>
        Uri BaseAddress { get; }

        /// <summary>
        /// Start the host.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task StartAsync(CancellationToken token);

        /// <summary>
        /// Stop the host.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task StopAsync(CancellationToken token);
    }
}
