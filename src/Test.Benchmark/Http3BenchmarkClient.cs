namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Net.Quic;
    using System.Net.Security;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// Minimal raw HTTP/3 benchmark client built on QUIC.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    internal sealed class Http3BenchmarkClient : IDisposable
    {
        private readonly Uri _BaseAddress;
        private QuicConnection _Connection = null;
        private QuicStream _OutboundControlStream = null;
        private QuicStream _OutboundQpackEncoderStream = null;
        private QuicStream _OutboundQpackDecoderStream = null;
        private QuicStream _InboundControlStream = null;
        private QuicStream _InboundQpackEncoderStream = null;
        private QuicStream _InboundQpackDecoderStream = null;

        /// <summary>
        /// Instantiate the client.
        /// </summary>
        /// <param name="baseAddress">Benchmark server base address.</param>
        public Http3BenchmarkClient(Uri baseAddress)
        {
            _BaseAddress = baseAddress ?? throw new ArgumentNullException(nameof(baseAddress));
        }

        /// <summary>
        /// Connect and complete the HTTP/3 bootstrap.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ConnectAsync(CancellationToken token)
        {
            QuicClientConnectionOptions options = new QuicClientConnectionOptions();
            options.RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, _BaseAddress.Port);
            options.MaxInboundBidirectionalStreams = 128;
            options.MaxInboundUnidirectionalStreams = 8;
            options.DefaultCloseErrorCode = 0;
            options.DefaultStreamErrorCode = 0;

            SslClientAuthenticationOptions authenticationOptions = new SslClientAuthenticationOptions();
            authenticationOptions.TargetHost = "localhost";
            authenticationOptions.ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 };
            authenticationOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;
            options.ClientAuthenticationOptions = authenticationOptions;

            _Connection = await QuicConnection.ConnectAsync(options, token).ConfigureAwait(false);
            await PerformHandshakeAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send a benchmark request and read the response body.
        /// </summary>
        /// <param name="scenario">Scenario.</param>
        /// <param name="requestPayload">Optional request payload.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response body bytes.</returns>
        public async Task<byte[]> SendAsync(BenchmarkScenario scenario, byte[] requestPayload, CancellationToken token)
        {
            if (_Connection == null) throw new InvalidOperationException("HTTP/3 benchmark client is not connected.");

            string method = GetMethod(scenario);
            string path = GetPath(scenario);
            byte[] body = GetBody(scenario, requestPayload);
            List<Http3HeaderField> additionalHeaders = CreateAdditionalHeaders(scenario, body);

            QuicStream requestStream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token).ConfigureAwait(false);
            try
            {
                await WriteRequestAsync(requestStream, method, path, body, additionalHeaders, token).ConfigureAwait(false);
                Http3MessageBody message = await Http3MessageSerializer.ReadMessageAsync(requestStream, token).ConfigureAwait(false);
                ValidateStatus(message);
                return await ReadBodyAsync(message.Body, token).ConfigureAwait(false);
            }
            finally
            {
                await requestStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Send a benchmark request and validate the response body as a byte stream without materializing it.
        /// </summary>
        /// <param name="scenario">Scenario.</param>
        /// <param name="requestPayload">Optional request payload.</param>
        /// <param name="expectedResponseBytes">Expected response bytes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Response byte count.</returns>
        public async Task<int> SendAndValidateBytesAsync(BenchmarkScenario scenario, byte[] requestPayload, byte[] expectedResponseBytes, CancellationToken token)
        {
            if (_Connection == null) throw new InvalidOperationException("HTTP/3 benchmark client is not connected.");
            if (expectedResponseBytes == null) throw new ArgumentNullException(nameof(expectedResponseBytes));

            string method = GetMethod(scenario);
            string path = GetPath(scenario);
            byte[] body = GetBody(scenario, requestPayload);
            List<Http3HeaderField> additionalHeaders = CreateAdditionalHeaders(scenario, body);

            QuicStream requestStream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, token).ConfigureAwait(false);
            try
            {
                await WriteRequestAsync(requestStream, method, path, body, additionalHeaders, token).ConfigureAwait(false);
                Http3MessageBody message = await Http3MessageSerializer.ReadMessageAsync(requestStream, token).ConfigureAwait(false);
                ValidateStatus(message);
                return await ReadAndValidateBodyAsync(message.Body, expectedResponseBytes, token).ConfigureAwait(false);
            }
            finally
            {
                await requestStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_InboundControlStream != null)
            {
                _InboundControlStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _InboundControlStream = null;
            }

            if (_InboundQpackEncoderStream != null)
            {
                _InboundQpackEncoderStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _InboundQpackEncoderStream = null;
            }

            if (_InboundQpackDecoderStream != null)
            {
                _InboundQpackDecoderStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _InboundQpackDecoderStream = null;
            }

            if (_OutboundControlStream != null)
            {
                _OutboundControlStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _OutboundControlStream = null;
            }

            if (_OutboundQpackEncoderStream != null)
            {
                _OutboundQpackEncoderStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _OutboundQpackEncoderStream = null;
            }

            if (_OutboundQpackDecoderStream != null)
            {
                _OutboundQpackDecoderStream.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _OutboundQpackDecoderStream = null;
            }

            if (_Connection != null)
            {
                _Connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _Connection = null;
            }
        }

        private async Task PerformHandshakeAsync(CancellationToken token)
        {
            await WriteControlBootstrapStreamAsync(token).ConfigureAwait(false);
            await WriteBootstrapStreamAsync(Http3StreamType.QpackEncoder, token).ConfigureAwait(false);
            await WriteBootstrapStreamAsync(Http3StreamType.QpackDecoder, token).ConfigureAwait(false);

            QuicStream controlStream = await AcceptServerBootstrapControlStreamAsync(token).ConfigureAwait(false);
            await ReadControlStreamBootstrapAsync(controlStream, token).ConfigureAwait(false);
        }

        private async Task WriteControlBootstrapStreamAsync(CancellationToken token)
        {
            _OutboundControlStream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token).ConfigureAwait(false);
            byte[] payload = Http3ControlStreamSerializer.Serialize(new Http3Settings());
            await _OutboundControlStream.WriteAsync(payload, false, token).ConfigureAwait(false);
        }

        private async Task WriteBootstrapStreamAsync(Http3StreamType streamType, CancellationToken token)
        {
            QuicStream stream = await _Connection.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, token).ConfigureAwait(false);
            byte[] streamTypeBytes = Http3VarInt.Encode((long)streamType);
            await stream.WriteAsync(streamTypeBytes, false, token).ConfigureAwait(false);

            if (streamType == Http3StreamType.QpackEncoder)
            {
                _OutboundQpackEncoderStream = stream;
            }
            else if (streamType == Http3StreamType.QpackDecoder)
            {
                _OutboundQpackDecoderStream = stream;
            }
        }

        private async Task<QuicStream> AcceptServerBootstrapControlStreamAsync(CancellationToken token)
        {
            QuicStream controlStream = null;
            int receivedBootstrapStreams = 0;

            while (receivedBootstrapStreams < 3)
            {
                QuicStream peerStream = await _Connection.AcceptInboundStreamAsync(token).ConfigureAwait(false);
                long streamType = await Http3VarInt.ReadAsync(peerStream, token).ConfigureAwait(false);

                if (streamType == (long)Http3StreamType.Control)
                {
                    controlStream = peerStream;
                    _InboundControlStream = peerStream;
                    receivedBootstrapStreams++;
                    continue;
                }

                try
                {
                    if (streamType == (long)Http3StreamType.QpackEncoder)
                    {
                        _InboundQpackEncoderStream = peerStream;
                    }
                    else if (streamType == (long)Http3StreamType.QpackDecoder)
                    {
                        _InboundQpackDecoderStream = peerStream;
                    }
                    else
                    {
                        throw new IOException("Unexpected HTTP/3 bootstrap stream type " + streamType.ToString() + ".");
                    }

                    receivedBootstrapStreams++;
                }
                finally
                {
                    if (!ReferenceEquals(peerStream, _InboundControlStream)
                        && !ReferenceEquals(peerStream, _InboundQpackEncoderStream)
                        && !ReferenceEquals(peerStream, _InboundQpackDecoderStream))
                    {
                        await peerStream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }

            if (controlStream == null) throw new IOException("HTTP/3 peer bootstrap did not include a control stream.");
            return controlStream;
        }

        private async Task ReadControlStreamBootstrapAsync(QuicStream controlStream, CancellationToken token)
        {
            Http3Frame settingsFrame = await Http3FrameSerializer.ReadFrameAsync(controlStream, token).ConfigureAwait(false);
            if (settingsFrame.Header.Type != (long)Http3FrameType.Settings)
            {
                throw new IOException("Peer control stream did not begin with SETTINGS.");
            }
        }

        private async Task WriteRequestAsync(QuicStream requestStream, string method, string path, byte[] body, List<Http3HeaderField> additionalHeaders, CancellationToken token)
        {
            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            headers.Add(new Http3HeaderField { Name = ":method", Value = method });
            headers.Add(new Http3HeaderField { Name = ":scheme", Value = "https" });
            headers.Add(new Http3HeaderField { Name = ":authority", Value = _BaseAddress.Authority });
            headers.Add(new Http3HeaderField { Name = ":path", Value = path });

            if (additionalHeaders != null)
            {
                for (int i = 0; i < additionalHeaders.Count; i++)
                {
                    headers.Add(additionalHeaders[i]);
                }
            }

            byte[] headerBytes = Http3HeaderCodec.Encode(headers);
            byte[] payload = Http3MessageSerializer.SerializeMessage(headerBytes, body, null);
            await requestStream.WriteAsync(payload, true, token).ConfigureAwait(false);
        }

        private List<Http3HeaderField> CreateAdditionalHeaders(BenchmarkScenario scenario, byte[] body)
        {
            if (scenario != BenchmarkScenario.Echo && scenario != BenchmarkScenario.JsonEcho) return null;

            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            headers.Add(new Http3HeaderField { Name = "content-type", Value = scenario == BenchmarkScenario.JsonEcho ? "application/json" : "text/plain" });
            headers.Add(new Http3HeaderField { Name = "content-length", Value = (body != null ? body.Length : 0).ToString() });
            return headers;
        }

        private string GetPath(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.Hello) return "/benchmark/hello";
            if (scenario == BenchmarkScenario.Echo) return "/benchmark/echo";
            if (scenario == BenchmarkScenario.Json) return "/benchmark/json";
            if (scenario == BenchmarkScenario.SerializeJson) return "/benchmark/serialize-json";
            if (scenario == BenchmarkScenario.JsonEcho) return "/benchmark/json-echo";
            return "/benchmark/sse";
        }

        private static string GetMethod(BenchmarkScenario scenario)
        {
            if (scenario == BenchmarkScenario.Echo || scenario == BenchmarkScenario.JsonEcho) return "POST";
            return "GET";
        }

        private static byte[] GetBody(BenchmarkScenario scenario, byte[] requestPayload)
        {
            if (scenario == BenchmarkScenario.Echo || scenario == BenchmarkScenario.JsonEcho)
            {
                return requestPayload ?? Array.Empty<byte>();
            }

            return null;
        }

        private void ValidateStatus(Http3MessageBody message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.Headers == null) throw new IOException("HTTP/3 response did not include headers.");

            NameValueCollection headers = DecodeHeaders(message.Headers.HeaderBlock);
            string status = headers.Get(":status");
            if (!String.Equals(status, "200", StringComparison.Ordinal))
            {
                throw new IOException("Unexpected HTTP/3 status code " + (status ?? "<null>") + ".");
            }
        }

        private NameValueCollection DecodeHeaders(byte[] payload)
        {
            List<Http3HeaderField> decodedHeaders = Http3HeaderCodec.Decode(payload);
            NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

            for (int i = 0; i < decodedHeaders.Count; i++)
            {
                headers.Add(decodedHeaders[i].Name, decodedHeaders[i].Value);
            }

            return headers;
        }

        private async Task<byte[]> ReadBodyAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] buffer = new byte[4096];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead < 1) break;
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return memoryStream.ToArray();
            }
        }

        private async Task<int> ReadAndValidateBodyAsync(Stream stream, byte[] expectedResponseBytes, CancellationToken token)
        {
            if (expectedResponseBytes == null) throw new ArgumentNullException(nameof(expectedResponseBytes));

            byte[] buffer = new byte[4096];
            int expectedOffset = 0;
            int totalBytes = 0;

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead < 1) break;

                if ((expectedOffset + bytesRead) > expectedResponseBytes.Length)
                {
                    throw new IOException("Invalid response length.");
                }

                for (int i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] != expectedResponseBytes[expectedOffset + i])
                    {
                        throw new IOException("Invalid response payload.");
                    }
                }

                expectedOffset += bytesRead;
                totalBytes += bytesRead;
            }

            if (expectedOffset != expectedResponseBytes.Length)
            {
                throw new IOException("Invalid response length.");
            }

            return totalBytes;
        }

        private async Task<byte[]> ReadToEndAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] buffer = new byte[256];

                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead < 1) break;
                    await memoryStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                }

                return memoryStream.ToArray();
            }
        }
    }
}
