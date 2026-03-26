namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/3 bidirectional message serializer and parser.
    /// </summary>
    public static class Http3MessageSerializer
    {
        /// <summary>
        /// Create a HEADERS frame.
        /// </summary>
        /// <param name="headerBlock">Encoded QPACK header block.</param>
        /// <returns>HEADERS frame.</returns>
        public static Http3Frame CreateHeadersFrame(byte[] headerBlock)
        {
            byte[] payload = headerBlock ?? Array.Empty<byte>();
            Http3Frame frame = new Http3Frame();
            frame.Header = new Http3FrameHeader { Type = (long)Http3FrameType.Headers, Length = payload.Length };
            frame.Payload = payload;
            return frame;
        }

        /// <summary>
        /// Create a DATA frame.
        /// </summary>
        /// <param name="data">Payload bytes.</param>
        /// <returns>DATA frame.</returns>
        public static Http3Frame CreateDataFrame(byte[] data)
        {
            byte[] payload = data ?? Array.Empty<byte>();
            Http3Frame frame = new Http3Frame();
            frame.Header = new Http3FrameHeader { Type = (long)Http3FrameType.Data, Length = payload.Length };
            frame.Payload = payload;
            return frame;
        }

        /// <summary>
        /// Read a complete HTTP/3 message from a bidirectional stream.
        /// Unknown frame types are ignored.
        /// </summary>
        /// <param name="stream">Readable stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed message.</returns>
        public static async Task<Http3MessageBody> ReadMessageAsync(Stream stream, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Http3MessageBody message = new Http3MessageBody();
            bool sawInitialHeaders = false;
            bool sawTrailers = false;

            while (true)
            {
                Http3Frame frame;

                try
                {
                    frame = await Http3FrameSerializer.ReadFrameAsync(stream, token).ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                if (frame.Header.Type == (long)Http3FrameType.Headers)
                {
                    Http3HeadersFrame headersFrame = new Http3HeadersFrame();
                    headersFrame.HeaderBlock = frame.Payload ?? Array.Empty<byte>();

                    if (!sawInitialHeaders)
                    {
                        message.Headers = headersFrame;
                        sawInitialHeaders = true;
                        continue;
                    }

                    if (sawTrailers)
                    {
                        throw new Http3ProtocolException("HTTP/3 message contained multiple trailing HEADERS frames.");
                    }

                    message.Trailers = headersFrame;
                    sawTrailers = true;
                    continue;
                }

                if (frame.Header.Type == (long)Http3FrameType.Data)
                {
                    if (!sawInitialHeaders)
                    {
                        throw new Http3ProtocolException("HTTP/3 DATA frame arrived before initial HEADERS.");
                    }

                    if (sawTrailers)
                    {
                        throw new Http3ProtocolException("HTTP/3 DATA frame arrived after trailing HEADERS.");
                    }

                    if (frame.Payload != null && frame.Payload.Length > 0)
                    {
                        message.Body.Write(frame.Payload, 0, frame.Payload.Length);
                    }

                    continue;
                }
            }

            if (!sawInitialHeaders)
            {
                throw new Http3ProtocolException("HTTP/3 message did not contain an initial HEADERS frame.");
            }

            if (message.BodyOrNull != null)
            {
                message.BodyOrNull.Seek(0, SeekOrigin.Begin);
            }

            return message;
        }

        /// <summary>
        /// Serialize a message.
        /// </summary>
        /// <param name="headers">Initial header block.</param>
        /// <param name="body">Optional body bytes.</param>
        /// <param name="trailers">Optional trailing header block.</param>
        /// <returns>Serialized frames.</returns>
        public static byte[] SerializeMessage(byte[] headers, byte[] body, byte[] trailers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            List<byte[]> frameSegments = new List<byte[]>();
            frameSegments.Add(Http3FrameSerializer.SerializeFrame(CreateHeadersFrame(headers)));

            byte[] bodyBytes = body ?? Array.Empty<byte>();
            if (bodyBytes.Length > 0)
            {
                frameSegments.Add(Http3FrameSerializer.SerializeFrame(CreateDataFrame(bodyBytes)));
            }

            if (trailers != null)
            {
                frameSegments.Add(Http3FrameSerializer.SerializeFrame(CreateHeadersFrame(trailers)));
            }

            int totalLength = 0;
            for (int i = 0; i < frameSegments.Count; i++)
            {
                totalLength += frameSegments[i].Length;
            }

            byte[] payload = new byte[totalLength];
            int offset = 0;

            for (int i = 0; i < frameSegments.Count; i++)
            {
                Buffer.BlockCopy(frameSegments[i], 0, payload, offset, frameSegments[i].Length);
                offset += frameSegments[i].Length;
            }

            return payload;
        }
    }
}
