namespace WatsonWebserver.Core.Http1
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads HTTP/1.1 request headers from a stream.
    /// </summary>
    public static class Http1HeaderReader
    {
        private const int ReadBlockSize = 4096;

        /// <summary>
        /// Read an HTTP/1.1 request header block from a stream.
        /// </summary>
        /// <param name="stream">Readable stream.</param>
        /// <param name="maxHeaderSize">Maximum permitted header size in bytes.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Request header read result.</returns>
        public static async Task<Http1HeaderReadResult> ReadAsync(Stream stream, int maxHeaderSize, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (maxHeaderSize < 1) throw new ArgumentOutOfRangeException(nameof(maxHeaderSize));

            byte[] buffer = ArrayPool<byte>.Shared.Rent(maxHeaderSize);
            int bytesReadTotal = 0;
            int searchStartIndex = 0;

            try
            {
                while (true)
                {
                    int bytesToRead = Math.Min(ReadBlockSize, maxHeaderSize - bytesReadTotal);
                    if (bytesToRead < 1)
                    {
                        throw new IOException("Request headers exceed maximum allowed size " + maxHeaderSize + ".");
                    }

                    int bytesRead = await stream.ReadAsync(buffer, bytesReadTotal, bytesToRead, token).ConfigureAwait(false);
                    if (bytesRead < 1)
                    {
                        if (bytesReadTotal < 1) return null;

                        Http1HeaderReadResult partialResult = new Http1HeaderReadResult();
                        partialResult.HeaderBytes = CopyBytes(buffer, 0, bytesReadTotal);
                        return partialResult;
                    }

                    bytesReadTotal += bytesRead;
                    int headerLength = FindHeaderLength(buffer, searchStartIndex, bytesReadTotal);
                    if (headerLength > 0)
                    {
                        Http1HeaderReadResult result = new Http1HeaderReadResult();
                        result.HeaderBytes = CopyBytes(buffer, 0, headerLength);

                        int prefixLength = bytesReadTotal - headerLength;
                        if (prefixLength > 0)
                        {
                            result.PrefixBytes = CopyBytes(buffer, headerLength, prefixLength);
                        }

                        return result;
                    }

                    searchStartIndex = Math.Max(0, bytesReadTotal - 3);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static int FindHeaderLength(byte[] buffer, int startIndex, int length)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));
            if (length < 1) return -1;
            if (startIndex >= length) return -1;

            int headerSearchStart = Math.Min(startIndex, Math.Max(0, length - 1));

            for (int i = headerSearchStart; i <= (length - 4); i++)
            {
                if (buffer[i] == 13
                    && buffer[i + 1] == 10
                    && buffer[i + 2] == 13
                    && buffer[i + 3] == 10)
                {
                    return i + 4;
                }
            }

            for (int i = headerSearchStart; i <= (length - 2); i++)
            {
                if (buffer[i] == 10 && buffer[i + 1] == 10)
                {
                    return i + 2;
                }
            }

            return -1;
        }

        private static byte[] CopyBytes(byte[] source, int offset, int count)
        {
            if (count < 1) return Array.Empty<byte>();

            byte[] result = new byte[count];
            Buffer.BlockCopy(source, offset, result, 0, count);
            return result;
        }

    }
}
