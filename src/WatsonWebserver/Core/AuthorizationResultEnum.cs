namespace WatsonWebserver.Core
{
    /// <summary>
    /// Authorization result enumeration indicating whether access is permitted.
    /// </summary>
    public enum AuthorizationResultEnum
    {
        /// <summary>
        /// Access is permitted.
        /// </summary>
        Permitted,

        /// <summary>
        /// Access is denied implicitly (no matching rule found).
        /// </summary>
        DeniedImplicit,

        /// <summary>
        /// Access is denied explicitly (a deny rule matched).
        /// </summary>
        DeniedExplicit
    }
}
