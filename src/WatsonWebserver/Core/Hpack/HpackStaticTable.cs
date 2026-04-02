namespace WatsonWebserver.Core.Hpack
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// HPACK static table entries used for minimal HTTP/2 header encoding and decoding.
    /// </summary>
    public static class HpackStaticTable
    {
        /// <summary>
        /// Number of static table entries.
        /// </summary>
        public static int EntryCount
        {
            get
            {
                return _Entries.Count;
            }
        }

        /// <summary>
        /// Retrieve a header field by HPACK static-table index.
        /// </summary>
        /// <param name="index">1-based HPACK index.</param>
        /// <returns>Header field.</returns>
        public static HpackHeaderField GetByIndex(int index)
        {
            if (index < 1 || index > _Entries.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _Entries[index - 1];
        }

        /// <summary>
        /// Find an exact header field match in the static table.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>1-based HPACK index or zero when not found.</returns>
        public static int FindExact(string name, string value)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            for (int i = 0; i < _Entries.Count; i++)
            {
                if (String.Equals(_Entries[i].Name, name, StringComparison.InvariantCultureIgnoreCase)
                    && String.Equals(_Entries[i].Value, value, StringComparison.InvariantCulture))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Find a header name in the static table.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <returns>1-based HPACK index or zero when not found.</returns>
        public static int FindName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            for (int i = 0; i < _Entries.Count; i++)
            {
                if (String.Equals(_Entries[i].Name, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static readonly List<HpackHeaderField> _Entries = new List<HpackHeaderField>
        {
            new HpackHeaderField { Name = ":authority", Value = String.Empty },
            new HpackHeaderField { Name = ":method", Value = "GET" },
            new HpackHeaderField { Name = ":method", Value = "POST" },
            new HpackHeaderField { Name = ":path", Value = "/" },
            new HpackHeaderField { Name = ":path", Value = "/index.html" },
            new HpackHeaderField { Name = ":scheme", Value = "http" },
            new HpackHeaderField { Name = ":scheme", Value = "https" },
            new HpackHeaderField { Name = ":status", Value = "200" },
            new HpackHeaderField { Name = ":status", Value = "204" },
            new HpackHeaderField { Name = ":status", Value = "206" },
            new HpackHeaderField { Name = ":status", Value = "304" },
            new HpackHeaderField { Name = ":status", Value = "400" },
            new HpackHeaderField { Name = ":status", Value = "404" },
            new HpackHeaderField { Name = ":status", Value = "500" },
            new HpackHeaderField { Name = "accept-charset", Value = String.Empty },
            new HpackHeaderField { Name = "accept-encoding", Value = "gzip, deflate" },
            new HpackHeaderField { Name = "accept-language", Value = String.Empty },
            new HpackHeaderField { Name = "accept-ranges", Value = String.Empty },
            new HpackHeaderField { Name = "accept", Value = String.Empty },
            new HpackHeaderField { Name = "access-control-allow-origin", Value = String.Empty },
            new HpackHeaderField { Name = "age", Value = String.Empty },
            new HpackHeaderField { Name = "allow", Value = String.Empty },
            new HpackHeaderField { Name = "authorization", Value = String.Empty },
            new HpackHeaderField { Name = "cache-control", Value = String.Empty },
            new HpackHeaderField { Name = "content-disposition", Value = String.Empty },
            new HpackHeaderField { Name = "content-encoding", Value = String.Empty },
            new HpackHeaderField { Name = "content-language", Value = String.Empty },
            new HpackHeaderField { Name = "content-length", Value = String.Empty },
            new HpackHeaderField { Name = "content-location", Value = String.Empty },
            new HpackHeaderField { Name = "content-range", Value = String.Empty },
            new HpackHeaderField { Name = "content-type", Value = String.Empty },
            new HpackHeaderField { Name = "cookie", Value = String.Empty },
            new HpackHeaderField { Name = "date", Value = String.Empty },
            new HpackHeaderField { Name = "etag", Value = String.Empty },
            new HpackHeaderField { Name = "expect", Value = String.Empty },
            new HpackHeaderField { Name = "expires", Value = String.Empty },
            new HpackHeaderField { Name = "from", Value = String.Empty },
            new HpackHeaderField { Name = "host", Value = String.Empty },
            new HpackHeaderField { Name = "if-match", Value = String.Empty },
            new HpackHeaderField { Name = "if-modified-since", Value = String.Empty },
            new HpackHeaderField { Name = "if-none-match", Value = String.Empty },
            new HpackHeaderField { Name = "if-range", Value = String.Empty },
            new HpackHeaderField { Name = "if-unmodified-since", Value = String.Empty },
            new HpackHeaderField { Name = "last-modified", Value = String.Empty },
            new HpackHeaderField { Name = "link", Value = String.Empty },
            new HpackHeaderField { Name = "location", Value = String.Empty },
            new HpackHeaderField { Name = "max-forwards", Value = String.Empty },
            new HpackHeaderField { Name = "proxy-authenticate", Value = String.Empty },
            new HpackHeaderField { Name = "proxy-authorization", Value = String.Empty },
            new HpackHeaderField { Name = "range", Value = String.Empty },
            new HpackHeaderField { Name = "referer", Value = String.Empty },
            new HpackHeaderField { Name = "refresh", Value = String.Empty },
            new HpackHeaderField { Name = "retry-after", Value = String.Empty },
            new HpackHeaderField { Name = "server", Value = String.Empty },
            new HpackHeaderField { Name = "set-cookie", Value = String.Empty },
            new HpackHeaderField { Name = "strict-transport-security", Value = String.Empty },
            new HpackHeaderField { Name = "transfer-encoding", Value = String.Empty },
            new HpackHeaderField { Name = "user-agent", Value = String.Empty },
            new HpackHeaderField { Name = "vary", Value = String.Empty },
            new HpackHeaderField { Name = "via", Value = String.Empty },
            new HpackHeaderField { Name = "www-authenticate", Value = String.Empty }
        };
    }
}
