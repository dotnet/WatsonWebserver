namespace WatsonWebserver.Core.Hpack
{
    using System;
    using System.Collections.Generic;
    using WatsonWebserver.Core.Http2;


    /// <summary>
    /// HPACK decoder state for a single connection.
    /// </summary>
    public sealed class HpackDecoderContext
    {
        /// <summary>
        /// Instantiate the decoder context.
        /// </summary>
        /// <param name="maxDynamicTableSize">Maximum dynamic-table size.</param>
        public HpackDecoderContext(int maxDynamicTableSize = 0)
        {
            if (maxDynamicTableSize < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicTableSize));
            _AllowedMaxDynamicTableSize = maxDynamicTableSize;
            _MaxDynamicTableSize = maxDynamicTableSize;
        }

        /// <summary>
        /// Retrieve an entry from the HPACK index space.
        /// </summary>
        /// <param name="index">1-based HPACK index.</param>
        /// <returns>Header field.</returns>
        public HpackHeaderField GetByIndex(int index)
        {
            if (index < 1) throw new ArgumentOutOfRangeException(nameof(index));

            if (index <= HpackStaticTable.EntryCount)
            {
                return HpackStaticTable.GetByIndex(index);
            }

            int dynamicIndex = index - HpackStaticTable.EntryCount - 1;
            if (dynamicIndex < 0 || dynamicIndex >= _Entries.Count) throw new ArgumentOutOfRangeException(nameof(index));

            HpackDynamicTableEntry entry = _Entries[dynamicIndex];
            HpackHeaderField field = new HpackHeaderField();
            field.Name = entry.Name;
            field.Value = entry.Value;
            return field;
        }

        /// <summary>
        /// Update the decoder maximum table size.
        /// </summary>
        /// <param name="maxDynamicTableSize">New maximum size.</param>
        public void UpdateMaxDynamicTableSize(int maxDynamicTableSize)
        {
            if (maxDynamicTableSize < 0) throw new ArgumentOutOfRangeException(nameof(maxDynamicTableSize));
            if (maxDynamicTableSize > _AllowedMaxDynamicTableSize)
            {
                throw new Http2ProtocolException(Http2ErrorCode.CompressionError, "HPACK dynamic table size update exceeds the configured maximum.");
            }

            _MaxDynamicTableSize = maxDynamicTableSize;
            EvictToFit();
        }

        /// <summary>
        /// Insert a new entry at the start of the dynamic table.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        public void Add(string name, string value)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            HpackDynamicTableEntry entry = new HpackDynamicTableEntry();
            entry.Name = name;
            entry.Value = value;

            if (entry.Size > _MaxDynamicTableSize)
            {
                _Entries.Clear();
                _CurrentSize = 0;
                return;
            }

            _Entries.Insert(0, entry);
            _CurrentSize += entry.Size;
            EvictToFit();
        }

        private void EvictToFit()
        {
            while (_CurrentSize > _MaxDynamicTableSize && _Entries.Count > 0)
            {
                HpackDynamicTableEntry removed = _Entries[_Entries.Count - 1];
                _Entries.RemoveAt(_Entries.Count - 1);
                _CurrentSize -= removed.Size;
            }
        }

        private readonly List<HpackDynamicTableEntry> _Entries = new List<HpackDynamicTableEntry>();
        private readonly int _AllowedMaxDynamicTableSize;
        private int _MaxDynamicTableSize;
        private int _CurrentSize = 0;
    }
}
