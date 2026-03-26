namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/2 frame read and write helpers.
    /// </summary>
    public static class Http2FrameSerializer
    {
        /// <summary>
        /// Serialize an HTTP/2 frame header.
        /// </summary>
        /// <param name="header">Frame header.</param>
        /// <returns>Serialized bytes.</returns>
        public static byte[] SerializeFrameHeader(Http2FrameHeader header)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));

            byte[] bytes = new byte[Http2Constants.FrameHeaderLength];
            bytes[0] = (byte)((header.Length >> 16) & 0xFF);
            bytes[1] = (byte)((header.Length >> 8) & 0xFF);
            bytes[2] = (byte)(header.Length & 0xFF);
            bytes[3] = (byte)header.Type;
            bytes[4] = header.Flags;
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(5, 4), (uint)header.StreamIdentifier & 0x7FFFFFFF);
            return bytes;
        }

        /// <summary>
        /// Serialize an HTTP/2 frame.
        /// </summary>
        /// <param name="frame">Frame.</param>
        /// <returns>Serialized bytes.</returns>
        public static byte[] SerializeFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            byte[] headerBytes = SerializeFrameHeader(frame.Header);
            byte[] bytes = new byte[headerBytes.Length + frame.Payload.Length];
            Buffer.BlockCopy(headerBytes, 0, bytes, 0, headerBytes.Length);
            Buffer.BlockCopy(frame.Payload, 0, bytes, headerBytes.Length, frame.Payload.Length);
            return bytes;
        }

        /// <summary>
        /// Read a frame header from the stream.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Frame header.</returns>
        public static async Task<Http2FrameHeader> ReadFrameHeaderAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] bytes = new byte[Http2Constants.FrameHeaderLength];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            return ParseFrameHeader(bytes);
        }

        /// <summary>
        /// Read a complete frame from the stream.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Raw frame.</returns>
        public static async Task<Http2RawFrame> ReadFrameAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Http2FrameHeader header = await ReadFrameHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
            byte[] payload = new byte[header.Length];
            if (payload.Length > 0) await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            return new Http2RawFrame(header, payload);
        }

        /// <summary>
        /// Parse a frame header from serialized bytes.
        /// </summary>
        /// <param name="bytes">Serialized header bytes.</param>
        /// <returns>Frame header.</returns>
        public static Http2FrameHeader ParseFrameHeader(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != Http2Constants.FrameHeaderLength) throw new ArgumentException("HTTP/2 frame headers must be exactly 9 bytes.", nameof(bytes));

            Http2FrameHeader header = new Http2FrameHeader();
            header.Length = (bytes[0] << 16) | (bytes[1] << 8) | bytes[2];
            header.Type = (Http2FrameType)bytes[3];
            header.Flags = bytes[4];
            header.StreamIdentifier = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)) & 0x7FFFFFFF);
            return header;
        }

        /// <summary>
        /// Create a SETTINGS frame.
        /// </summary>
        /// <param name="settings">Settings payload.</param>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreateSettingsFrame(Http2Settings settings)
        {
            byte[] payload = Http2SettingsSerializer.SerializePayload(settings);
            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = payload.Length,
                Type = Http2FrameType.Settings,
                Flags = 0,
                StreamIdentifier = 0
            }, payload);
        }

        /// <summary>
        /// Create an empty SETTINGS acknowledgement frame.
        /// </summary>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreateSettingsAcknowledgementFrame()
        {
            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = 0,
                Type = Http2FrameType.Settings,
                Flags = (byte)Http2FrameFlags.EndStreamOrAck,
                StreamIdentifier = 0
            }, Array.Empty<byte>());
        }

        /// <summary>
        /// Parse SETTINGS payload from a raw frame.
        /// </summary>
        /// <param name="frame">Raw frame.</param>
        /// <returns>Settings payload.</returns>
        public static Http2Settings ReadSettingsFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header.Type != Http2FrameType.Settings) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected a SETTINGS frame.");
            if (frame.Header.StreamIdentifier != 0) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "SETTINGS frames must use stream identifier 0.");

            bool isAck = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
            if (isAck)
            {
                if (frame.Payload.Length != 0) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "SETTINGS acknowledgement frames must not include a payload.");
                return new Http2Settings();
            }

            return Http2SettingsSerializer.ParsePayload(frame.Payload);
        }

        /// <summary>
        /// Create a PING frame.
        /// </summary>
        /// <param name="frame">PING frame.</param>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreatePingFrame(Http2PingFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = frame.OpaqueData.Length,
                Type = Http2FrameType.Ping,
                Flags = frame.Acknowledge ? (byte)Http2FrameFlags.EndStreamOrAck : (byte)Http2FrameFlags.None,
                StreamIdentifier = 0
            }, frame.OpaqueData);
        }

        /// <summary>
        /// Parse a PING frame.
        /// </summary>
        /// <param name="frame">Raw frame.</param>
        /// <returns>PING payload.</returns>
        public static Http2PingFrame ReadPingFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header.Type != Http2FrameType.Ping) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected a PING frame.");
            if (frame.Header.StreamIdentifier != 0) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "PING frames must use stream identifier 0.");
            if (frame.Payload.Length != 8) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "PING frames must contain exactly 8 bytes of opaque data.");

            Http2PingFrame pingFrame = new Http2PingFrame();
            pingFrame.Acknowledge = (frame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
            pingFrame.OpaqueData = frame.Payload;
            return pingFrame;
        }

        /// <summary>
        /// Create an RST_STREAM frame.
        /// </summary>
        /// <param name="frame">RST_STREAM payload.</param>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreateRstStreamFrame(Http2RstStreamFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            byte[] payload = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), (uint)frame.ErrorCode);

            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = payload.Length,
                Type = Http2FrameType.RstStream,
                Flags = 0,
                StreamIdentifier = frame.StreamIdentifier
            }, payload);
        }

        /// <summary>
        /// Parse an RST_STREAM frame.
        /// </summary>
        /// <param name="frame">Raw frame.</param>
        /// <returns>RST_STREAM payload.</returns>
        public static Http2RstStreamFrame ReadRstStreamFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header.Type != Http2FrameType.RstStream) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected an RST_STREAM frame.");
            if (frame.Header.StreamIdentifier == 0) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "RST_STREAM frames must use a non-zero stream identifier.");
            if (frame.Payload.Length != 4) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "RST_STREAM frames must contain exactly 4 bytes.");

            Http2RstStreamFrame rstStreamFrame = new Http2RstStreamFrame();
            rstStreamFrame.StreamIdentifier = frame.Header.StreamIdentifier;
            rstStreamFrame.ErrorCode = (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(0, 4));
            return rstStreamFrame;
        }

        /// <summary>
        /// Create a WINDOW_UPDATE frame.
        /// </summary>
        /// <param name="frame">WINDOW_UPDATE payload.</param>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreateWindowUpdateFrame(Http2WindowUpdateFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            byte[] payload = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), (uint)frame.WindowSizeIncrement & 0x7FFFFFFF);

            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = payload.Length,
                Type = Http2FrameType.WindowUpdate,
                Flags = 0,
                StreamIdentifier = frame.StreamIdentifier
            }, payload);
        }

        /// <summary>
        /// Parse a WINDOW_UPDATE frame.
        /// </summary>
        /// <param name="frame">Raw frame.</param>
        /// <returns>WINDOW_UPDATE payload.</returns>
        public static Http2WindowUpdateFrame ReadWindowUpdateFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header.Type != Http2FrameType.WindowUpdate) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected a WINDOW_UPDATE frame.");
            if (frame.Payload.Length != 4) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "WINDOW_UPDATE frames must contain exactly 4 bytes.");

            int increment = (int)(BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(0, 4)) & 0x7FFFFFFF);
            if (increment < 1) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "WINDOW_UPDATE increments must be greater than zero.");

            Http2WindowUpdateFrame windowUpdateFrame = new Http2WindowUpdateFrame();
            windowUpdateFrame.StreamIdentifier = frame.Header.StreamIdentifier;
            windowUpdateFrame.WindowSizeIncrement = increment;
            return windowUpdateFrame;
        }

        /// <summary>
        /// Create a GOAWAY frame.
        /// </summary>
        /// <param name="frame">GOAWAY frame.</param>
        /// <returns>Raw frame.</returns>
        public static Http2RawFrame CreateGoAwayFrame(Http2GoAwayFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));

            byte[] payload = new byte[8 + frame.AdditionalDebugData.Length];
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0, 4), (uint)frame.LastStreamIdentifier & 0x7FFFFFFF);
            BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(4, 4), (uint)frame.ErrorCode);

            if (frame.AdditionalDebugData.Length > 0)
            {
                Buffer.BlockCopy(frame.AdditionalDebugData, 0, payload, 8, frame.AdditionalDebugData.Length);
            }

            return new Http2RawFrame(new Http2FrameHeader
            {
                Length = payload.Length,
                Type = Http2FrameType.GoAway,
                Flags = 0,
                StreamIdentifier = 0
            }, payload);
        }

        /// <summary>
        /// Parse a GOAWAY frame.
        /// </summary>
        /// <param name="frame">Raw frame.</param>
        /// <returns>GOAWAY payload.</returns>
        public static Http2GoAwayFrame ReadGoAwayFrame(Http2RawFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header.Type != Http2FrameType.GoAway) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "Expected a GOAWAY frame.");
            if (frame.Header.StreamIdentifier != 0) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "GOAWAY frames must use stream identifier 0.");
            if (frame.Payload.Length < 8) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "GOAWAY frames must contain at least 8 bytes.");

            Http2GoAwayFrame goAwayFrame = new Http2GoAwayFrame();
            goAwayFrame.LastStreamIdentifier = (int)(BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(0, 4)) & 0x7FFFFFFF);
            goAwayFrame.ErrorCode = (Http2ErrorCode)BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(4, 4));

            byte[] debugData = new byte[frame.Payload.Length - 8];
            if (debugData.Length > 0) Buffer.BlockCopy(frame.Payload, 8, debugData, 0, debugData.Length);
            goAwayFrame.AdditionalDebugData = debugData;

            return goAwayFrame;
        }
    }
}
