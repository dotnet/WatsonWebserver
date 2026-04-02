namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// HTTP/3 SETTINGS payload serializer and parser.
    /// </summary>
    public static class Http3SettingsSerializer
    {
        /// <summary>
        /// Serialize settings payload bytes.
        /// </summary>
        /// <param name="settings">Typed settings.</param>
        /// <returns>Serialized payload bytes.</returns>
        public static byte[] SerializePayload(Http3Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            using (MemoryStream memoryStream = new MemoryStream())
            {
                WriteSetting(memoryStream, Http3SettingIdentifier.QpackMaxTableCapacity, settings.QpackMaxTableCapacity);
                WriteSetting(memoryStream, Http3SettingIdentifier.MaxFieldSectionSize, settings.MaxFieldSectionSize);
                WriteSetting(memoryStream, Http3SettingIdentifier.QpackBlockedStreams, settings.QpackBlockedStreams);
                WriteSetting(memoryStream, Http3SettingIdentifier.H3Datagram, settings.EnableDatagram ? 1 : 0);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Parse settings payload bytes.
        /// </summary>
        /// <param name="payload">Serialized payload bytes.</param>
        /// <returns>Typed settings.</returns>
        public static Http3Settings ParsePayload(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            Http3Settings settings = new Http3Settings();
            HashSet<long> encounteredSettings = new HashSet<long>();
            int offset = 0;

            while (offset < payload.Length)
            {
                int identifierBytesConsumed;
                long identifier = Http3VarInt.Decode(payload, offset, out identifierBytesConsumed);
                offset += identifierBytesConsumed;

                int valueBytesConsumed;
                long value = Http3VarInt.Decode(payload, offset, out valueBytesConsumed);
                offset += valueBytesConsumed;

                if (!encounteredSettings.Add(identifier))
                {
                    throw new Http3ProtocolException("Duplicate HTTP/3 setting identifier " + identifier + " was supplied.");
                }

                ApplySetting(settings, identifier, value);
            }

            return settings;
        }

        /// <summary>
        /// Create a SETTINGS frame.
        /// </summary>
        /// <param name="settings">Typed settings.</param>
        /// <returns>SETTINGS frame.</returns>
        public static Http3Frame CreateSettingsFrame(Http3Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            byte[] payload = SerializePayload(settings);
            Http3Frame frame = new Http3Frame();
            frame.Header = new Http3FrameHeader { Type = (long)Http3FrameType.Settings, Length = payload.Length };
            frame.Payload = payload;
            return frame;
        }

        /// <summary>
        /// Parse a SETTINGS frame.
        /// </summary>
        /// <param name="frame">SETTINGS frame.</param>
        /// <returns>Typed settings.</returns>
        public static Http3Settings ReadSettingsFrame(Http3Frame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (frame.Header == null) throw new ArgumentNullException(nameof(frame.Header));
            if (frame.Header.Type != (long)Http3FrameType.Settings) throw new Http3ProtocolException("HTTP/3 frame is not a SETTINGS frame.");
            return ParsePayload(frame.Payload ?? Array.Empty<byte>());
        }

        private static void WriteSetting(Stream stream, Http3SettingIdentifier identifier, long value)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            byte[] identifierBytes = Http3VarInt.Encode((long)identifier);
            byte[] valueBytes = Http3VarInt.Encode(value);
            stream.Write(identifierBytes, 0, identifierBytes.Length);
            stream.Write(valueBytes, 0, valueBytes.Length);
        }

        private static void ApplySetting(Http3Settings settings, long identifier, long value)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            if (identifier == (long)Http3SettingIdentifier.QpackMaxTableCapacity)
            {
                settings.QpackMaxTableCapacity = value;
            }
            else if (identifier == (long)Http3SettingIdentifier.MaxFieldSectionSize)
            {
                settings.MaxFieldSectionSize = value;
            }
            else if (identifier == (long)Http3SettingIdentifier.QpackBlockedStreams)
            {
                settings.QpackBlockedStreams = value;
            }
            else if (identifier == (long)Http3SettingIdentifier.H3Datagram)
            {
                if (value > 1) throw new Http3ProtocolException("HTTP/3 H3_DATAGRAM setting must be 0 or 1.");
                settings.EnableDatagram = value == 1;
            }
        }
    }
}
