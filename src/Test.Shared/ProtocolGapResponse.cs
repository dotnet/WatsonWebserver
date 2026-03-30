namespace Test.Shared
{
    /// <summary>
    /// Structured response used by shared protocol gap coverage.
    /// </summary>
    public class ProtocolGapResponse
    {
        /// <summary>
        /// User identifier from structured authentication metadata.
        /// </summary>
        public int UserId { get; set; } = 0;

        /// <summary>
        /// Role from structured authentication metadata.
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// Marker written by middleware into the response headers.
        /// </summary>
        public string Middleware { get; set; } = string.Empty;

        /// <summary>
        /// Request path observed by the route.
        /// </summary>
        public string Path { get; set; } = string.Empty;
    }
}
