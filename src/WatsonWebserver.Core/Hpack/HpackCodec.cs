namespace WatsonWebserver.Core.Hpack
{
    using System;
    using System.Collections.Generic;
    using WatsonWebserver.Core.Http2;

    using System.Text;

    /// <summary>
    /// Minimal HPACK encoder and decoder supporting indexed and literal-without-indexing fields.
    /// </summary>
    public static class HpackCodec
    {
        /// <summary>
        /// Decode HPACK header fields.
        /// </summary>
        /// <param name="payload">HPACK payload.</param>
        /// <returns>Decoded header fields.</returns>
        public static List<HpackHeaderField> Decode(byte[] payload)
        {
            return Decode(payload, new HpackDecoderContext());
        }

        /// <summary>
        /// Decode HPACK header fields using a connection-scoped decoding context.
        /// </summary>
        /// <param name="payload">HPACK payload.</param>
        /// <param name="context">Decoder context.</param>
        /// <returns>Decoded header fields.</returns>
        public static List<HpackHeaderField> Decode(byte[] payload, HpackDecoderContext context)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (context == null) throw new ArgumentNullException(nameof(context));

            List<HpackHeaderField> headers = new List<HpackHeaderField>();
            int offset = 0;

            while (offset < payload.Length)
            {
                byte current = payload[offset];

                if ((current & 0x80) == 0x80)
                {
                    int index = DecodeInteger(payload, ref offset, 7);
                    headers.Add(context.GetByIndex(index));
                    continue;
                }

                if ((current & 0x40) == 0x40)
                {
                    HpackHeaderField indexedHeader = DecodeLiteralHeaderFieldWithIncrementalIndexing(payload, ref offset, context);
                    headers.Add(indexedHeader);
                    continue;
                }

                if ((current & 0xE0) == 0x20)
                {
                    int updatedSize = DecodeInteger(payload, ref offset, 5);
                    context.UpdateMaxDynamicTableSize(updatedSize);
                    continue;
                }

                if ((current & 0xF0) == 0x00 || (current & 0xF0) == 0x10)
                {
                    headers.Add(DecodeLiteralHeaderFieldWithoutIndexing(payload, ref offset, context));
                    continue;
                }

                throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "Unsupported HPACK representation was received.");
            }

            return headers;
        }

        /// <summary>
        /// Encode HPACK header fields using indexed fields when possible and literal-without-indexing otherwise.
        /// </summary>
        /// <param name="headers">Header fields.</param>
        /// <returns>Encoded payload.</returns>
        public static byte[] Encode(IReadOnlyList<HpackHeaderField> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            List<byte> output = new List<byte>();

            for (int i = 0; i < headers.Count; i++)
            {
                HpackHeaderField header = headers[i];
                int exactIndex = HpackStaticTable.FindExact(header.Name, header.Value);
                if (exactIndex > 0)
                {
                    EncodeInteger(output, exactIndex, 7, 0x80);
                    continue;
                }

                int nameIndex = HpackStaticTable.FindName(header.Name);
                output.AddRange(EncodeLiteralWithoutIndexing(header.Name, header.Value, nameIndex));
            }

            return output.ToArray();
        }

        private static HpackHeaderField DecodeLiteralHeaderFieldWithIncrementalIndexing(byte[] payload, ref int offset, HpackDecoderContext context)
        {
            int nameIndex = DecodeInteger(payload, ref offset, 6);
            string name = String.Empty;

            if (nameIndex > 0)
            {
                name = context.GetByIndex(nameIndex).Name;
            }
            else
            {
                name = DecodeString(payload, ref offset);
            }

            string value = DecodeString(payload, ref offset);
            context.Add(name, value);

            HpackHeaderField field = new HpackHeaderField();
            field.Name = name;
            field.Value = value;
            return field;
        }

        private static HpackHeaderField DecodeLiteralHeaderFieldWithoutIndexing(byte[] payload, ref int offset, HpackDecoderContext context)
        {
            int nameIndex = DecodeInteger(payload, ref offset, 4);
            string name = String.Empty;

            if (nameIndex > 0)
            {
                name = context.GetByIndex(nameIndex).Name;
            }
            else
            {
                name = DecodeString(payload, ref offset);
            }

            string value = DecodeString(payload, ref offset);
            HpackHeaderField field = new HpackHeaderField();
            field.Name = name;
            field.Value = value;
            return field;
        }

        private static byte[] EncodeLiteralWithoutIndexing(string name, string value, int nameIndex)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            List<byte> bytes = new List<byte>();
            EncodeInteger(bytes, nameIndex, 4, 0x00);
            if (nameIndex == 0) bytes.AddRange(EncodeString(name));
            bytes.AddRange(EncodeString(value));
            return bytes.ToArray();
        }

        private static string DecodeString(byte[] payload, ref int offset)
        {
            if (offset >= payload.Length) throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "HPACK string length is truncated.");

            bool huffmanEncoded = (payload[offset] & 0x80) == 0x80;
            int length = DecodeInteger(payload, ref offset, 7);
            if (length < 0 || (offset + length) > payload.Length) throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "HPACK string payload is truncated.");

            byte[] stringPayload = new byte[length];
            Buffer.BlockCopy(payload, offset, stringPayload, 0, length);
            offset += length;

            try
            {
                return huffmanEncoded ? HttpFieldHuffmanDecoder.Decode(stringPayload) : Encoding.UTF8.GetString(stringPayload);
            }
            catch (InvalidOperationException e)
            {
                throw new Http2ProtocolException(Http2ErrorCode.CompressionError, e.Message);
            }
        }

        private static byte[] EncodeString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            List<byte> bytes = new List<byte>();
            EncodeInteger(bytes, stringBytes.Length, 7, 0x00);
            bytes.AddRange(stringBytes);
            return bytes.ToArray();
        }

        private static int DecodeInteger(byte[] payload, ref int offset, int prefixBits)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (offset >= payload.Length) throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "HPACK integer is truncated.");

            int prefixMask = (1 << prefixBits) - 1;
            int value = payload[offset] & prefixMask;
            offset++;

            if (value < prefixMask) return value;

            int multiplier = 0;
            byte nextByte = 0;

            do
            {
                if (offset >= payload.Length) throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "HPACK integer continuation is truncated.");
                nextByte = payload[offset++];
                value += (nextByte & 0x7F) << multiplier;
                multiplier += 7;
            }
            while ((nextByte & 0x80) == 0x80);

            return value;
        }

        private static void EncodeInteger(List<byte> output, int value, int prefixBits, int prefixMask)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));

            int maxPrefixValue = (1 << prefixBits) - 1;

            if (value < maxPrefixValue)
            {
                output.Add((byte)(prefixMask | value));
                return;
            }

            output.Add((byte)(prefixMask | maxPrefixValue));
            int remaining = value - maxPrefixValue;

            while (remaining >= 128)
            {
                output.Add((byte)((remaining % 128) + 128));
                remaining /= 128;
            }

            output.Add((byte)remaining);
        }
    }
}
