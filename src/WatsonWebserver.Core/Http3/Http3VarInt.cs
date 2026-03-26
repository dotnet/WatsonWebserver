namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// QUIC and HTTP/3 variable-length integer codec.
    /// </summary>
    public static class Http3VarInt
    {
        /// <summary>
        /// Maximum legal value.
        /// </summary>
        public const long MaxValue = 4611686018427387903L;

        /// <summary>
        /// Encode a value.
        /// </summary>
        /// <param name="value">Value to encode.</param>
        /// <returns>Encoded bytes.</returns>
        public static byte[] Encode(long value)
        {
            if (value < 0 || value > MaxValue) throw new ArgumentOutOfRangeException(nameof(value));

            if (value <= 63)
            {
                return new byte[] { (byte)value };
            }

            if (value <= 16383)
            {
                return new byte[]
                {
                    (byte)(0x40 | ((value >> 8) & 0x3F)),
                    (byte)(value & 0xFF)
                };
            }

            if (value <= 1073741823)
            {
                return new byte[]
                {
                    (byte)(0x80 | ((value >> 24) & 0x3F)),
                    (byte)((value >> 16) & 0xFF),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF)
                };
            }

            return new byte[]
            {
                (byte)(0xC0 | ((value >> 56) & 0x3F)),
                (byte)((value >> 48) & 0xFF),
                (byte)((value >> 40) & 0xFF),
                (byte)((value >> 32) & 0xFF),
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        /// <summary>
        /// Decode an encoded value.
        /// </summary>
        /// <param name="buffer">Encoded bytes.</param>
        /// <param name="offset">Offset in the buffer.</param>
        /// <param name="bytesConsumed">Number of bytes consumed.</param>
        /// <returns>Decoded value.</returns>
        public static long Decode(byte[] buffer, int offset, out int bytesConsumed)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset >= buffer.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            int encodedLength = GetEncodedLength(buffer[offset]);
            if ((buffer.Length - offset) < encodedLength) throw new Http3ProtocolException("HTTP/3 variable-length integer is truncated.");

            long value = buffer[offset] & 0x3F;
            for (int i = 1; i < encodedLength; i++)
            {
                value = (value << 8) | buffer[offset + i];
            }

            bytesConsumed = encodedLength;
            return value;
        }

        /// <summary>
        /// Read an encoded value from a stream.
        /// </summary>
        /// <param name="stream">Readable stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Decoded value.</returns>
        public static async Task<long> ReadAsync(Stream stream, CancellationToken token = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            byte[] firstByteBuffer = new byte[1];
            await ReadExactAsync(stream, firstByteBuffer, 0, 1, token).ConfigureAwait(false);

            int encodedLength = GetEncodedLength(firstByteBuffer[0]);
            if (encodedLength == 1)
            {
                return firstByteBuffer[0] & 0x3F;
            }

            byte[] remainder = new byte[encodedLength - 1];
            await ReadExactAsync(stream, remainder, 0, remainder.Length, token).ConfigureAwait(false);

            byte[] combined = new byte[encodedLength];
            combined[0] = firstByteBuffer[0];
            Buffer.BlockCopy(remainder, 0, combined, 1, remainder.Length);

            int bytesConsumed;
            return Decode(combined, 0, out bytesConsumed);
        }

        private static int GetEncodedLength(byte firstByte)
        {
            int prefix = (firstByte & 0xC0) >> 6;
            if (prefix == 0) return 1;
            if (prefix == 1) return 2;
            if (prefix == 2) return 4;
            return 8;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken token)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, token).ConfigureAwait(false);
                if (bytesRead < 1) throw new EndOfStreamException("Unexpected end of stream while reading HTTP/3 variable-length integer.");
                totalRead += bytesRead;
            }
        }
    }
}
