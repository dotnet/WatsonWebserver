namespace Test.Benchmark
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Kestrel benchmark host.
    /// </summary>
    internal class KestrelBenchmarkHost : IBenchmarkHost
    {
        private readonly BenchmarkProtocol _Protocol;
        private readonly BenchmarkOptions _Options;
        private readonly int _Port;
        private readonly string _HelloPayload;
        private readonly byte[] _HelloPayloadBytes;
        private readonly string _JsonPayload;
        private readonly BenchmarkJsonPayload _JsonObjectPayload;
        private readonly string _EventPayload;
        private readonly X509Certificate2 _Certificate;
        private static readonly JsonSerializerOptions _JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = false };
        private WebApplication _Application = null;
        private bool _PortReleased = false;

        /// <summary>
        /// Instantiate the host.
        /// </summary>
        /// <param name="protocol">Protocol.</param>
        /// <param name="options">Options.</param>
        public KestrelBenchmarkHost(BenchmarkProtocol protocol, BenchmarkOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _Protocol = protocol;
            _Options = options;
            _Port = BenchmarkPortFactory.GetAvailablePort(BenchmarkTarget.Kestrel);
            _HelloPayload = new string('x', _Options.PayloadBytes);
            _HelloPayloadBytes = Encoding.UTF8.GetBytes(_HelloPayload);
            _JsonPayload = BuildJsonPayload(_Options.PayloadBytes);
            _JsonObjectPayload = BuildJsonObjectPayload(_JsonPayload);
            _EventPayload = new string('e', Math.Max(1, _Options.PayloadBytes / 4));

            bool useTls = protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11;
            if (useTls)
            {
                _Certificate = BenchmarkCertificateFactory.Create();
            }
        }

        /// <inheritdoc />
        public string Name
        {
            get
            {
                return "Kestrel";
            }
        }

        /// <inheritdoc />
        public BenchmarkProtocol Protocol
        {
            get
            {
                return _Protocol;
            }
        }

        /// <inheritdoc />
        public Uri BaseAddress
        {
            get
            {
                bool useTls = _Protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11;
                return new Uri((useTls ? "https" : "http") + "://127.0.0.1:" + _Port.ToString());
            }
        }

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken token)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(_Port, ConfigureListenOptions);
            });

            _Application = builder.Build();
            _Application.MapGet("/benchmark/hello", HelloRouteAsync);
            _Application.MapPost("/benchmark/echo", EchoRouteAsync);
            _Application.MapGet("/benchmark/chunked-response", ChunkedResponseRouteAsync);
            _Application.MapGet("/benchmark/json", JsonRouteAsync);
            _Application.MapGet("/benchmark/serialize-json", SerializeJsonRouteAsync);
            _Application.MapPost("/benchmark/json-echo", JsonEchoRouteAsync);
            _Application.MapGet("/benchmark/sse", ServerSentEventsRouteAsync);

            await _Application.StartAsync(token).ConfigureAwait(false);
            await Task.Delay(250, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken token)
        {
            if (_Application != null)
            {
                await _Application.StopAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Application != null)
            {
                _Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _Application = null;
            }

            if (_Certificate != null)
            {
                _Certificate.Dispose();
            }

            if (!_PortReleased)
            {
                BenchmarkPortFactory.ReleasePort(_Port);
                _PortReleased = true;
            }
        }

        private void ConfigureListenOptions(ListenOptions listenOptions)
        {
            if (_Protocol == BenchmarkProtocol.Http11)
            {
                listenOptions.Protocols = HttpProtocols.Http1;
            }
            else if (_Protocol == BenchmarkProtocol.Http2)
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            }
            else
            {
                listenOptions.Protocols = HttpProtocols.Http3;
            }

            if (_Protocol != BenchmarkProtocol.Http11 || _Options.UseTlsForHttp11)
            {
                listenOptions.UseHttps(_Certificate);
            }
        }

        private Task HelloRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            context.Response.ContentLength = _HelloPayloadBytes.Length;
            return context.Response.Body.WriteAsync(_HelloPayloadBytes, 0, _HelloPayloadBytes.Length, context.RequestAborted);
        }

        private async Task EchoRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            if (context.Request.ContentLength.HasValue)
            {
                context.Response.ContentLength = context.Request.ContentLength.Value;
            }

            await context.Request.Body.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
        }

        private Task JsonRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(_JsonPayload, context.RequestAborted);
        }

        private async Task ChunkedResponseRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";

            const int chunkSize = 32;
            for (int offset = 0; offset < _HelloPayloadBytes.Length; offset += chunkSize)
            {
                int count = Math.Min(chunkSize, _HelloPayloadBytes.Length - offset);
                await context.Response.Body.WriteAsync(_HelloPayloadBytes, offset, count, context.RequestAborted).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            }
        }

        private Task SerializeJsonRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            string json = JsonSerializer.Serialize(_JsonObjectPayload, _JsonSerializerOptions);
            return context.Response.WriteAsync(json, context.RequestAborted);
        }

        private async Task JsonEchoRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";

            using (StreamReader reader = new StreamReader(context.Request.Body))
            {
                string body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
                await context.Response.WriteAsync(body ?? String.Empty, context.RequestAborted).ConfigureAwait(false);
            }
        }

        private async Task ServerSentEventsRouteAsync(HttpContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";

            for (int i = 0; i < _Options.ServerSentEventCount; i++)
            {
                string payload = "event: benchmark\ndata: " + _EventPayload + "\n\n";
                await context.Response.WriteAsync(payload, context.RequestAborted).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(context.RequestAborted).ConfigureAwait(false);
            }
        }

        private static string BuildJsonPayload(int payloadBytes)
        {
            int clampedPayloadBytes = payloadBytes;
            if (clampedPayloadBytes < 128) clampedPayloadBytes = 128;

            BenchmarkJsonPayload payload = new BenchmarkJsonPayload();
            payload.Message = "benchmark";
            payload.Category = "json";
            payload.Sequence = 1;
            payload.Content = String.Empty;

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions();
            serializerOptions.WriteIndented = false;

            string json = JsonSerializer.Serialize(payload, serializerOptions);
            if (System.Text.Encoding.UTF8.GetByteCount(json) >= clampedPayloadBytes) return json;

            int remainingBytes = clampedPayloadBytes - System.Text.Encoding.UTF8.GetByteCount(json);
            payload.Content = new string('j', remainingBytes);

            json = JsonSerializer.Serialize(payload, serializerOptions);
            while (System.Text.Encoding.UTF8.GetByteCount(json) > clampedPayloadBytes && payload.Content.Length > 0)
            {
                payload.Content = payload.Content.Substring(0, payload.Content.Length - 1);
                json = JsonSerializer.Serialize(payload, serializerOptions);
            }

            return json;
        }

        private static BenchmarkJsonPayload BuildJsonObjectPayload(string json)
        {
            return JsonSerializer.Deserialize<BenchmarkJsonPayload>(json);
        }

    }
}
