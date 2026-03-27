namespace Test.Automated
{
    using System;

    /// <summary>
    /// Typed response describing request header observations.
    /// </summary>
    public class HeaderObservationResponse
    {
        /// <summary>
        /// Indicates whether the custom header exists.
        /// </summary>
        public bool HeaderExists { get; set; } = false;

        /// <summary>
        /// Header value retrieved from the fast lookup path.
        /// </summary>
        public string RetrievedHeaderValue
        {
            get
            {
                return _RetrievedHeaderValue;
            }
            set
            {
                _RetrievedHeaderValue = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Header value retrieved from materialized headers.
        /// </summary>
        public string MaterializedHeaderValue
        {
            get
            {
                return _MaterializedHeaderValue;
            }
            set
            {
                _MaterializedHeaderValue = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Request content type.
        /// </summary>
        public string ContentType
        {
            get
            {
                return _ContentType;
            }
            set
            {
                _ContentType = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Request user agent.
        /// </summary>
        public string UserAgent
        {
            get
            {
                return _UserAgent;
            }
            set
            {
                _UserAgent = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Request query value.
        /// </summary>
        public string QueryValue
        {
            get
            {
                return _QueryValue;
            }
            set
            {
                _QueryValue = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Request body string.
        /// </summary>
        public string Body
        {
            get
            {
                return _Body;
            }
            set
            {
                _Body = value ?? String.Empty;
            }
        }

        private string _RetrievedHeaderValue = String.Empty;
        private string _MaterializedHeaderValue = String.Empty;
        private string _ContentType = String.Empty;
        private string _UserAgent = String.Empty;
        private string _QueryValue = String.Empty;
        private string _Body = String.Empty;
    }
}
