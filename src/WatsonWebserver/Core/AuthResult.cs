namespace WatsonWebserver.Core
{
    /// <summary>
    /// Result of an authentication and authorization check.
    /// Used by structured API authentication to determine whether a request should proceed.
    /// </summary>
    public class AuthResult
    {
        #region Public-Members

        /// <summary>
        /// The authentication result.
        /// </summary>
        public AuthenticationResultEnum AuthenticationResult { get; set; } = AuthenticationResultEnum.NotFound;

        /// <summary>
        /// The authorization result.
        /// </summary>
        public AuthorizationResultEnum AuthorizationResult { get; set; } = AuthorizationResultEnum.DeniedImplicit;

        /// <summary>
        /// User-supplied metadata from the authentication handler.
        /// This value is propagated to the route handler via ApiRequest.Metadata and HttpContextBase.Metadata.
        /// </summary>
        public object Metadata { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthResult()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Returns true if the authentication was successful and authorization is permitted.
        /// </summary>
        /// <returns>True if allowed to proceed.</returns>
        public bool IsPermitted()
        {
            return AuthenticationResult == AuthenticationResultEnum.Success
                && AuthorizationResult == AuthorizationResultEnum.Permitted;
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
