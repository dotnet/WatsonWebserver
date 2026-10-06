namespace Test.Aot
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Response type returned by the API routes.
    /// </summary>
    public class AotUser
    {
        /// <summary>
        /// Identifier.
        /// </summary>
        public Guid Id { get; set; } = Guid.Empty;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Role.
        /// </summary>
        public AotUserRole Role { get; set; } = AotUserRole.Member;

        /// <summary>
        /// Creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        /// <summary>
        /// Tags.
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
    }
}
