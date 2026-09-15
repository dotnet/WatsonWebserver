namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;

    /// <summary>
    /// Server object representing a server URL. Used by <see cref="OpenApiSettings.Servers"/> for
    /// the document-level server list.
    /// </summary>
    public class OpenApiServer
    {
        /// <summary>
        /// A URL to the target host. Required.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// An optional short name for the server. Added in OpenAPI 3.2 and emitted only when
        /// targeting that version or later.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// A description of the server.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// A map of server variables used for substitution in the server <see cref="Url"/> template,
        /// keyed by variable name. Null when the URL contains no variables.
        /// </summary>
        public Dictionary<string, OpenApiServerVariableMetadata> Variables { get; set; } = null;
    }
}
