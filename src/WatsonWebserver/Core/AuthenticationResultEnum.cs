namespace WatsonWebserver.Core
{
    /// <summary>
    /// Authentication result enumeration indicating the outcome of an authentication attempt.
    /// </summary>
    public enum AuthenticationResultEnum
    {
        /// <summary>
        /// Authentication was successful.
        /// </summary>
        Success,

        /// <summary>
        /// The credentials or identity were not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// The credentials or session have expired.
        /// </summary>
        Expired,

        /// <summary>
        /// Authentication failed due to insufficient permissions.
        /// </summary>
        PermissionDenied,

        /// <summary>
        /// The request contained invalid or missing authentication material.
        /// </summary>
        Invalid
    }
}
