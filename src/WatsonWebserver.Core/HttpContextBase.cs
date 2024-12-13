namespace WatsonWebserver.Core
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using Timestamps;

    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContextBase
    {
        #region Public-Members

        /// <summary>
        /// UTC timestamp from when the context object was created.
        /// </summary>
        [JsonPropertyOrder(0)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        [JsonPropertyOrder(1)]
        public HttpRequestBase Request { get; set; } = null;

        /// <summary>
        /// Type of route.
        /// </summary>
        [JsonPropertyOrder(2)]
        public RouteTypeEnum RouteType { get; set; } = RouteTypeEnum.Default;

        /// <summary>
        /// Matched route.
        /// </summary>
        [JsonPropertyOrder(3)]
        public object Route { get; set; } = null;

        /// <summary>
        /// Globally-unique identifier for the context.
        /// </summary>
        [JsonPropertyOrder(4)]
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Cancellation token source.
        /// </summary>
        [JsonPropertyOrder(5)]
        [JsonIgnore]
        public CancellationTokenSource TokenSource
        {
            get
            {
                return _TokenSource;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(TokenSource));
                _TokenSource = value;
            }
        }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        [JsonPropertyOrder(6)]
        [JsonIgnore]
        public CancellationToken Token { get; set; } = _TokenSource.Token;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        [JsonPropertyOrder(998)]
        public HttpResponseBase Response { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
