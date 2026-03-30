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
    /// Query details.
    /// </summary>
    public class QueryDetails
    {
        #region Public-Members

        /// <summary>
        /// Querystring, excluding the leading '?'.
        /// </summary>
        public string Querystring
        {
            get
            {
                if (_QuerystringEvaluated) return _Querystring;

                if (!String.IsNullOrEmpty(_FullUrl))
                {
                    int queryIndex = _FullUrl.IndexOf("?", StringComparison.Ordinal);
                    if (queryIndex >= 0 && queryIndex < (_FullUrl.Length - 1))
                    {
                        _Querystring = _FullUrl.Substring(queryIndex + 1, _FullUrl.Length - queryIndex - 1);
                    }
                }

                _QuerystringEvaluated = true;
                return _Querystring;
            }
        }

        /// <summary>
        /// Query elements.
        /// </summary>
        public NameValueCollection Elements
        {
            get
            {
                if (_Elements != null) return _Elements;

                NameValueCollection ret = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                string qs = Querystring;
                if (!String.IsNullOrEmpty(qs))
                {
                    int queryStartIndex = 0;

                    while (queryStartIndex < qs.Length)
                    {
                        int queryEndIndex = qs.IndexOf('&', queryStartIndex);
                        if (queryEndIndex < 0) queryEndIndex = qs.Length;

                        if (queryEndIndex > queryStartIndex)
                        {
                            int keyValueSeparatorIndex = qs.IndexOf('=', queryStartIndex, queryEndIndex - queryStartIndex);
                            if (keyValueSeparatorIndex >= 0)
                            {
                                string key = qs.Substring(queryStartIndex, keyValueSeparatorIndex - queryStartIndex);
                                string value = keyValueSeparatorIndex < (queryEndIndex - 1)
                                    ? qs.Substring(keyValueSeparatorIndex + 1, queryEndIndex - keyValueSeparatorIndex - 1)
                                    : String.Empty;
                                ret.Add(key, value);
                            }
                            else
                            {
                                ret.Add(qs.Substring(queryStartIndex, queryEndIndex - queryStartIndex), null);
                            }
                        }

                        queryStartIndex = queryEndIndex + 1;
                    }
                }

                _Elements = ret;
                return _Elements;
            }
        }

        #endregion

        #region Private-Members

        private string _FullUrl = null;
        private string _Querystring = null;
        private bool _QuerystringEvaluated = false;
        private NameValueCollection _Elements = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Query details.
        /// </summary>
        public QueryDetails()
        {

        }

        /// <summary>
        /// Query details.
        /// </summary>
        /// <param name="fullUrl">Full URL.</param>
        public QueryDetails(string fullUrl)
        {
            if (String.IsNullOrEmpty(fullUrl)) throw new ArgumentNullException(nameof(fullUrl));
            _FullUrl = fullUrl;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
