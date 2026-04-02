namespace WatsonWebserver.Core
{
    using System;

    /// <summary>
    /// Page served by Watson webserver.
    /// </summary>
    public class WebserverPage
    {
        #region Public-Members

        /// <summary>
        /// Content type.
        /// </summary>
        public string ContentType { get; private set; } = null;

        /// <summary>
        /// Content.
        /// </summary>
        public string Content { get; private set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="contentType">Content type.</param>
        /// <param name="content">Content.</param>
        /// <exception cref="ArgumentNullException">Thrown when contentType or content is null or empty.</exception>
        public WebserverPage(string contentType, string content)
        {
            if (String.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));
            if (String.IsNullOrEmpty(content)) throw new ArgumentNullException(nameof(content));

            ContentType = contentType;
            Content = content;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
