﻿namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// A chunk of data, used when reading from a request where the Transfer-Encoding header includes 'chunked'.
    /// </summary>
    public class Chunk
    {
        /// <summary>
        /// Length of the data.
        /// </summary>
        public int Length
        {
            get
            {
                return _Length;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Length));
                _Length = value;
            }
        }

        /// <summary>
        /// Data.
        /// </summary>
        public byte[] Data { get; set; } = null;

        /// <summary>
        /// Any additional metadata that appears on the length line after the length hex value and semicolon.
        /// </summary>
        public string Metadata { get; set; } = null;

        /// <summary>
        /// Indicates whether or not this is the final chunk, i.e. the chunk length received was zero.
        /// </summary>
        public bool IsFinal { get; set; } = false;

        private int _Length = 0;

        /// <summary>
        /// A chunk of data, used when reading from a request where the Transfer-Encoding header includes 'chunked'.
        /// </summary>
        public Chunk()
        {
        }
    }
}
