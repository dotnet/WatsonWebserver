namespace Test.Benchmark
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Base benchmark host that runs a legacy Watson server out of process.
    /// </summary>
    internal abstract class LegacyProcessBenchmarkHost : IBenchmarkHost
    {
        private readonly BenchmarkTarget _Target;
        private readonly BenchmarkOptions _Options;
        private int _Port;
        private Process _Process = null;
        private bool _PortReleased = false;

        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="target">Legacy target.</param>
        /// <param name="options">Benchmark options.</param>
        protected LegacyProcessBenchmarkHost(BenchmarkTarget target, BenchmarkOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _Target = target;
            _Options = options;
            _Port = BenchmarkPortFactory.GetAvailablePort(target);
        }

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public BenchmarkProtocol Protocol
        {
            get
            {
                return BenchmarkProtocol.Http11;
            }
        }

        /// <inheritdoc />
        public Uri BaseAddress
        {
            get
            {
                return new Uri((_Options.UseTlsForHttp11 ? "https" : "http") + "://127.0.0.1:" + _Port.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken token)
        {
            string helperProjectPath = GetHelperProjectPath();
            string targetFramework = GetCurrentTargetFrameworkMoniker();
            const int maxAttempts = 4;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string arguments =
                    "run --project \"" + helperProjectPath + "\" --framework " + targetFramework + " -c Release --" +
                    " --target " + GetLegacyTargetArgument() +
                    " --port " + _Port.ToString(CultureInfo.InvariantCulture) +
                    " --use-tls " + _Options.UseTlsForHttp11.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() +
                    " --payload-bytes " + _Options.PayloadBytes.ToString(CultureInfo.InvariantCulture) +
                    " --sse-events " + _Options.ServerSentEventCount.ToString(CultureInfo.InvariantCulture) +
                    " --timeout-seconds " + _Options.RequestTimeoutSeconds.ToString(CultureInfo.InvariantCulture) +
                    " --concurrency " + _Options.Concurrency.ToString(CultureInfo.InvariantCulture);

                ProcessStartInfo startInfo = new ProcessStartInfo("dotnet", arguments);
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;
                startInfo.CreateNoWindow = true;
                startInfo.WorkingDirectory = GetRepositoryRoot();

                _Process = new Process();
                _Process.StartInfo = startInfo;
                _Process.Start();

                try
                {
                    await WaitForReadyAsync(_Process, token).ConfigureAwait(false);
                    return;
                }
                catch (IOException exception) when (attempt < maxAttempts && IsAddressInUseError(exception))
                {
                    await TerminateProcessAsync(CancellationToken.None).ConfigureAwait(false);
                    BenchmarkPortFactory.ReleasePort(_Port);
                    _Port = BenchmarkPortFactory.GetAvailablePort(_Target);
                    _PortReleased = false;
                }
                catch
                {
                    await TerminateProcessAsync(CancellationToken.None).ConfigureAwait(false);
                    throw;
                }
            }
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken token)
        {
            await TerminateProcessAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Process != null)
            {
                if (!_Process.HasExited)
                {
                    _Process.Kill(true);
                    _Process.WaitForExit();
                }

                _Process.Dispose();
                _Process = null;
            }

            ReleasePort();
        }

        /// <summary>
        /// Get the legacy target argument value.
        /// </summary>
        /// <returns>Argument text.</returns>
        protected string GetLegacyTargetArgument()
        {
            if (_Target == BenchmarkTarget.Watson6) return "watson6";
            return "watsonlite6";
        }

        private async Task WaitForReadyAsync(Process process, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (process.HasExited)
                {
                    string errorText = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    throw new IOException("Legacy benchmark host exited before ready. " + errorText);
                }

                string line = await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false);
                if (String.IsNullOrEmpty(line)) continue;
                if (String.Equals(line, "READY", StringComparison.OrdinalIgnoreCase)) return;
                if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(line);
                }
            }
        }

        private string GetHelperProjectPath()
        {
            return Path.Combine(GetRepositoryRoot(), "src", "Test.Benchmark.LegacyHost", "Test.Benchmark.LegacyHost.csproj");
        }

        private string GetRepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        private string GetCurrentTargetFrameworkMoniker()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            TargetFrameworkAttribute attribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            if (attribute == null || String.IsNullOrEmpty(attribute.FrameworkName)) return "net10.0";
            if (attribute.FrameworkName.Contains("Version=v8.0", StringComparison.OrdinalIgnoreCase)) return "net8.0";
            if (attribute.FrameworkName.Contains("Version=v10.0", StringComparison.OrdinalIgnoreCase)) return "net10.0";
            return "net10.0";
        }

        private void ReleasePort()
        {
            if (_PortReleased) return;
            BenchmarkPortFactory.ReleasePort(_Port);
            _PortReleased = true;
        }

        private static bool IsAddressInUseError(IOException exception)
        {
            if (exception == null) return false;

            string message = exception.Message ?? String.Empty;
            return message.IndexOf("Only one usage of each socket address", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Address already in use", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task TerminateProcessAsync(CancellationToken token)
        {
            if (_Process == null) return;

            try
            {
                if (!_Process.HasExited)
                {
                    try
                    {
                        await _Process.StandardInput.WriteLineAsync().ConfigureAwait(false);
                        await _Process.StandardInput.FlushAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    Task exitTask = _Process.WaitForExitAsync(token);
                    Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), token);
                    Task completedTask = await Task.WhenAny(exitTask, timeoutTask).ConfigureAwait(false);

                    if (completedTask != exitTask && !_Process.HasExited)
                    {
                        _Process.Kill(true);
                        await _Process.WaitForExitAsync(token).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _Process.Dispose();
                _Process = null;
            }
        }
    }
}
