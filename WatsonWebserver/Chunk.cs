using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// A chunk of data, used when reading from a request using chunked transfer-encoding.
    /// </summary>
    public class Chunk
    {
        /// <summary>
        /// Length of the data.
        /// </summary>
        public int Length = 0;

        /// <summary>
        /// Data.
        /// </summary>
        public byte[] Data = null;

        /// <summary>
        /// Any additional metadata that appears on the length line after the length hex value and semicolon.
        /// </summary>
        public string Metadata = null;

        /// <summary>
        /// Indicates whether or not this is the final chunk, i.e. the chunk length received was zero.
        /// </summary>
        public bool IsFinalChunk = false;

        internal Chunk()
        {
        }
    }
}
