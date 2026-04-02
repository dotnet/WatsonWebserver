namespace WatsonWebserver.Core.Http1
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads HTTP/1.1 chunked transfer-encoding payloads.
    /// </summary>
    public static class Http1ChunkReader
    {
        /// <summary>
        /// Read the next chunk from a stream.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <param name="streamBufferSize">Read buffer size.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Chunk metadata and payload.</returns>
        public static async Task<Chunk> ReadAsync(Stream stream, int streamBufferSize, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (streamBufferSize < 1) throw new ArgumentOutOfRangeException(nameof(streamBufferSize));

            Chunk chunk = new Chunk();
            byte[] buffer = new byte[1];
            MemoryStream lengthStream = new MemoryStream();

            using (lengthStream)
            {
                while (true)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead < 1) throw new MalformedHttpRequestException("Unable to read chunk length from stream.");

                    await lengthStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                    if (buffer[0] == 10)
                    {
                        string lengthString = Encoding.UTF8.GetString(lengthStream.ToArray()).Trim();

                        try
                        {
                            if (lengthString.Contains(";"))
                            {
                                string[] lengthParts = lengthString.Split(new char[] { ';' }, 2);
                                chunk.Length = int.Parse(lengthParts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                if (lengthParts.Length > 1) chunk.Metadata = lengthParts[1];
                            }
                            else
                            {
                                chunk.Length = int.Parse(lengthString, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                            }
                        }
                        catch (Exception e)
                        {
                            throw new MalformedHttpRequestException("Chunk length value is invalid.", e);
                        }

                        break;
                    }
                }
            }

            if (chunk.Length > 0)
            {
                chunk.IsFinal = false;
                chunk.Data = await ReadChunkDataAsync(stream, chunk.Length, streamBufferSize, token).ConfigureAwait(false);
            }
            else
            {
                chunk.IsFinal = true;
                chunk.Data = Array.Empty<byte>();
            }

            await ReadTrailingCrlfAsync(stream, token).ConfigureAwait(false);
            return chunk;
        }

        private static async Task<byte[]> ReadChunkDataAsync(Stream stream, int chunkLength, int streamBufferSize, CancellationToken token)
        {
            int bytesRemaining = chunkLength;

            using (MemoryStream memoryStream = new MemoryStream())
            {
                while (bytesRemaining > 0)
                {
                    int bytesToRead = bytesRemaining > streamBufferSize ? streamBufferSize : bytesRemaining;
                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                    if (bytesRead < 1) throw new MalformedHttpRequestException("Unexpected end of stream while reading chunk data.");

                    memoryStream.Write(buffer, 0, bytesRead);
                    bytesRemaining -= bytesRead;
                }

                return memoryStream.ToArray();
            }
        }

        private static async Task ReadTrailingCrlfAsync(Stream stream, CancellationToken token)
        {
            byte[] buffer = new byte[1];

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                if (bytesRead < 1) throw new MalformedHttpRequestException("Unexpected end of stream while reading chunk terminator.");
                if (buffer[0] == 10) break;
            }
        }
    }
}
