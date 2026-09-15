namespace WatsonWebserver.Core.OpenApi
{
    using System.Collections.Generic;

    /// <summary>
    /// Configuration for a single OAuth2 flow within an <see cref="OpenApiOAuthFlows"/> object.
    /// Which URLs are required depends on the flow: the authorization-code and implicit flows use
    /// <see cref="AuthorizationUrl"/>, the password and client-credentials flows use
    /// <see cref="TokenUrl"/>, and the device authorization flow (OpenAPI 3.2 and later) uses
    /// <see cref="DeviceAuthorizationUrl"/>.
    /// </summary>
    public class OpenApiOAuthFlow
    {
        #region Public-Members

        /// <summary>
        /// The authorization URL to be used for this flow. Applies to the implicit and
        /// authorization-code flows.
        /// </summary>
        public string AuthorizationUrl { get; set; } = null;

        /// <summary>
        /// The token URL to be used for this flow. Applies to the password, client-credentials, and
        /// authorization-code flows.
        /// </summary>
        public string TokenUrl { get; set; } = null;

        /// <summary>
        /// The device authorization endpoint URL. Applies to the device authorization flow and is
        /// only valid when targeting OpenAPI 3.2 or later.
        /// </summary>
        public string DeviceAuthorizationUrl { get; set; } = null;

        /// <summary>
        /// The URL to be used for obtaining refresh tokens. Optional for all flows.
        /// </summary>
        public string RefreshUrl { get; set; } = null;

        /// <summary>
        /// The available scopes for the OAuth2 security scheme, keyed by scope name with the
        /// description as the value. May be empty but should not be null when the flow is emitted.
        /// </summary>
        public Dictionary<string, string> Scopes { get; set; } = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public OpenApiOAuthFlow()
        {
        }

        #endregion
    }
}
