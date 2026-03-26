namespace Test.Benchmark.LegacyHost
{
    using System;
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Lite;

    /// <summary>
    /// Legacy benchmark host process entry point.
    /// </summary>
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            LegacyHostOptions options = LegacyHostOptions.Parse(args);

            try
            {
                if (String.Equals(options.Target, "watson6", StringComparison.OrdinalIgnoreCase))
                {
                    await RunWatsonAsync(options).ConfigureAwait(false);
                }
                else if (String.Equals(options.Target, "watsonlite6", StringComparison.OrdinalIgnoreCase))
                {
                    await RunWatsonLiteAsync(options).ConfigureAwait(false);
                }
                else
                {
                    throw new InvalidOperationException("Unknown legacy benchmark target.");
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine("ERROR: " + exception.GetType().Name + ": " + exception.Message);
                return 1;
            }
        }

        private static async Task RunWatsonAsync(LegacyHostOptions options)
        {
            X509Certificate2 certificate = null;
            Webserver server = null;

            try
            {
                WebserverSettings settings = CreateSettings(options, ref certificate);
                server = new Webserver(settings, DefaultRouteAsync);
                AddRoutes(server.Routes, options);
                server.Start();
                Console.WriteLine("READY");
                await Console.In.ReadLineAsync().ConfigureAwait(false);
            }
            finally
            {
                if (server != null)
                {
                    server.Stop();
                    server.Dispose();
                }

                if (certificate != null)
                {
                    certificate.Dispose();
                }
            }
        }

        private static async Task RunWatsonLiteAsync(LegacyHostOptions options)
        {
            X509Certificate2 certificate = null;
            WebserverLite server = null;

            try
            {
                WebserverSettings settings = CreateSettings(options, ref certificate);
                server = new WebserverLite(settings, DefaultRouteAsync);
                AddRoutes(server.Routes, options);
                server.Start();
                Console.WriteLine("READY");
                await Console.In.ReadLineAsync().ConfigureAwait(false);
            }
            finally
            {
                if (server != null)
                {
                    server.Stop();
                    server.Dispose();
                }

                if (certificate != null)
                {
                    certificate.Dispose();
                }
            }
        }

        private static WebserverSettings CreateSettings(LegacyHostOptions options, ref X509Certificate2 certificate)
        {
            WebserverSettings settings = new WebserverSettings("127.0.0.1", options.Port, options.UseTls);
            settings.IO.EnableKeepAlive = true;
            settings.IO.MaxRequests = Math.Max(1024, options.Concurrency * 16);
            settings.IO.ReadTimeoutMs = Math.Max(30000, options.TimeoutSeconds * 1000);

            if (settings.Ssl.Enable)
            {
                certificate = BenchmarkCertificateFactory.Create();
                settings.Ssl.SslCertificate = certificate;
            }

            return settings;
        }

        private static void AddRoutes(WebserverRoutes routes, LegacyHostOptions options)
        {
            string helloPayload = new string('x', options.PayloadBytes);
            byte[] helloPayloadBytes = Encoding.UTF8.GetBytes(helloPayload);
            string jsonPayload = BuildJsonPayload(options.PayloadBytes);
            string eventPayload = new string('e', Math.Max(1, options.PayloadBytes / 4));
            DefaultSerializationHelper serializer = new DefaultSerializationHelper();

            routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/hello", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                await context.Response.Send(helloPayloadBytes, context.Token).ConfigureAwait(false);
            });

            routes.PostAuthentication.Static.Add(HttpMethod.POST, "/benchmark/echo", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                if (context.Request.ChunkedTransfer)
                {
                    byte[] body = context.Request.DataAsBytes ?? Array.Empty<byte>();
                    context.Response.ContentLength = body != null ? body.Length : 0;
                    await context.Response.Send(body ?? Array.Empty<byte>(), context.Token).ConfigureAwait(false);
                }
                else
                {
                    if (context.Request.ContentLength > 0 && context.Request.Data != null)
                    {
                        await context.Response.Send(context.Request.ContentLength, context.Request.Data, context.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await context.Response.Send(Array.Empty<byte>(), context.Token).ConfigureAwait(false);
                    }
                }
            });

            routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/chunked-response", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain";
                context.Response.ChunkedTransfer = true;

                const int chunkSize = 32;
                for (int offset = 0; offset < helloPayloadBytes.Length; offset += chunkSize)
                {
                    int count = Math.Min(chunkSize, helloPayloadBytes.Length - offset);
                    byte[] chunk = new byte[count];
                    Buffer.BlockCopy(helloPayloadBytes, offset, chunk, 0, count);
                    await context.Response.SendChunk(chunk, offset + count >= helloPayloadBytes.Length, context.Token).ConfigureAwait(false);
                }
            });

            routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/json", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.Send(jsonPayload, context.Token).ConfigureAwait(false);
            });

            LegacyBenchmarkJsonPayload jsonObjectPayload = BuildJsonObjectPayload(jsonPayload);
            routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/serialize-json", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.Send(serializer.SerializeJson(jsonObjectPayload, false), context.Token).ConfigureAwait(false);
            });

            routes.PostAuthentication.Static.Add(HttpMethod.POST, "/benchmark/json-echo", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.Send(context.Request.DataAsString ?? String.Empty, context.Token).ConfigureAwait(false);
            });

            routes.PostAuthentication.Static.Add(HttpMethod.GET, "/benchmark/sse", async context =>
            {
                context.Response.StatusCode = 200;
                context.Response.ServerSentEvents = true;

                for (int i = 0; i < options.ServerSentEventCount; i++)
                {
                    ServerSentEvent serverSentEvent = new ServerSentEvent();
                    serverSentEvent.Event = "benchmark";
                    serverSentEvent.Data = eventPayload;
                    await context.Response.SendEvent(serverSentEvent, i == (options.ServerSentEventCount - 1), context.Token).ConfigureAwait(false);
                }
            });
        }

        private static Task DefaultRouteAsync(HttpContextBase context)
        {
            context.Response.StatusCode = 404;
            return context.Response.Send("not-found", context.Token);
        }

        private static string BuildJsonPayload(int payloadBytes)
        {
            int clampedPayloadBytes = payloadBytes;
            if (clampedPayloadBytes < 128) clampedPayloadBytes = 128;

            LegacyBenchmarkJsonPayload payload = new LegacyBenchmarkJsonPayload();
            payload.Message = "benchmark";
            payload.Category = "json";
            payload.Sequence = 1;
            payload.Content = String.Empty;

            JsonSerializerOptions serializerOptions = new JsonSerializerOptions();
            serializerOptions.WriteIndented = false;

            string json = JsonSerializer.Serialize(payload, serializerOptions);
            if (Encoding.UTF8.GetByteCount(json) >= clampedPayloadBytes) return json;

            int remainingBytes = clampedPayloadBytes - Encoding.UTF8.GetByteCount(json);
            payload.Content = new string('j', remainingBytes);

            json = JsonSerializer.Serialize(payload, serializerOptions);
            while (Encoding.UTF8.GetByteCount(json) > clampedPayloadBytes && payload.Content.Length > 0)
            {
                payload.Content = payload.Content.Substring(0, payload.Content.Length - 1);
                json = JsonSerializer.Serialize(payload, serializerOptions);
            }

            return json;
        }

        private static LegacyBenchmarkJsonPayload BuildJsonObjectPayload(string json)
        {
            return JsonSerializer.Deserialize<LegacyBenchmarkJsonPayload>(json);
        }

        private sealed class LegacyHostOptions
        {
            public string Target { get; set; } = "watson6";

            public int Port { get; set; } = 8000;

            public bool UseTls { get; set; } = false;

            public int PayloadBytes { get; set; } = 4096;

            public int ServerSentEventCount { get; set; } = 8;

            public int TimeoutSeconds { get; set; } = 30;

            public int Concurrency { get; set; } = 32;

            public static LegacyHostOptions Parse(string[] args)
            {
                LegacyHostOptions options = new LegacyHostOptions();

                if (args == null) return options;

                for (int i = 0; i < args.Length; i++)
                {
                    string argument = args[i];
                    if (String.IsNullOrEmpty(argument)) continue;

                    if (String.Equals(argument, "--target", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                    {
                        options.Target = args[++i];
                    }
                    else if (String.Equals(argument, "--port", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                    {
                        options.Port = ParseInt(args[++i], 1, 65535, options.Port);
                    }
                    else if (String.Equals(argument, "--use-tls", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                    {
                        options.UseTls = ParseBool(args[++i], options.UseTls);
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
                        options.TimeoutSeconds = ParseInt(args[++i], 1, 3600, options.TimeoutSeconds);
                    }
                    else if (String.Equals(argument, "--concurrency", StringComparison.OrdinalIgnoreCase) && (i + 1) < args.Length)
                    {
                        options.Concurrency = ParseInt(args[++i], 1, 4096, options.Concurrency);
                    }
                }

                return options;
            }

            private static int ParseInt(string value, int minimum, int maximum, int fallback)
            {
                int parsed;
                if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) return fallback;
                if (parsed < minimum) return minimum;
                if (parsed > maximum) return maximum;
                return parsed;
            }

            private static bool ParseBool(string value, bool fallback)
            {
                bool parsed;
                if (!Boolean.TryParse(value, out parsed)) return fallback;
                return parsed;
            }
        }

        private static class BenchmarkCertificateFactory
        {
            public static X509Certificate2 Create()
            {
                using (RSA rsa = RSA.Create(2048))
                {
                    CertificateRequest request = new CertificateRequest(
                        "CN=localhost",
                        rsa,
                        HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);

                    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                    request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

                    SubjectAlternativeNameBuilder subjectAlternativeName = new SubjectAlternativeNameBuilder();
                    subjectAlternativeName.AddDnsName("localhost");
                    subjectAlternativeName.AddIpAddress(System.Net.IPAddress.Loopback);
                    request.CertificateExtensions.Add(subjectAlternativeName.Build());

                    using (X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1)))
                    {
                        byte[] exported = certificate.Export(X509ContentType.Pfx);
#if NET10_0_OR_GREATER
                        return X509CertificateLoader.LoadPkcs12(exported, null);
#else
#pragma warning disable SYSLIB0057
                        return new X509Certificate2(exported);
#pragma warning restore SYSLIB0057
#endif
                    }
                }
            }
        }
    }
}
