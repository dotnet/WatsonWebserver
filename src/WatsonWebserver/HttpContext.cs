namespace WatsonWebserver
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Timestamps;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Http1;

    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext : HttpContextBase
    {
        #region Public-Members

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ctx">HTTP listener context.</param>
        /// <param name="settings">Settings.</param>
        /// <param name="events">Events.</param>
        /// <param name="serializer">Serializer.</param>
        internal HttpContext(
            HttpListenerContext ctx, 
            WebserverSettings settings, 
            WebserverEvents events,
            ISerializationHelper serializer)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            HttpRequest request = new HttpRequest();
            request.Initialize(ctx, serializer);

            HttpResponse response = new HttpResponse();
            response.Initialize(request, ctx, settings, events, serializer);

            Initialize(
                CreateHttp1ConnectionMetadata(
                    ctx.Request.RemoteEndPoint.Address.ToString(),
                    ctx.Request.RemoteEndPoint.Port,
                    ctx.Request.LocalEndPoint.Address.ToString(),
                    ctx.Request.LocalEndPoint.Port,
                    settings.Hostname,
                    ctx.Request.IsSecureConnection),
                request,
                response);
        }

        internal HttpContext(
            WebserverSettings settings,
            WebserverEvents events,
            Stream stream,
            Http1RequestMetadata requestMetadata,
            int streamBufferSize)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (requestMetadata == null) throw new ArgumentNullException(nameof(requestMetadata));
            if (streamBufferSize < 1) throw new ArgumentOutOfRangeException(nameof(streamBufferSize));

            HttpRequest request = new HttpRequest();
            request.Initialize(settings, stream, requestMetadata);

            HttpResponse response = new HttpResponse();
            response.Initialize(request, settings, events, stream, streamBufferSize);

            Initialize(
                CreateHttp1ConnectionMetadata(
                    requestMetadata.Source.IpAddress,
                    requestMetadata.Source.Port,
                    requestMetadata.Destination.IpAddress,
                    requestMetadata.Destination.Port,
                    settings.Hostname,
                    settings.Ssl.Enable),
                request,
                response);
        }

        #endregion

        #region Public-Methods

        internal void Initialize(ConnectionMetadata connectionMetadata, HttpRequest request, HttpResponse response)
        {
            if (connectionMetadata == null) throw new ArgumentNullException(nameof(connectionMetadata));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (response == null) throw new ArgumentNullException(nameof(response));

            Protocol = HttpProtocol.Http1;
            SetConnectionFactory(() => connectionMetadata);
            SetStreamFactory(() => CreateHttp1StreamMetadata());
            Request = request;
            Response = response;
        }

        internal void ReturnToPool()
        {
            ResetForReuse();
        }

        /// <summary>
        /// Reset the HTTP/1.1 context before returning it to the pool.
        /// </summary>
        protected override void ResetForReuse()
        {
            base.ResetForReuse();
        }

        #endregion

        #region Private-Methods

        private static ConnectionMetadata CreateHttp1ConnectionMetadata(
            string sourceIp,
            int sourcePort,
            string destinationIp,
            int destinationPort,
            string hostname,
            bool isEncrypted)
        {
            ConnectionMetadata connectionMetadata = new ConnectionMetadata();
            connectionMetadata.Protocol = HttpProtocol.Http1;
            connectionMetadata.IsEncrypted = isEncrypted;
            connectionMetadata.Source = new SourceDetails(sourceIp, sourcePort);
            connectionMetadata.Destination = new DestinationDetails(destinationIp, destinationPort, hostname);
            return connectionMetadata;
        }

        private static StreamMetadata CreateHttp1StreamMetadata()
        {
            StreamMetadata streamMetadata = new StreamMetadata();
            streamMetadata.Protocol = HttpProtocol.Http1;
            streamMetadata.Multiplexed = false;
            return streamMetadata;
        }

        #endregion
    }
}
