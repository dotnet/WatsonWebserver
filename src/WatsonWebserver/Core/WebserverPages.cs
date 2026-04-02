namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Default pages served.
    /// </summary>
    public class WebserverPages
    {
        #region Public-Members

        /// <summary>
        /// Pages by status code.
        /// </summary>
        public Dictionary<int, WebserverPage> Pages
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

        private Dictionary<int, WebserverPage> _Pages = new Dictionary<int, WebserverPage>
        {
            { 400, new WebserverPage(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent400) },
            { 404, new WebserverPage(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent404) },
            { 500, new WebserverPage(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent500) }
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
    }
}
