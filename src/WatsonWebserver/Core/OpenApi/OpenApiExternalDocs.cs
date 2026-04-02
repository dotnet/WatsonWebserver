namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// External documentation object.
    /// </summary>
    public class OpenApiExternalDocs
    {
        /// <summary>
        /// A description of the target documentation.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// The URL for the target documentation. Required.
        /// </summary>
        public string Url { get; set; } = null;
    }
}
