namespace WatsonWebserver.Core.Qpack
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using WatsonWebserver.Core.Http3;
    using WatsonWebserver.Core.Hpack;


    /// <summary>
    /// Minimal QPACK encoder and decoder supporting static-table references and literal fields.
    /// </summary>
    public static class QpackCodec
    {
        /// <summary>
        /// Decode a QPACK header block.
        /// </summary>
        /// <param name="payload">Encoded QPACK payload.</param>
        /// <returns>Decoded header fields.</returns>
        public static List<Http3HeaderField> Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            List<Http3HeaderField> headers = new List<Http3HeaderField>();
            int offset = 0;
            DecodeHeaderBlockPrefix(payload, ref offset);

            while (offset < payload.Length)
            {
                byte current = payload[offset];

                if ((current & 0x80) == 0x80)
                {
                    headers.Add(DecodeIndexedFieldLine(payload, ref offset));
                    continue;
                }

                if ((current & 0x40) == 0x40)
                {
                    headers.Add(DecodeLiteralFieldLineWithNameReference(payload, ref offset));
                    continue;
                }

                if ((current & 0x20) == 0x20)
                {
                    headers.Add(DecodeLiteralFieldLineWithLiteralName(payload, ref offset));
                    continue;
                }

                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Unsupported QPACK representation was received.");
            }

            return headers;
        }

        /// <summary>
        /// Encode header fields using static-table references when possible and literal forms otherwise.
        /// </summary>
        /// <param name="headers">Header fields.</param>
        /// <returns>Encoded QPACK payload.</returns>
        public static byte[] Encode(IReadOnlyList<Http3HeaderField> headers)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            List<byte> output = new List<byte>();
            EncodeInteger(output, 0, 8, 0x00);
            EncodeInteger(output, 0, 7, 0x00);

            for (int i = 0; i < headers.Count; i++)
            {
                Http3HeaderField header = headers[i];
                if (header == null) throw new ArgumentNullException(nameof(headers), "Header collection contains a null entry.");

                int exactIndex = QpackStaticTable.FindExact(header.Name, header.Value);
                if (exactIndex >= 0)
                {
                    EncodeInteger(output, exactIndex, 6, 0xC0);
                    continue;
                }

                int nameIndex = QpackStaticTable.FindName(header.Name);
                if (nameIndex >= 0)
                {
                    EncodeLiteralFieldLineWithNameReference(output, header, nameIndex);
                    continue;
                }

                EncodeLiteralFieldLineWithLiteralName(output, header);
            }

            return output.ToArray();
        }

        private static void DecodeHeaderBlockPrefix(byte[] payload, ref int offset)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            int requiredInsertCount = DecodeInteger(payload, ref offset, 8);
            if (requiredInsertCount != 0)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Dynamic-table QPACK references are not supported by the current transport.");
            }

            if (offset >= payload.Length)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "QPACK header-block prefix is truncated.");
            }

            bool deltaBaseNegative = (payload[offset] & 0x80) == 0x80;
            int deltaBase = DecodeInteger(payload, ref offset, 7);
            if (deltaBaseNegative || deltaBase != 0)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Non-zero QPACK delta-base values are not supported by the current transport.");
            }
        }

        private static Http3HeaderField DecodeIndexedFieldLine(byte[] payload, ref int offset)
        {
            bool isStaticReference = (payload[offset] & 0x40) == 0x40;
            if (!isStaticReference)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Dynamic-table QPACK indexed fields are not supported by the current transport.");
            }

            int index = DecodeInteger(payload, ref offset, 6);
            return QpackStaticTable.GetByIndex(index);
        }

        private static Http3HeaderField DecodeLiteralFieldLineWithNameReference(byte[] payload, ref int offset)
        {
            bool isStaticReference = (payload[offset] & 0x10) == 0x10;
            if (!isStaticReference)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Dynamic-table QPACK name references are not supported by the current transport.");
            }

            int index = DecodeInteger(payload, ref offset, 4);
            Http3HeaderField referencedHeader = QpackStaticTable.GetByIndex(index);
            string value = DecodeString(payload, ref offset);

            Http3HeaderField decoded = new Http3HeaderField();
            decoded.Name = referencedHeader.Name;
            decoded.Value = value;
            return decoded;
        }

        private static Http3HeaderField DecodeLiteralFieldLineWithLiteralName(byte[] payload, ref int offset)
        {
            int marker = payload[offset];
            bool neverIndexed = (marker & 0x10) == 0x10;
            if (neverIndexed)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "Unsupported QPACK never-indexed literal field was received.");
            }

            string name = DecodeString(payload, ref offset, 3);
            string value = DecodeString(payload, ref offset);

            Http3HeaderField decoded = new Http3HeaderField();
            decoded.Name = name;
            decoded.Value = value;
            return decoded;
        }

        private static void EncodeLiteralFieldLineWithNameReference(List<byte> output, Http3HeaderField header, int nameIndex)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (nameIndex < 0) throw new ArgumentOutOfRangeException(nameof(nameIndex));

            EncodeInteger(output, nameIndex, 4, 0x50);
            EncodeString(output, header.Value);
        }

        private static void EncodeLiteralFieldLineWithLiteralName(List<byte> output, Http3HeaderField header)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (header == null) throw new ArgumentNullException(nameof(header));

            EncodeString(output, header.Name, 3, 0x20);
            EncodeString(output, header.Value);
        }

        private static string DecodeString(byte[] payload, ref int offset)
        {
            return DecodeString(payload, ref offset, 7);
        }

        private static string DecodeString(byte[] payload, ref int offset, int prefixBits)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (offset >= payload.Length) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "QPACK string length is truncated.");

            bool huffmanEncoded = (payload[offset] & 0x80) == 0x80;
            int length = DecodeInteger(payload, ref offset, prefixBits);
            if (length < 0 || (offset + length) > payload.Length)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, "QPACK string payload is truncated.");
            }

            byte[] stringPayload = new byte[length];
            Buffer.BlockCopy(payload, offset, stringPayload, 0, length);
            offset += length;

            try
            {
                return huffmanEncoded ? HttpFieldHuffmanDecoder.Decode(stringPayload) : Encoding.UTF8.GetString(stringPayload);
            }
            catch (InvalidOperationException e)
            {
                throw new Http3ProtocolException(Http3ErrorCode.MessageError, e.Message);
            }
        }

        private static void EncodeString(List<byte> output, string value)
        {
            EncodeString(output, value, 7, 0x00);
        }

        private static void EncodeString(List<byte> output, string value, int prefixBits, int prefixMask)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (value == null) throw new ArgumentNullException(nameof(value));

            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            EncodeInteger(output, stringBytes.Length, prefixBits, prefixMask);

            for (int i = 0; i < stringBytes.Length; i++)
            {
                output.Add(stringBytes[i]);
            }
        }

        private static int DecodeInteger(byte[] payload, ref int offset, int prefixBits)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (offset >= payload.Length) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "QPACK integer is truncated.");

            int prefixMask = (1 << prefixBits) - 1;
            int value = payload[offset] & prefixMask;
            offset++;

            if (value < prefixMask) return value;

            int multiplier = 0;
            while (true)
            {
                if (offset >= payload.Length) throw new Http3ProtocolException(Http3ErrorCode.MessageError, "QPACK integer continuation is truncated.");

                byte nextByte = payload[offset++];
                value += (nextByte & 0x7F) << multiplier;
                if ((nextByte & 0x80) != 0x80) break;
                multiplier += 7;
            }

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
