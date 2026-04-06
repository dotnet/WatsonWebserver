#if NET8_0_OR_GREATER
namespace WatsonWebserver.Http3
{
    using System;
    using WatsonWebserver.Core;

    /// <summary>
    /// HTTP/3 request context.
    /// </summary>
    public class Http3Context : HttpContextBase
    {
        /// <summary>
        /// Instantiate the context.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="request">HTTP/3 request.</param>
        /// <param name="response">HTTP/3 response.</param>
        /// <param name="connectionMetadataFactory">Connection metadata factory.</param>
        /// <param name="streamMetadataFactory">Stream metadata factory.</param>
        public Http3Context(
            WebserverSettings settings,
            Http3Request request,
            Http3Response response,
            Func<ConnectionMetadata> connectionMetadataFactory,
            Func<StreamMetadata> streamMetadataFactory)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (connectionMetadataFactory == null) throw new ArgumentNullException(nameof(connectionMetadataFactory));
            if (streamMetadataFactory == null) throw new ArgumentNullException(nameof(streamMetadataFactory));

            Protocol = HttpProtocol.Http3;
            SetConnectionFactory(connectionMetadataFactory);
            SetStreamFactory(streamMetadataFactory);
            Request = request;
            Response = response;
        }
    }
}
#endif

