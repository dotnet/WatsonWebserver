namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/2 connection handshake helpers.
    /// </summary>
    public static class Http2ConnectionHandshake
    {
        /// <summary>
        /// Read the HTTP/2 client preface and initial SETTINGS frame.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Handshake result.</returns>
        public static async Task<Http2HandshakeResult> ReadClientHandshakeAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            await Http2ConnectionPreface.ReadAndValidateClientPrefaceAsync(stream, cancellationToken).ConfigureAwait(false);

            Http2RawFrame firstFrame = await Http2FrameSerializer.ReadFrameAsync(stream, cancellationToken).ConfigureAwait(false);
            if (firstFrame.Header.Type != Http2FrameType.Settings)
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "The first frame after the client preface must be a SETTINGS frame.");
            }

            bool isAcknowledgement = (firstFrame.Header.Flags & (byte)Http2FrameFlags.EndStreamOrAck) == (byte)Http2FrameFlags.EndStreamOrAck;
            if (isAcknowledgement)
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "The first SETTINGS frame from a peer must not be an acknowledgement.");
            }

            Http2HandshakeResult result = new Http2HandshakeResult();
            result.ClientPrefaceReceived = true;
            result.RemoteSettings = Http2FrameSerializer.ReadSettingsFrame(firstFrame);
            return result;
        }

        /// <summary>
        /// Create the initial server SETTINGS frame.
        /// </summary>
        /// <param name="settings">Local HTTP/2 settings.</param>
        /// <returns>SETTINGS frame.</returns>
        public static Http2RawFrame CreateServerSettingsFrame(Http2Settings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Http2FrameSerializer.CreateSettingsFrame(settings);
        }

        /// <summary>
        /// Create the SETTINGS acknowledgement frame.
        /// </summary>
        /// <returns>SETTINGS acknowledgement frame.</returns>
        public static Http2RawFrame CreateSettingsAcknowledgementFrame()
        {
            return Http2FrameSerializer.CreateSettingsAcknowledgementFrame();
        }
    }
}
