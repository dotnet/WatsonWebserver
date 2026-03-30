namespace WatsonWebserver.Http2
{
    using System;
    using WatsonWebserver.Core;

    /// <summary>
    /// HTTP/2 request context.
    /// </summary>
    public class Http2Context : HttpContextBase
    {
        /// <summary>
        /// Instantiate the context.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="request">HTTP/2 request.</param>
        /// <param name="response">HTTP/2 response.</param>
        /// <param name="connectionMetadataFactory">Connection metadata factory.</param>
        /// <param name="streamMetadataFactory">Stream metadata factory.</param>
        public Http2Context(
            WebserverSettings settings,
            Http2Request request,
            Http2Response response,
            Func<ConnectionMetadata> connectionMetadataFactory,
            Func<StreamMetadata> streamMetadataFactory)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (connectionMetadataFactory == null) throw new ArgumentNullException(nameof(connectionMetadataFactory));
            if (streamMetadataFactory == null) throw new ArgumentNullException(nameof(streamMetadataFactory));

            Protocol = HttpProtocol.Http2;
            SetConnectionFactory(connectionMetadataFactory);
            SetStreamFactory(streamMetadataFactory);
            Request = request;
            Response = response;
        }
    }
}

