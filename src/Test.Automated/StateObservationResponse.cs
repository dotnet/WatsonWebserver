namespace Test.Automated
{
    using System;

    /// <summary>
    /// Typed response describing request state observations.
    /// </summary>
    public class StateObservationResponse
    {
        /// <summary>
        /// Trace header value.
        /// </summary>
        public string TraceHeader
        {
            get
            {
                return _TraceHeader;
            }
            set
            {
                _TraceHeader = value ?? String.Empty;
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

        /// <summary>
        /// Request content length.
        /// </summary>
        public long ContentLength
        {
            get
            {
                return _ContentLength;
            }
            set
            {
                if (value < 0) _ContentLength = 0;
                else _ContentLength = value;
            }
        }

        /// <summary>
        /// Indicates whether the request was chunked.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        private string _TraceHeader = String.Empty;
        private string _Body = String.Empty;
        private long _ContentLength = 0;
    }
}
