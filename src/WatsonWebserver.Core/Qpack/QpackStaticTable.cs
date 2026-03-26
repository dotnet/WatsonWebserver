namespace WatsonWebserver.Core.Qpack
{
    using System;
    using System.Collections.Generic;
    using WatsonWebserver.Core.Http3;

    /// <summary>
    /// Minimal QPACK static table entries used by the HTTP/3 transport.
    /// </summary>
    public static class QpackStaticTable
    {
        /// <summary>
        /// Retrieve a header field by zero-based QPACK static-table index.
        /// </summary>
        /// <param name="index">Zero-based QPACK index.</param>
        /// <returns>Header field.</returns>
        public static Http3HeaderField GetByIndex(int index)
        {
            if (index < 0 || index >= _Entries.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return _Entries[index];
        }

        /// <summary>
        /// Find an exact header field match in the static table.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>Zero-based QPACK index or -1 when not found.</returns>
        public static int FindExact(string name, string value)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (value == null) throw new ArgumentNullException(nameof(value));

            for (int i = 0; i < _Entries.Count; i++)
            {
                if (String.Equals(_Entries[i].Name, name, StringComparison.InvariantCultureIgnoreCase)
                    && String.Equals(_Entries[i].Value, value, StringComparison.InvariantCulture))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Find a header name in the static table.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <returns>Zero-based QPACK index or -1 when not found.</returns>
        public static int FindName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            for (int i = 0; i < _Entries.Count; i++)
            {
                if (String.Equals(_Entries[i].Name, name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static readonly List<Http3HeaderField> _Entries = new List<Http3HeaderField>
        {
            new Http3HeaderField { Name = ":authority", Value = String.Empty },
            new Http3HeaderField { Name = ":path", Value = "/" },
            new Http3HeaderField { Name = "age", Value = "0" },
            new Http3HeaderField { Name = "content-disposition", Value = String.Empty },
            new Http3HeaderField { Name = "content-length", Value = "0" },
            new Http3HeaderField { Name = "cookie", Value = String.Empty },
            new Http3HeaderField { Name = "date", Value = String.Empty },
            new Http3HeaderField { Name = "etag", Value = String.Empty },
            new Http3HeaderField { Name = "if-modified-since", Value = String.Empty },
            new Http3HeaderField { Name = "if-none-match", Value = String.Empty },
            new Http3HeaderField { Name = "last-modified", Value = String.Empty },
            new Http3HeaderField { Name = "link", Value = String.Empty },
            new Http3HeaderField { Name = "location", Value = String.Empty },
            new Http3HeaderField { Name = "referer", Value = String.Empty },
            new Http3HeaderField { Name = "set-cookie", Value = String.Empty },
            new Http3HeaderField { Name = ":method", Value = "CONNECT" },
            new Http3HeaderField { Name = ":method", Value = "DELETE" },
            new Http3HeaderField { Name = ":method", Value = "GET" },
            new Http3HeaderField { Name = ":method", Value = "HEAD" },
            new Http3HeaderField { Name = ":method", Value = "OPTIONS" },
            new Http3HeaderField { Name = ":method", Value = "POST" },
            new Http3HeaderField { Name = ":method", Value = "PUT" },
            new Http3HeaderField { Name = ":scheme", Value = "http" },
            new Http3HeaderField { Name = ":scheme", Value = "https" },
            new Http3HeaderField { Name = ":status", Value = "103" },
            new Http3HeaderField { Name = ":status", Value = "200" },
            new Http3HeaderField { Name = ":status", Value = "304" },
            new Http3HeaderField { Name = ":status", Value = "404" },
            new Http3HeaderField { Name = ":status", Value = "503" },
            new Http3HeaderField { Name = "accept", Value = "*/*" },
            new Http3HeaderField { Name = "accept", Value = "application/dns-message" },
            new Http3HeaderField { Name = "accept-encoding", Value = "gzip, deflate, br" },
            new Http3HeaderField { Name = "accept-ranges", Value = "bytes" },
            new Http3HeaderField { Name = "access-control-allow-headers", Value = "cache-control" },
            new Http3HeaderField { Name = "access-control-allow-headers", Value = "content-type" },
            new Http3HeaderField { Name = "access-control-allow-origin", Value = "*" },
            new Http3HeaderField { Name = "cache-control", Value = "max-age=0" },
            new Http3HeaderField { Name = "cache-control", Value = "max-age=2592000" },
            new Http3HeaderField { Name = "cache-control", Value = "max-age=604800" },
            new Http3HeaderField { Name = "cache-control", Value = "no-cache" },
            new Http3HeaderField { Name = "cache-control", Value = "no-store" },
            new Http3HeaderField { Name = "cache-control", Value = "public, max-age=31536000" },
            new Http3HeaderField { Name = "content-encoding", Value = "br" },
            new Http3HeaderField { Name = "content-encoding", Value = "gzip" },
            new Http3HeaderField { Name = "content-type", Value = "application/dns-message" },
            new Http3HeaderField { Name = "content-type", Value = "application/javascript" },
            new Http3HeaderField { Name = "content-type", Value = "application/json" },
            new Http3HeaderField { Name = "content-type", Value = "application/x-www-form-urlencoded" },
            new Http3HeaderField { Name = "content-type", Value = "image/gif" },
            new Http3HeaderField { Name = "content-type", Value = "image/jpeg" },
            new Http3HeaderField { Name = "content-type", Value = "image/png" },
            new Http3HeaderField { Name = "content-type", Value = "text/css" },
            new Http3HeaderField { Name = "content-type", Value = "text/html; charset=utf-8" },
            new Http3HeaderField { Name = "content-type", Value = "text/plain" },
            new Http3HeaderField { Name = "content-type", Value = "text/plain;charset=utf-8" },
            new Http3HeaderField { Name = "range", Value = "bytes=0-" },
            new Http3HeaderField { Name = "strict-transport-security", Value = "max-age=31536000" },
            new Http3HeaderField { Name = "strict-transport-security", Value = "max-age=31536000; includesubdomains" },
            new Http3HeaderField { Name = "strict-transport-security", Value = "max-age=31536000; includesubdomains; preload" },
            new Http3HeaderField { Name = "vary", Value = "accept-encoding" },
            new Http3HeaderField { Name = "vary", Value = "origin" },
            new Http3HeaderField { Name = "x-content-type-options", Value = "nosniff" },
            new Http3HeaderField { Name = "x-xss-protection", Value = "1; mode=block" },
            new Http3HeaderField { Name = ":status", Value = "100" },
            new Http3HeaderField { Name = ":status", Value = "204" },
            new Http3HeaderField { Name = ":status", Value = "206" },
            new Http3HeaderField { Name = ":status", Value = "302" },
            new Http3HeaderField { Name = ":status", Value = "400" },
            new Http3HeaderField { Name = ":status", Value = "403" },
            new Http3HeaderField { Name = ":status", Value = "421" },
            new Http3HeaderField { Name = ":status", Value = "425" },
            new Http3HeaderField { Name = ":status", Value = "500" },
            new Http3HeaderField { Name = "accept-language", Value = String.Empty },
            new Http3HeaderField { Name = "access-control-allow-credentials", Value = "FALSE" },
            new Http3HeaderField { Name = "access-control-allow-credentials", Value = "TRUE" },
            new Http3HeaderField { Name = "access-control-allow-headers", Value = "*" },
            new Http3HeaderField { Name = "access-control-allow-methods", Value = "get" },
            new Http3HeaderField { Name = "access-control-allow-methods", Value = "get, post, options" },
            new Http3HeaderField { Name = "access-control-allow-methods", Value = "options" },
            new Http3HeaderField { Name = "access-control-expose-headers", Value = "content-length" },
            new Http3HeaderField { Name = "access-control-request-headers", Value = "content-type" },
            new Http3HeaderField { Name = "access-control-request-method", Value = "get" },
            new Http3HeaderField { Name = "access-control-request-method", Value = "post" },
            new Http3HeaderField { Name = "alt-svc", Value = "clear" },
            new Http3HeaderField { Name = "authorization", Value = String.Empty },
            new Http3HeaderField { Name = "content-security-policy", Value = "script-src 'none'; object-src 'none'; base-uri 'none'" },
            new Http3HeaderField { Name = "early-data", Value = "1" },
            new Http3HeaderField { Name = "expect-ct", Value = String.Empty },
            new Http3HeaderField { Name = "forwarded", Value = String.Empty },
            new Http3HeaderField { Name = "if-range", Value = String.Empty },
            new Http3HeaderField { Name = "origin", Value = String.Empty },
            new Http3HeaderField { Name = "purpose", Value = "prefetch" },
            new Http3HeaderField { Name = "server", Value = String.Empty },
            new Http3HeaderField { Name = "timing-allow-origin", Value = "*" },
            new Http3HeaderField { Name = "upgrade-insecure-requests", Value = "1" },
            new Http3HeaderField { Name = "user-agent", Value = String.Empty },
            new Http3HeaderField { Name = "x-forwarded-for", Value = String.Empty },
            new Http3HeaderField { Name = "x-frame-options", Value = "deny" },
            new Http3HeaderField { Name = "x-frame-options", Value = "sameorigin" }
        };
    }
}
