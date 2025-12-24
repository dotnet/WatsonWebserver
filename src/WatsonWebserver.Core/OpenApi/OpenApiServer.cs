namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// Server object representing a server URL.
    /// </summary>
    public class OpenApiServer
    {
        /// <summary>
        /// A URL to the target host. Required.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// A description of the server.
        /// </summary>
        public string Description { get; set; } = null;
    }
}
