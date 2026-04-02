namespace WatsonWebserver.Core
{
    using System;
    using System.Threading;

    /// <summary>
    /// API request wrapper providing typed access to URL parameters, query parameters, headers,
    /// and the deserialized request body. Passed to API route handlers.
    /// </summary>
    /// <remarks>
    /// Instances are created per request and are not intended for concurrent use across threads.
    /// <see cref="Data"/>, <see cref="AuthResult"/>, and <see cref="Metadata"/> may be null depending on
    /// the route shape and authentication outcome.
    /// </remarks>
    public class ApiRequest
    {
        #region Public-Members

        /// <summary>
        /// The deserialized request body data.
        /// Null when using a non-generic route or when no request body was present.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// The underlying HTTP context providing full access to the raw request and response.
        /// </summary>
        public HttpContextBase Http { get; }

        /// <summary>
        /// URL path parameters with typed accessors.
        /// For a route like /users/{id}, access via Parameters["id"] or Parameters.GetGuid("id").
        /// </summary>
        public RequestParameters Parameters { get; }

        /// <summary>
        /// Query string parameters with typed accessors.
        /// For a URL like /users?page=2, access via Query["page"] or Query.GetInt("page").
        /// </summary>
        public RequestParameters Query { get; }

        /// <summary>
        /// Request headers with typed accessors.
        /// </summary>
        public RequestParameters Headers { get; }

        /// <summary>
        /// Serialization helper for manual serialization and deserialization.
        /// </summary>
        public ISerializationHelper Serializer { get; }

        /// <summary>
        /// Authentication and authorization result.
        /// Populated when a structured authentication handler is configured.
        /// Null when no structured authentication result was produced for the request.
        /// </summary>
        public AuthResult AuthResult { get; set; } = null;

        /// <summary>
        /// Cancellation token for the request.
        /// When request timeouts are enabled, this token will be cancelled when the timeout expires.
        /// Pass this token to async operations within your route handler to enable cooperative cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// User-supplied metadata.
        /// Populated from HttpContextBase.Metadata, typically set by an authentication handler.
        /// Null when no metadata was attached to the request.
        /// </summary>
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an API request.
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <param name="serializer">Serialization helper.</param>
        /// <param name="data">Deserialized request body data, or null.</param>
        /// <param name="cancellationToken">Cancellation token for the request. When timeouts are enabled, this token is cancelled on timeout.</param>
        /// <exception cref="ArgumentNullException">Thrown when ctx or serializer is null.</exception>
        public ApiRequest(HttpContextBase ctx, ISerializationHelper serializer, object data, CancellationToken cancellationToken = default)
        {
            Http = ctx ?? throw new ArgumentNullException(nameof(ctx));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Data = data;
            CancellationToken = cancellationToken;

            Parameters = new RequestParameters(ctx.Request?.Url?.Parameters);
            Query = new RequestParameters(ctx.Request?.Query?.Elements);
            Headers = new RequestParameters(ctx.Request?.Headers);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Cast the deserialized data to a specific type.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <returns>The data cast to the specified type, or null if the data is null or not of the expected type.</returns>
        public T GetData<T>() where T : class
        {
            return Data as T;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
