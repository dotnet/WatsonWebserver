namespace WatsonWebserver.Core.OpenApi
{
    /// <summary>
    /// The set of OAuth2 flows an OAuth2 security scheme supports. At least one flow should be set
    /// when the parent scheme's type is <c>oauth2</c>; document generation fails otherwise. The
    /// <see cref="DeviceAuthorization"/> flow is only valid when targeting OpenAPI 3.2 or later.
    /// </summary>
    public class OpenApiOAuthFlows
    {
        #region Public-Members

        /// <summary>
        /// Configuration for the OAuth2 implicit flow.
        /// </summary>
        public OpenApiOAuthFlow Implicit { get; set; } = null;

        /// <summary>
        /// Configuration for the OAuth2 resource-owner-password flow.
        /// </summary>
        public OpenApiOAuthFlow Password { get; set; } = null;

        /// <summary>
        /// Configuration for the OAuth2 client-credentials flow.
        /// </summary>
        public OpenApiOAuthFlow ClientCredentials { get; set; } = null;

        /// <summary>
        /// Configuration for the OAuth2 authorization-code flow.
        /// </summary>
        public OpenApiOAuthFlow AuthorizationCode { get; set; } = null;

        /// <summary>
        /// Configuration for the OAuth2 device authorization flow. Only valid when targeting
        /// OpenAPI 3.2 or later; setting it under an earlier target version fails document generation.
        /// </summary>
        public OpenApiOAuthFlow DeviceAuthorization { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiOAuthFlows()
        {
        }

        #endregion
    }
}
