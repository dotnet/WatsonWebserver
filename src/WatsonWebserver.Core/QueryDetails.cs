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
                if (_FullUrl.Contains("?"))
                {
                    return _FullUrl.Substring(_FullUrl.IndexOf("?") + 1, (_FullUrl.Length - _FullUrl.IndexOf("?") - 1));
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Query elements.
        /// </summary>
        public NameValueCollection Elements
        {
            get
            {
                NameValueCollection ret = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                string qs = Querystring;
                if (!String.IsNullOrEmpty(qs))
                {
                    string[] queries = qs.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                    if (queries.Length > 0)
                    {
                        for (int i = 0; i < queries.Length; i++)
                        {
                            string[] queryParts = queries[i].Split('=');
                            if (queryParts != null && queryParts.Length == 2)
                            {
                                ret.Add(queryParts[0], queryParts[1]);
                            }
                            else if (queryParts != null && queryParts.Length == 1)
                            {
                                ret.Add(queryParts[0], null);
                            }
                        }
                    }
                }

                return ret;
            }
        }

        #endregion

        #region Private-Members

        private string _FullUrl = null;

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
