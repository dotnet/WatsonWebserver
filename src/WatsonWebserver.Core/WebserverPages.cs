namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Default pages served.
    /// </summary>
    public class WebserverPages
    {
        #region Public-Members

        /// <summary>
        /// Pages by status code.
        /// </summary>
        public Dictionary<int, Page> Pages
        {
            get
            {
                return _Pages;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Pages));
                _Pages = value;
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<int, Page> _Pages = new Dictionary<int, Page>
        {
            { 400, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent400) },
            { 404, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent404) },
            { 500, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent500) }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WebserverPages()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion

        #region Embedded-Classes

        /// <summary>
        /// Page served by Watson webserver.
        /// </summary>
        public class Page
        {
            /// <summary>
            /// Content type.
            /// </summary>
            public string ContentType { get; private set; } = null;

            /// <summary>
            /// Content.
            /// </summary>
            public string Content { get; private set; } = null;

            /// <summary>
            /// Page served by Watson webserver.
            /// </summary>
            /// <param name="contentType">Content type.</param>
            /// <param name="content">Content.</param>
            public Page(string contentType, string content)
            {
                if (String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));
                if (String.IsNullOrEmpty(content)) throw new ArgumentNullException(nameof(content));

                ContentType = contentType;
                Content = content;
            }
        }

        #endregion
    }
}
