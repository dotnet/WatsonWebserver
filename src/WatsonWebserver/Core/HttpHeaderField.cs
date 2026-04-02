namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Specialized;
    using System.Text;

    /// <summary>
    /// Header field helper used to defer request header collection materialization.
    /// </summary>
    public readonly struct HttpHeaderField
    {
        /// <summary>
        /// Header name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Header value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Instantiate the header field.
        /// </summary>
        /// <param name="name">Header name.</param>
        /// <param name="value">Header value.</param>
        public HttpHeaderField(string name, string value)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? String.Empty;
        }

        /// <summary>
        /// Materialize a name-value collection from header fields.
        /// </summary>
        /// <param name="fields">Header fields.</param>
        /// <returns>Name-value collection.</returns>
        public static NameValueCollection ToNameValueCollection(HttpHeaderField[] fields)
        {
            NameValueCollection headers = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            if (fields == null || fields.Length < 1) return headers;

            for (int i = 0; i < fields.Length; i++)
            {
                headers.Add(fields[i].Name, fields[i].Value);
            }

            return headers;
        }

        /// <summary>
        /// Determine if a header exists in the supplied field set.
        /// </summary>
        /// <param name="fields">Header fields.</param>
        /// <param name="key">Header name.</param>
        /// <returns>True if present.</returns>
        public static bool Exists(HttpHeaderField[] fields, string key)
        {
            if (fields == null || fields.Length < 1 || String.IsNullOrEmpty(key)) return false;

            for (int i = 0; i < fields.Length; i++)
            {
                if (String.Equals(fields[i].Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header value from the supplied field set.
        /// </summary>
        /// <param name="fields">Header fields.</param>
        /// <param name="key">Header name.</param>
        /// <returns>Header value if present.</returns>
        public static string GetValue(HttpHeaderField[] fields, string key)
        {
            if (fields == null || fields.Length < 1 || String.IsNullOrEmpty(key)) return null;

            string firstValue = null;
            StringBuilder combined = null;

            for (int i = 0; i < fields.Length; i++)
            {
                if (!String.Equals(fields[i].Name, key, StringComparison.OrdinalIgnoreCase)) continue;

                if (firstValue == null)
                {
                    firstValue = fields[i].Value;
                }
                else
                {
                    if (combined == null)
                    {
                        combined = new StringBuilder(firstValue);
                    }

                    combined.Append(',');
                    combined.Append(fields[i].Value);
                }
            }

            if (combined != null) return combined.ToString();
            return firstValue;
        }
    }
}
