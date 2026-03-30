namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core.Hpack;
    using WatsonWebserver.Core.Http2;

    /// <summary>
    /// Shared HTTP/2 transport helpers for smoke tests.
    /// </summary>
    internal static class Http2SharedTestUtilities
    {
        /// <summary>
        /// Perform a minimal HTTP/2 client handshake.
        /// </summary>
        /// <param name="stream">Connected network stream.</param>
        /// <returns>Server SETTINGS frame.</returns>
        internal static async Task<Http2RawFrame> PerformClientHandshakeAsync(NetworkStream stream)
        {
            return await PerformClientHandshakeAsync(stream, new Http2Settings()).ConfigureAwait(false);
        }

        /// <summary>
        /// Perform a minimal HTTP/2 client handshake with caller-supplied settings.
        /// </summary>
        /// <param name="stream">Connected network stream.</param>
        /// <param name="clientSettings">Client settings.</param>
        /// <returns>Server SETTINGS frame.</returns>
        internal static async Task<Http2RawFrame> PerformClientHandshakeAsync(NetworkStream stream, Http2Settings clientSettings)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (clientSettings == null) throw new ArgumentNullException(nameof(clientSettings));

            byte[] prefaceBytes = Http2ConnectionPreface.GetClientPrefaceBytes();
            byte[] settingsBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsFrame(clientSettings));

            await stream.WriteAsync(prefaceBytes, 0, prefaceBytes.Length).ConfigureAwait(false);
            await stream.WriteAsync(settingsBytes, 0, settingsBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            Http2RawFrame serverSettings = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

            byte[] acknowledgementBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsAcknowledgementFrame());
            await stream.WriteAsync(acknowledgementBytes, 0, acknowledgementBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            return serverSettings;
        }

        /// <summary>
        /// Build a simple HTTP/2 request header block.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="scheme">URI scheme.</param>
        /// <param name="authority">Request authority.</param>
        /// <param name="path">Request path.</param>
        /// <returns>Encoded HPACK header block.</returns>
        internal static byte[] BuildRequestHeaderBlock(string method, string scheme, string authority, string path)
        {
            if (String.IsNullOrEmpty(method)) throw new ArgumentNullException(nameof(method));
            if (String.IsNullOrEmpty(scheme)) throw new ArgumentNullException(nameof(scheme));
            if (String.IsNullOrEmpty(authority)) throw new ArgumentNullException(nameof(authority));
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));

            List<HpackHeaderField> requestHeaders = new List<HpackHeaderField>();
            requestHeaders.Add(new HpackHeaderField { Name = ":method", Value = method });
            requestHeaders.Add(new HpackHeaderField { Name = ":scheme", Value = scheme });
            requestHeaders.Add(new HpackHeaderField { Name = ":authority", Value = authority });
            requestHeaders.Add(new HpackHeaderField { Name = ":path", Value = path });
            return HpackCodec.Encode(requestHeaders);
        }

        /// <summary>
        /// Read a simple HTTP/2 response composed of response header frames followed by DATA frames.
        /// </summary>
        /// <param name="stream">Connected network stream.</param>
        /// <returns>Response envelope.</returns>
        internal static async Task<Http2ResponseEnvelope> ReadResponseAsync(NetworkStream stream)
        {
            return await ReadResponseAsync(stream, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a simple HTTP/2 response composed of response header frames followed by DATA frames.
        /// </summary>
        /// <param name="stream">Connected network stream.</param>
        /// <param name="headerFrames">Optional response header frames collector.</param>
        /// <returns>Response envelope.</returns>
        internal static async Task<Http2ResponseEnvelope> ReadResponseAsync(NetworkStream stream, List<Http2RawFrame> headerFrames)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Http2ResponseEnvelope response = new Http2ResponseEnvelope();

            using (MemoryStream bodyStream = new MemoryStream())
            using (MemoryStream headerBlockStream = new MemoryStream())
            {
                bool responseHeadersReceived = false;

                while (true)
                {
                    Http2RawFrame frame = await Http2FrameSerializer.ReadFrameAsync(stream, CancellationToken.None).ConfigureAwait(false);

                    if (frame.Header.Type == Http2FrameType.Headers || frame.Header.Type == Http2FrameType.Continuation)
                    {
                        if (headerFrames != null)
                        {
                            headerFrames.Add(frame);
                        }

                        if (frame.Payload.Length > 0)
                        {
                            await headerBlockStream.WriteAsync(frame.Payload, 0, frame.Payload.Length).ConfigureAwait(false);
                        }

                        bool endHeaders = (frame.Header.Flags & (byte)Http2FrameFlags.EndHeaders) == (byte)Http2FrameFlags.EndHeaders;
                        if (endHeaders)
                        {
                            List<HpackHeaderField> decodedHeaderFields = HpackCodec.Decode(headerBlockStream.ToArray());
                            NameValueCollection destination = responseHeadersReceived ? response.Trailers : response.Headers;

                            for (int i = 0; i < decodedHeaderFields.Count; i++)
                            {
                                destination[decodedHeaderFields[i].Name] = decodedHeaderFields[i].Value;
                            }

                            responseHeadersReceived = true;
                            headerBlockStream.SetLength(0);
                        }

                        bool endStreamOnHeaders = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                        if (endStreamOnHeaders && responseHeadersReceived)
                        {
                            break;
                        }
                    }
                    else if (frame.Header.Type == Http2FrameType.Data)
                    {
                        if (frame.Payload.Length > 0)
                        {
                            await bodyStream.WriteAsync(frame.Payload, 0, frame.Payload.Length).ConfigureAwait(false);
                        }

                        bool endStreamOnData = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                        if (endStreamOnData)
                        {
                            break;
                        }
                    }
                    else if (frame.Header.Type == Http2FrameType.Settings)
                    {
                        bool isAcknowledgement = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
                        if (!isAcknowledgement)
                        {
                            byte[] acknowledgementBytes = Http2FrameSerializer.SerializeFrame(Http2FrameSerializer.CreateSettingsAcknowledgementFrame());
                            await stream.WriteAsync(acknowledgementBytes, 0, acknowledgementBytes.Length).ConfigureAwait(false);
                            await stream.FlushAsync().ConfigureAwait(false);
                        }
                    }
                    else if (frame.Header.Type == Http2FrameType.WindowUpdate)
                    {
                        continue;
                    }
                    else
                    {
                        throw new IOException("Unexpected HTTP/2 frame type while reading response.");
                    }
                }

                response.BodyString = Encoding.UTF8.GetString(bodyStream.ToArray());
                return response;
            }
        }

        /// <summary>
        /// Build a padded HEADERS frame payload that also includes PRIORITY information.
        /// </summary>
        /// <param name="headerBlock">Header block fragment.</param>
        /// <param name="padLength">Pad length.</param>
        /// <param name="streamDependency">Stream dependency identifier.</param>
        /// <param name="weight">Priority weight.</param>
        /// <returns>Frame payload.</returns>
        internal static byte[] BuildPaddedPriorityHeadersPayload(byte[] headerBlock, byte padLength, int streamDependency, byte weight)
        {
            if (headerBlock == null) throw new ArgumentNullException(nameof(headerBlock));
            if (streamDependency < 0) throw new ArgumentOutOfRangeException(nameof(streamDependency));

            byte[] payload = new byte[1 + 5 + headerBlock.Length + padLength];
            payload[0] = padLength;
            payload[1] = (byte)((streamDependency >> 24) & 0x7F);
            payload[2] = (byte)((streamDependency >> 16) & 0xFF);
            payload[3] = (byte)((streamDependency >> 8) & 0xFF);
            payload[4] = (byte)(streamDependency & 0xFF);
            payload[5] = weight;
            Buffer.BlockCopy(headerBlock, 0, payload, 6, headerBlock.Length);
            return payload;
        }

        /// <summary>
        /// Build a padded DATA frame payload.
        /// </summary>
        /// <param name="body">Body payload.</param>
        /// <param name="padLength">Pad length.</param>
        /// <returns>Frame payload.</returns>
        internal static byte[] BuildPaddedDataPayload(byte[] body, byte padLength)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            byte[] payload = new byte[1 + body.Length + padLength];
            payload[0] = padLength;
            Buffer.BlockCopy(body, 0, payload, 1, body.Length);
            return payload;
        }
    }
}
