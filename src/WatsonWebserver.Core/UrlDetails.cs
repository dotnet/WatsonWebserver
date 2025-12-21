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
                if (_Uri != null) return _Uri.Scheme;
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
                if (_Uri != null) return _Uri.Host;
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
                if (_Uri != null) return _Uri.Port;
                return null;
            }
        }

        /// <summary>
        /// Full URL.
        /// </summary>
        public string Full { get; set; } = null;

        /// <summary>
        /// Raw URL with query.
        /// </summary>
        public string RawWithQuery { get; set; } = null;

        /// <summary>
        /// Raw URL without query.
        /// </summary>
        public string RawWithoutQuery
        {
            get
            {
                if (!String.IsNullOrEmpty(RawWithQuery))
                {
                    if (RawWithQuery.Contains("?")) return RawWithQuery.Substring(0, RawWithQuery.IndexOf("?"));
                    else return RawWithQuery;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Raw URL elements.
        /// </summary>
        public string[] Elements
        {
            get
            {
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

                        return decoded;
                    }
                }

                string[] ret = new string[0];
                return ret;
            }
        }

        /// <summary>
        /// Parameters found within the URL, if using parameter routes.
        /// </summary>
        public NameValueCollection Parameters
        {
            get
            {
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
        private NameValueCollection _Parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// URL details.
        /// </summary>
        public UrlDetails()
        {

        }

        /// <summary>
        /// URL details.
        /// </summary>
        /// <param name="fullUrl">Full URL.</param>
        /// <param name="rawUrl">Raw URL.</param>
        public UrlDetails(string fullUrl, string rawUrl)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            _Uri = new Uri(fullUrl);

            Full = fullUrl;
            RawWithQuery = rawUrl;
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

        #endregion
    }
}
