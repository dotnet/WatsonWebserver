namespace WatsonWebserver.Core.Hpack
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Shared HPACK/QPACK Huffman decoder.
    /// </summary>
    internal static class HttpFieldHuffmanDecoder
    {
        /// <summary>
        /// Decode HPACK/QPACK Huffman-encoded bytes into a UTF-8 string.
        /// </summary>
        /// <param name="payload">Encoded bytes.</param>
        /// <returns>Decoded string.</returns>
        public static string Decode(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            List<byte> output = new List<byte>();
            uint currentCode = 0;
            int currentLength = 0;

            for (int i = 0; i < payload.Length; i++)
            {
                byte currentByte = payload[i];

                for (int bit = 7; bit >= 0; bit--)
                {
                    currentCode = (currentCode << 1) | (uint)((currentByte >> bit) & 0x01);
                    currentLength++;

                    if (currentLength > 30)
                    {
                        throw new InvalidOperationException("HTTP field Huffman code exceeds the maximum legal symbol length.");
                    }

                    long lookupKey = CreateLookupKey(currentLength, currentCode);
                    if (_DecodeTable.TryGetValue(lookupKey, out int symbol))
                    {
                        if (symbol == 256)
                        {
                            throw new InvalidOperationException("HTTP field Huffman EOS symbol must not appear within encoded data.");
                        }

                        output.Add((byte)symbol);
                        currentCode = 0;
                        currentLength = 0;
                    }
                }
            }

            if (currentLength > 0)
            {
                if (currentLength > 7)
                {
                    throw new InvalidOperationException("HTTP field Huffman padding exceeds the maximum legal length.");
                }

                uint expectedPadding = (uint)((1 << currentLength) - 1);
                if (currentCode != expectedPadding)
                {
                    throw new InvalidOperationException("HTTP field Huffman padding must be a prefix of EOS.");
                }
            }

            return Encoding.UTF8.GetString(output.ToArray());
        }

        private static long CreateLookupKey(int length, uint code)
        {
            return ((long)length << 32) | code;
        }

        private static Dictionary<long, int> BuildDecodeTable()
        {
            Dictionary<long, int> table = new Dictionary<long, int>();

            for (int i = 0; i < _Codes.Length; i++)
            {
                table[CreateLookupKey(_Lengths[i], _Codes[i])] = i;
            }

            return table;
        }

        private static readonly uint[] _Codes = new uint[]
        {
            0x1ff8u, 0x7fffd8u, 0xfffffe2u, 0xfffffe3u, 0xfffffe4u, 0xfffffe5u, 0xfffffe6u, 0xfffffe7u, 0xfffffe8u, 0xffffeau,
            0x3ffffffcu, 0xfffffe9u, 0xfffffeau, 0x3ffffffdu, 0xfffffebu, 0xfffffecu, 0xfffffedu, 0xfffffeeu, 0xfffffefu, 0xffffff0u,
            0xffffff1u, 0xffffff2u, 0x3ffffffeu, 0xffffff3u, 0xffffff4u, 0xffffff5u, 0xffffff6u, 0xffffff7u, 0xffffff8u, 0xffffff9u,
            0xffffffau, 0xffffffbu, 0x14u, 0x3f8u, 0x3f9u, 0xffau, 0x1ff9u, 0x15u, 0xf8u, 0x7fau,
            0x3fau, 0x3fbu, 0xf9u, 0x7fbu, 0xfau, 0x16u, 0x17u, 0x18u, 0x0u, 0x1u,
            0x2u, 0x19u, 0x1au, 0x1bu, 0x1cu, 0x1du, 0x1eu, 0x1fu, 0x5cu, 0xfbu,
            0x7ffcu, 0x20u, 0xffbu, 0x3fcu, 0x1ffau, 0x21u, 0x5du, 0x5eu, 0x5fu, 0x60u,
            0x61u, 0x62u, 0x63u, 0x64u, 0x65u, 0x66u, 0x67u, 0x68u, 0x69u, 0x6au,
            0x6bu, 0x6cu, 0x6du, 0x6eu, 0x6fu, 0x70u, 0x71u, 0x72u, 0xfcu, 0x73u,
            0xfdu, 0x1ffbu, 0x7fff0u, 0x1ffcu, 0x3ffcu, 0x22u, 0x7ffdu, 0x3u, 0x23u, 0x4u,
            0x24u, 0x5u, 0x25u, 0x26u, 0x27u, 0x6u, 0x74u, 0x75u, 0x28u, 0x29u,
            0x2au, 0x7u, 0x2bu, 0x76u, 0x2cu, 0x8u, 0x9u, 0x2du, 0x77u, 0x78u,
            0x79u, 0x7au, 0x7bu, 0x7ffeu, 0x7fcu, 0x3ffdu, 0x1ffdu, 0xffffffcu, 0xfffe6u, 0x3fffd2u,
            0xfffe7u, 0xfffe8u, 0x3fffd3u, 0x3fffd4u, 0x3fffd5u, 0x7fffd9u, 0x3fffd6u, 0x7fffdau, 0x7fffdbu, 0x7fffdcu,
            0x7fffddu, 0x7fffdeu, 0xffffebu, 0x7fffdfu, 0xffffecu, 0xffffedu, 0x3fffd7u, 0x7fffe0u, 0xffffeeu, 0x7fffe1u,
            0x7fffe2u, 0x7fffe3u, 0x7fffe4u, 0x1fffdcu, 0x3fffd8u, 0x7fffe5u, 0x3fffd9u, 0x7fffe6u, 0x7fffe7u, 0xffffefu,
            0x3fffdau, 0x1fffddu, 0xfffe9u, 0x3fffdbu, 0x3fffdcu, 0x7fffe8u, 0x7fffe9u, 0x1fffdeu, 0x7fffeau, 0x3fffddu,
            0x3fffdeu, 0xfffff0u, 0x1fffdfu, 0x3fffdfu, 0x7fffebu, 0x7fffecu, 0x1fffe0u, 0x1fffe1u, 0x3fffe0u, 0x1fffe2u,
            0x7fffedu, 0x3fffe1u, 0x7fffeeu, 0x7fffefu, 0xfffeau, 0x3fffe2u, 0x3fffe3u, 0x3fffe4u, 0x7ffff0u, 0x3fffe5u,
            0x3fffe6u, 0x7ffff1u, 0x3ffffe0u, 0x3ffffe1u, 0xfffebu, 0x7fff1u, 0x3fffe7u, 0x7ffff2u, 0x3fffe8u, 0x1ffffecu,
            0x3ffffe2u, 0x3ffffe3u, 0x3ffffe4u, 0x7ffffdeu, 0x7ffffdfu, 0x3ffffe5u, 0xfffff1u, 0x1ffffedu, 0x7fff2u, 0x1fffe3u,
            0x3ffffe6u, 0x7ffffe0u, 0x7ffffe1u, 0x3ffffe7u, 0x7ffffe2u, 0xfffff2u, 0x1fffe4u, 0x1fffe5u, 0x3ffffe8u, 0x3ffffe9u,
            0xffffffdu, 0x7ffffe3u, 0x7ffffe4u, 0x7ffffe5u, 0xfffecu, 0xfffff3u, 0xfffedu, 0x1fffe6u, 0x3fffe9u, 0x1fffe7u,
            0x1fffe8u, 0x7ffff3u, 0x3fffeau, 0x3fffebu, 0x1ffffeeu, 0x1ffffefu, 0xfffff4u, 0xfffff5u, 0x3ffffeau, 0x7ffff4u,
            0x3ffffebu, 0x7ffffe6u, 0x3ffffecu, 0x3ffffedu, 0x7ffffe7u, 0x7ffffe8u, 0x7ffffe9u, 0x7ffffeau, 0x7ffffebu, 0xffffffeu,
            0x7ffffecu, 0x7ffffedu, 0x7ffffeeu, 0x7ffffefu, 0x7fffff0u, 0x3ffffeeu, 0x3fffffffu
        };

        private static readonly int[] _Lengths = new int[]
        {
            13, 23, 28, 28, 28, 28, 28, 28, 28, 24,
            30, 28, 28, 30, 28, 28, 28, 28, 28, 28,
            28, 28, 30, 28, 28, 28, 28, 28, 28, 28,
            28, 28, 6, 10, 10, 12, 13, 6, 8, 11,
            10, 10, 8, 11, 8, 6, 6, 6, 5, 5,
            5, 6, 6, 6, 6, 6, 6, 6, 7, 8,
            15, 6, 12, 10, 13, 6, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 8, 7,
            8, 13, 19, 13, 14, 6, 15, 5, 6, 5,
            6, 5, 6, 6, 6, 5, 7, 7, 6, 6,
            6, 5, 6, 7, 6, 5, 5, 6, 7, 7,
            7, 7, 7, 15, 11, 14, 13, 28, 20, 22,
            20, 20, 22, 22, 22, 23, 22, 23, 23, 23,
            23, 23, 24, 23, 24, 24, 22, 23, 24, 23,
            23, 23, 23, 21, 22, 23, 22, 23, 23, 24,
            22, 21, 20, 22, 22, 23, 23, 21, 23, 22,
            22, 24, 21, 22, 23, 23, 21, 21, 22, 21,
            23, 22, 23, 23, 20, 22, 22, 22, 23, 22,
            22, 23, 26, 26, 20, 19, 22, 23, 22, 25,
            26, 26, 26, 27, 27, 26, 24, 25, 19, 21,
            26, 27, 27, 26, 27, 24, 21, 21, 26, 26,
            28, 27, 27, 27, 20, 24, 20, 21, 22, 21,
            21, 23, 22, 22, 25, 25, 24, 24, 26, 23,
            26, 27, 26, 26, 27, 27, 27, 27, 27, 28,
            27, 27, 27, 27, 27, 26, 30
        };

        private static readonly Dictionary<long, int> _DecodeTable = BuildDecodeTable();
    }
}
