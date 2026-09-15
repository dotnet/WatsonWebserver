namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// Selects which OpenAPI specification version an <see cref="OpenApiDocumentGenerator"/>
    /// emits. The default is <see cref="V3_0"/> so that documents produced without any explicit
    /// version selection remain byte-compatible with earlier Watson releases.
    /// </summary>
    public enum OpenApiVersionEnum
    {
        /// <summary>
        /// OpenAPI 3.0. Emits the version string <c>3.0.3</c> and the 3.0 Schema Object encoding
        /// (for example <c>nullable: true</c> and <c>format: binary</c>). This is the default.
        /// </summary>
        V3_0,

        /// <summary>
        /// OpenAPI 3.1. Emits the version string <c>3.1.1</c> and aligns the Schema Object with
        /// JSON Schema 2020-12 (nullable is expressed with a <c>"null"</c> type, binary payloads
        /// use <c>contentMediaType</c>/<c>contentEncoding</c>, and schema examples use the
        /// <c>examples</c> array).
        /// </summary>
        V3_1,

        /// <summary>
        /// OpenAPI 3.2. Emits the version string <c>3.2.0</c>, everything from <see cref="V3_1"/>,
        /// plus the 3.2 additions Watson supports (<c>$self</c>, server <c>name</c>, hierarchical
        /// tags, streaming <c>itemSchema</c>, additional path operations such as <c>QUERY</c>, and
        /// the OAuth2 device authorization flow).
        /// </summary>
        V3_2
    }
}
