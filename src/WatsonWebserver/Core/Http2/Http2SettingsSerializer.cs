namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;

    /// <summary>
    /// HTTP/2 SETTINGS payload serializer and parser.
    /// </summary>
    public static class Http2SettingsSerializer
    {
        /// <summary>
        /// Serialize settings to a SETTINGS payload.
        /// </summary>
        /// <param name="settings">Settings payload.</param>
        /// <returns>Serialized payload bytes.</returns>
        public static byte[] SerializePayload(Http2Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            List<Http2SettingsParameter> parameters = ToParameters(settings);
            byte[] payload = new byte[parameters.Count * 6];

            for (int i = 0; i < parameters.Count; i++)
            {
                Http2SettingsParameter parameter = parameters[i];
                int offset = i * 6;
                BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(offset, 2), (ushort)parameter.Identifier);
                BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(offset + 2, 4), parameter.Value);
            }

            return payload;
        }

        /// <summary>
        /// Parse settings from a SETTINGS payload.
        /// </summary>
        /// <param name="payload">Serialized payload bytes.</param>
        /// <returns>Typed settings.</returns>
        public static Http2Settings ParsePayload(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if ((payload.Length % 6) != 0) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "SETTINGS payload length must be a multiple of 6 bytes.");

            Http2Settings settings = new Http2Settings();
            List<Http2SettingsParameter> parameters = ParseParameters(payload);

            foreach (Http2SettingsParameter parameter in parameters)
            {
                ApplyParameter(settings, parameter);
            }

            return settings;
        }

        /// <summary>
        /// Convert typed settings to individual parameters.
        /// </summary>
        /// <param name="settings">Typed settings.</param>
        /// <returns>Parameter list.</returns>
        public static List<Http2SettingsParameter> ToParameters(Http2Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            List<Http2SettingsParameter> parameters = new List<Http2SettingsParameter>();
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.HeaderTableSize, Value = settings.HeaderTableSize });
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.EnablePush, Value = settings.EnablePush ? 1U : 0U });
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.MaxConcurrentStreams, Value = settings.MaxConcurrentStreams });
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.InitialWindowSize, Value = (uint)settings.InitialWindowSize });
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.MaxFrameSize, Value = (uint)settings.MaxFrameSize });
            parameters.Add(new Http2SettingsParameter { Identifier = Http2SettingIdentifier.MaxHeaderListSize, Value = settings.MaxHeaderListSize });
            return parameters;
        }

        /// <summary>
        /// Parse a SETTINGS payload into individual parameters.
        /// </summary>
        /// <param name="payload">Serialized payload bytes.</param>
        /// <returns>Parameter list.</returns>
        public static List<Http2SettingsParameter> ParseParameters(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if ((payload.Length % 6) != 0) throw new Http2ProtocolException(Http2ErrorCode.FrameSizeError, "SETTINGS payload length must be a multiple of 6 bytes.");

            List<Http2SettingsParameter> parameters = new List<Http2SettingsParameter>();

            for (int offset = 0; offset < payload.Length; offset += 6)
            {
                Http2SettingsParameter parameter = new Http2SettingsParameter();
                parameter.Identifier = (Http2SettingIdentifier)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset, 2));
                parameter.Value = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(offset + 2, 4));
                parameters.Add(parameter);
            }

            return parameters;
        }

        private static void ApplyParameter(Http2Settings settings, Http2SettingsParameter parameter)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (parameter == null) throw new ArgumentNullException(nameof(parameter));

            switch (parameter.Identifier)
            {
                case Http2SettingIdentifier.HeaderTableSize:
                    settings.HeaderTableSize = parameter.Value;
                    break;
                case Http2SettingIdentifier.EnablePush:
                    if (parameter.Value > 1) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "SETTINGS_ENABLE_PUSH must be 0 or 1.");
                    settings.EnablePush = parameter.Value == 1;
                    break;
                case Http2SettingIdentifier.MaxConcurrentStreams:
                    settings.MaxConcurrentStreams = parameter.Value;
                    break;
                case Http2SettingIdentifier.InitialWindowSize:
                    if (parameter.Value > Int32.MaxValue) throw new Http2ProtocolException(Http2ErrorCode.FlowControlError, "SETTINGS_INITIAL_WINDOW_SIZE exceeds the legal 31-bit range.");
                    settings.InitialWindowSize = (int)parameter.Value;
                    break;
                case Http2SettingIdentifier.MaxFrameSize:
                    if (parameter.Value < Http2Constants.MinMaxFrameSize || parameter.Value > Http2Constants.MaxMaxFrameSize) throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "SETTINGS_MAX_FRAME_SIZE is outside the legal HTTP/2 range.");
                    settings.MaxFrameSize = (int)parameter.Value;
                    break;
                case Http2SettingIdentifier.MaxHeaderListSize:
                    settings.MaxHeaderListSize = parameter.Value;
                    break;
                default:
                    break;
            }
        }
    }
}
