using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Default pages served by Watson webserver.
    /// </summary>
    public class WatsonWebserverPages
    {
        #region Public-Members

        /// <summary>
        /// Page displayed when sending a 404 due to a lack of a route.
        /// </summary>
        public Page Default404Page
        {
            get
            {
                return _Default404Page;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Default404Page));
                _Default404Page = value;
            }
        }

        /// <summary>
        /// Page displayed when sending a 500 due to an exception is unhandled within your routes.
        /// </summary>
        public Page Default500Page
        {
            get
            {
                return _Default500Page;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Default500Page));
                _Default500Page = value;
            }
        }

        #endregion

        #region Private-Members

        private Page _Default404Page = new Page("text/plain", "Not found");
        private Page _Default500Page = new Page("text/plain", "Internal server error");

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WatsonWebserverPages()
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
