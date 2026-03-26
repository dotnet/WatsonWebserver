namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/3 frame serializer and parser.
    /// </summary>
    public static class Http3FrameSerializer
    {
        /// <summary>
        /// Serialize an HTTP/3 frame header.
        /// </summary>
        /// <param name="frameType">Frame type.</param>
        /// <param name="payloadLength">Payload length.</param>
        /// <returns>Serialized header bytes.</returns>
        public static byte[] SerializeFrameHeader(long frameType, long payloadLength)
        {
            if (payloadLength < 0) throw new ArgumentOutOfRangeException(nameof(payloadLength));

            byte[] typeBytes = Http3VarInt.Encode(frameType);
            byte[] lengthBytes = Http3VarInt.Encode(payloadLength);
            byte[] serialized = new byte[typeBytes.Length + lengthBytes.Length];
            Buffer.BlockCopy(typeBytes, 0, serialized, 0, typeBytes.Length);
            Buffer.BlockCopy(lengthBytes, 0, serialized, typeBytes.Length, lengthBytes.Length);
            return serialized;
        }

        /// <summary>
        /// Serialize a frame.
        /// </summary>
        /// <param name="frame">Frame to serialize.</param>
        /// <returns>Serialized frame bytes.</returns>
        public static byte[] SerializeFrame(Http3Frame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header == null) throw new ArgumentNullException(nameof(frame.Header));
            if (frame.Payload == null) throw new ArgumentNullException(nameof(frame.Payload));
            if (frame.Header.Length != frame.Payload.Length) throw new ArgumentException("HTTP/3 frame header length must match payload length.", nameof(frame));

            byte[] headerBytes = SerializeFrameHeader(frame.Header.Type, frame.Header.Length);
            byte[] serialized = new byte[headerBytes.Length + frame.Payload.Length];
            Buffer.BlockCopy(headerBytes, 0, serialized, 0, headerBytes.Length);
            if (frame.Payload.Length > 0)
            {
                Buffer.BlockCopy(frame.Payload, 0, serialized, headerBytes.Length, frame.Payload.Length);
            }

            return serialized;
        }

        /// <summary>
        /// Create a GOAWAY frame.
        /// </summary>
        /// <param name="frame">GOAWAY payload.</param>
        /// <returns>HTTP/3 frame.</returns>
        public static Http3Frame CreateGoAwayFrame(Http3GoAwayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            byte[] identifierBytes = Http3VarInt.Encode(frame.Identifier);
            Http3Frame goAwayFrame = new Http3Frame();
            goAwayFrame.Header = new Http3FrameHeader { Type = (long)Http3FrameType.GoAway, Length = identifierBytes.Length };
            goAwayFrame.Payload = identifierBytes;
            return goAwayFrame;
        }

        /// <summary>
        /// Parse a GOAWAY frame.
        /// </summary>
        /// <param name="frame">GOAWAY frame.</param>
        /// <returns>Typed GOAWAY payload.</returns>
        public static Http3GoAwayFrame ReadGoAwayFrame(Http3Frame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header == null) throw new ArgumentNullException(nameof(frame.Header));
            if (frame.Header.Type != (long)Http3FrameType.GoAway) throw new Http3ProtocolException("HTTP/3 frame is not a GOAWAY frame.");

            int bytesConsumed;
            long identifier = Http3VarInt.Decode(frame.Payload ?? Array.Empty<byte>(), 0, out bytesConsumed);
            if (frame.Payload == null || bytesConsumed != frame.Payload.Length)
            {
                throw new Http3ProtocolException("HTTP/3 GOAWAY frame payload is malformed.");
            }

            Http3GoAwayFrame goAwayFrame = new Http3GoAwayFrame();
            goAwayFrame.Identifier = identifier;
            return goAwayFrame;
        }

        /// <summary>
        /// Read a frame from a stream.
        /// </summary>
        /// <param name="stream">Readable stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed frame.</returns>
        public static async Task<Http3Frame> ReadFrameAsync(Stream stream, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            long frameType = await Http3VarInt.ReadAsync(stream, token).ConfigureAwait(false);
            long frameLength = await Http3VarInt.ReadAsync(stream, token).ConfigureAwait(false);
            if (frameLength > Int32.MaxValue) throw new Http3ProtocolException("HTTP/3 frame length exceeds supported test harness limits.");

            byte[] payload = new byte[frameLength];
            if (frameLength > 0)
            {
                await ReadExactAsync(stream, payload, 0, (int)frameLength, token).ConfigureAwait(false);
            }

            Http3Frame frame = new Http3Frame();
            frame.Header = new Http3FrameHeader { Type = frameType, Length = frameLength };
            frame.Payload = payload;
            return frame;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token).ConfigureAwait(false);
                if (bytesRead < 1) throw new EndOfStreamException("Unexpected end of stream while reading HTTP/3 frame payload.");
                totalRead += bytesRead;
            }
        }
    }
}
