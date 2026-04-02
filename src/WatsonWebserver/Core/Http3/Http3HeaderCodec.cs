namespace WatsonWebserver.Core.Http3
{
    using System;
    using System.Collections.Generic;

    using WatsonWebserver.Core.Qpack;

    /// <summary>
    /// HTTP/3 header-block codec backed by QPACK static-table-plus-literals encoding.
    /// </summary>
    public static class Http3HeaderCodec
    {
        /// <summary>
        /// Encode header fields.
        /// </summary>
        /// <param name="headers">Header fields.</param>
        /// <returns>Encoded header block.</returns>
        public static byte[] Encode(IReadOnlyList<Http3HeaderField> headers)
        {
            return QpackCodec.Encode(headers);
        }

        /// <summary>
        /// Decode a header block.
        /// </summary>
        /// <param name="payload">Encoded header block.</param>
        /// <returns>Decoded headers.</returns>
        public static List<Http3HeaderField> Decode(byte[] payload)
        {
            return QpackCodec.Decode(payload);
        }
    }
}
