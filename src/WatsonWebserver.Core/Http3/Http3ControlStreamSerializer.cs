namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/3 control stream bootstrap serializer and parser.
    /// </summary>
    public static class Http3ControlStreamSerializer
    {
        /// <summary>
        /// Serialize the control stream type marker and SETTINGS frame.
        /// </summary>
        /// <param name="settings">Settings to advertise.</param>
        /// <returns>Serialized control stream bootstrap bytes.</returns>
        public static byte[] Serialize(Http3Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            byte[] streamTypeBytes = Http3VarInt.Encode((long)Http3StreamType.Control);
            byte[] settingsFrameBytes = Http3FrameSerializer.SerializeFrame(Http3SettingsSerializer.CreateSettingsFrame(settings));
            byte[] payload = new byte[streamTypeBytes.Length + settingsFrameBytes.Length];

            Buffer.BlockCopy(streamTypeBytes, 0, payload, 0, streamTypeBytes.Length);
            Buffer.BlockCopy(settingsFrameBytes, 0, payload, streamTypeBytes.Length, settingsFrameBytes.Length);
            return payload;
        }

        /// <summary>
        /// Read the control stream type marker and initial SETTINGS frame.
        /// </summary>
        /// <param name="stream">Readable stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed control stream payload.</returns>
        public static async Task<Http3ControlStreamPayload> ReadAsync(Stream stream, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            long streamTypeValue = await Http3VarInt.ReadAsync(stream, token).ConfigureAwait(false);
            if (streamTypeValue != (long)Http3StreamType.Control)
            {
                throw new Http3ProtocolException("HTTP/3 unidirectional stream did not begin with a control stream type.");
            }

            Http3Frame settingsFrame = await Http3FrameSerializer.ReadFrameAsync(stream, token).ConfigureAwait(false);
            if (settingsFrame.Header.Type != (long)Http3FrameType.Settings)
            {
                throw new Http3ProtocolException("HTTP/3 control stream did not begin with a SETTINGS frame.");
            }

            Http3ControlStreamPayload payload = new Http3ControlStreamPayload();
            payload.StreamType = Http3StreamType.Control;
            payload.Settings = Http3SettingsSerializer.ReadSettingsFrame(settingsFrame);
            return payload;
        }
    }
}
