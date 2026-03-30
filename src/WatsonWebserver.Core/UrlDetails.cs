namespace WatsonWebserver.Core
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Timestamps;

    /// <summary>
    /// URL details.
    /// </summary>
    public class UrlDetails
    {
        #region Public-Members

        /// <summary>
        /// URI.  This value may be null based on how the UrlDetails object was initialized.
        /// </summary>
        public Uri Uri
        {
            get
            {
                if (_Uri == null && !String.IsNullOrEmpty(_Full))
                {
                    _Uri = new Uri(_Full);
                }

                return _Uri;
            }
        }

        /// <summary>
        /// Scheme name for the URI.
        /// </summary>
        public string Scheme
        {
            get
            {
                if (!String.IsNullOrEmpty(_Scheme)) return _Scheme;
                if (Uri != null) return Uri.Scheme;
                return null;
            }
        }

        /// <summary>
        /// Host name from the URI.
        /// </summary>
        public string Host
        {
            get
            {
                if (!String.IsNullOrEmpty(_Host)) return _Host;
                if (!String.IsNullOrEmpty(_Authority))
                {
                    return GetAuthorityUri().Host;
                }
                if (Uri != null) return Uri.Host;
                return null;
            }
        }

        /// <summary>
        /// Port number from the URI.
        /// </summary>
        public int? Port
        {
            get
            {
                if (_Port > 0) return _Port;
                if (!String.IsNullOrEmpty(_Authority))
                {
                    return GetAuthorityUri().Port;
                }
                if (Uri != null) return Uri.Port;
                return null;
            }
        }

        /// <summary>
        /// Full URL.
        /// </summary>
        public string Full
        {
            get
            {
                if (String.IsNullOrEmpty(_Full)
                    && !String.IsNullOrEmpty(_RawWithQuery)
                    && !String.IsNullOrEmpty(_Scheme))
                {
                    string rawWithQuery = _RawWithQuery;
                    if (!rawWithQuery.StartsWith("/")) rawWithQuery = "/" + rawWithQuery;

                    if (!String.IsNullOrEmpty(_Authority))
                    {
                        _Full = _Scheme + "://" + _Authority + rawWithQuery;
                    }
                    else if (!String.IsNullOrEmpty(_Host) && _Port > 0)
                    {
                        _Full = _Scheme + "://" + _Host + ":" + _Port + rawWithQuery;
                    }
                }

                return _Full;
            }
            set
            {
                _Full = value;
                _Uri = null;
            }
        }

        /// <summary>
        /// Raw URL with query.
        /// </summary>
        public string RawWithQuery
        {
            get
            {
                return _RawWithQuery;
            }
            set
            {
                _RawWithQuery = value;
                _RawWithoutQuery = null;
                _NormalizedRawWithoutQuery = null;
                _Elements = null;
            }
        }

        /// <summary>
        /// Raw URL without query.
        /// </summary>
        public string RawWithoutQuery
        {
            get
            {
                if (_RawWithoutQuery != null) return _RawWithoutQuery;
                if (String.IsNullOrEmpty(_RawWithQuery)) return null;

                int queryIndex = _RawWithQuery.IndexOf("?", StringComparison.Ordinal);
                if (queryIndex >= 0) _RawWithoutQuery = _RawWithQuery.Substring(0, queryIndex);
                else _RawWithoutQuery = _RawWithQuery;

                return _RawWithoutQuery;
            }
        }

        /// <summary>
        /// Raw URL without query, normalized for routing comparisons.
        /// </summary>
        public string NormalizedRawWithoutQuery
        {
            get
            {
                if (_NormalizedRawWithoutQuery != null) return _NormalizedRawWithoutQuery;

                string rawWithoutQuery = RawWithoutQuery;
                if (String.IsNullOrEmpty(rawWithoutQuery)) return null;

                _NormalizedRawWithoutQuery = NormalizeRawPathForRouting(rawWithoutQuery);
                return _NormalizedRawWithoutQuery;
            }
        }

        /// <summary>
        /// Raw URL elements.
        /// </summary>
        public string[] Elements
        {
            get
            {
                if (_Elements != null) return _Elements;
                string rawUrl = RawWithoutQuery;

                if (!String.IsNullOrEmpty(rawUrl))
                {
                    while (rawUrl.Contains("//")) rawUrl = rawUrl.Replace("//", "/");
                    while (rawUrl.StartsWith("/")) rawUrl = rawUrl.Substring(1);
                    while (rawUrl.EndsWith("/")) rawUrl = rawUrl.Substring(0, rawUrl.Length - 1);
                    string[] encoded = rawUrl.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (encoded != null && encoded.Length > 0)
                    {
                        string[] decoded = new string[encoded.Length];
                        for (int i = 0; i < encoded.Length; i++)
                        {
                            decoded[i] = WebUtility.UrlDecode(encoded[i]);
                        }

                        _Elements = decoded;
                        return _Elements;
                    }
                }

                _Elements = Array.Empty<string>();
                return _Elements;
            }
        }

        /// <summary>
        /// Parameters found within the URL, if using parameter routes.
        /// </summary>
        public NameValueCollection Parameters
        {
            get
            {
                if (_Parameters == null) _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                return _Parameters;
            }
            set
            {
                if (value == null) _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                else _Parameters = value;
            }
        }

        #endregion 

        #region Private-Members

        private Uri _Uri = null;
        private string _Scheme = null;
        private string _Authority = null;
        private string _Host = null;
        private int _Port = 0;
        private string _Full = null;
        private string _RawWithQuery = null;
        private string _RawWithoutQuery = null;
        private string _NormalizedRawWithoutQuery = null;
        private string[] _Elements = null;
        private NameValueCollection _Parameters = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// URL details.
        /// </summary>
        public UrlDetails()
        {

        }

        /// <summary>
        /// Normalize a raw URL path for routing comparisons.
        /// </summary>
        /// <param name="rawPath">Raw path.</param>
        /// <returns>Normalized path.</returns>
        internal static string NormalizeRawPathForRouting(string rawPath)
        {
            if (String.IsNullOrEmpty(rawPath)) return rawPath;
            if (IsNormalizedRoutingPath(rawPath)) return rawPath;

            string normalized = rawPath;
            if (!normalized.StartsWith("/")) normalized = "/" + normalized;
            if (!normalized.EndsWith("/")) normalized += "/";
            return normalized.ToLowerInvariant();
        }

        /// <summary>
        /// URL details.
        /// </summary>
        /// <param name="fullUrl">Full URL.</param>
        /// <param name="rawUrl">Raw URL.</param>
        public UrlDetails(string fullUrl, string rawUrl)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            Full = fullUrl;
            RawWithQuery = rawUrl;
        }

        /// <summary>
        /// URL details.
        /// </summary>
        /// <param name="rawUrl">Raw URL.</param>
        /// <param name="scheme">Scheme.</param>
        /// <param name="host">Host.</param>
        /// <param name="port">Port.</param>
        public UrlDetails(string rawUrl, string scheme, string host, int port)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));
            if (String.IsNullOrEmpty(scheme)) throw new ArgumentNullException(nameof(scheme));
            if (String.IsNullOrEmpty(host)) throw new ArgumentNullException(nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            RawWithQuery = rawUrl;
            _Scheme = scheme;
            _Host = host;
            _Port = port;
        }

        /// <summary>
        /// URL details.
        /// </summary>
        /// <param name="rawUrl">Raw URL.</param>
        /// <param name="scheme">Scheme.</param>
        /// <param name="authority">Authority.</param>
        public UrlDetails(string rawUrl, string scheme, string authority)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));
            if (String.IsNullOrEmpty(scheme)) throw new ArgumentNullException(nameof(scheme));
            if (String.IsNullOrEmpty(authority)) throw new ArgumentNullException(nameof(authority));

            RawWithQuery = rawUrl;
            _Scheme = scheme;
            _Authority = authority;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Check if a parameter exists.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True if exists.</returns>
        public bool ParameterExists(string key)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (_Parameters == null) return false;
            if (_Parameters.AllKeys.Contains(key)) return true;
            return false;
        }

        #endregion

        #region Private-Methods

        private Uri GetAuthorityUri()
        {
            if (_AuthorityUri == null)
            {
                _AuthorityUri = new Uri(_Scheme + "://" + _Authority);
            }

            return _AuthorityUri;
        }

        private static bool IsNormalizedRoutingPath(string rawPath)
        {
            if (String.IsNullOrEmpty(rawPath)) return false;
            if (!rawPath.StartsWith("/")) return false;
            if (!rawPath.EndsWith("/")) return false;

            for (int i = 0; i < rawPath.Length; i++)
            {
                if (Char.IsUpper(rawPath[i])) return false;
            }

            return true;
        }

        #endregion

        private Uri _AuthorityUri = null;
    }
}
