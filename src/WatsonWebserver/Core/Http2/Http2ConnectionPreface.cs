namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// HTTP/2 client connection preface helpers.
    /// </summary>
    public static class Http2ConnectionPreface
    {
        /// <summary>
        /// Return a copy of the canonical client preface bytes.
        /// </summary>
        /// <returns>Preface bytes.</returns>
        public static byte[] GetClientPrefaceBytes()
        {
            return (byte[])Http2Constants.ClientConnectionPrefaceBytes.Clone();
        }

        /// <summary>
        /// Determine whether the supplied bytes match the HTTP/2 client preface.
        /// </summary>
        /// <param name="bytes">Candidate bytes.</param>
        /// <returns>True if the bytes match the preface.</returns>
        public static bool IsClientPreface(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != Http2Constants.ClientConnectionPrefaceBytes.Length) return false;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != Http2Constants.ClientConnectionPrefaceBytes[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// Write the client connection preface to a stream.
        /// </summary>
        /// <param name="stream">Output stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task WriteClientPrefaceAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] bytes = Http2Constants.ClientConnectionPrefaceBytes;
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Read and validate the HTTP/2 client connection preface.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task ReadAndValidateClientPrefaceAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] bytes = new byte[Http2Constants.ClientConnectionPrefaceBytes.Length];
#if NET8_0_OR_GREATER
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
#else
            await ReadExactlyFallbackAsync(stream, bytes, cancellationToken).ConfigureAwait(false);
#endif

            if (!IsClientPreface(bytes))
            {
                throw new Http2ProtocolException(Http2ErrorCode.ProtocolError, "The received client connection preface is not valid HTTP/2.");
            }
        }

#if !NET8_0_OR_GREATER
        private static async Task ReadExactlyFallbackAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
                if (bytesRead < 1) throw new System.IO.EndOfStreamException("Unexpected end of stream.");
                offset += bytesRead;
            }
        }
#endif
    }
}
