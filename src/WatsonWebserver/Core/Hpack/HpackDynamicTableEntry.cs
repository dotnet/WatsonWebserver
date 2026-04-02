namespace WatsonWebserver.Core.Hpack
{
    using System;
    using System.Text;

    /// <summary>
    /// HPACK dynamic-table entry.
    /// </summary>
    internal sealed class HpackDynamicTableEntry
    {
        /// <summary>
        /// Header name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Header value.
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// RFC 7541 dynamic-table size.
        /// </summary>
        public int Size
        {
            get
            {
                return 32 + Encoding.UTF8.GetByteCount(Name ?? String.Empty) + Encoding.UTF8.GetByteCount(Value ?? String.Empty);
            }
        }
    }
}
