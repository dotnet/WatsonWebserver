namespace Test.Aot
{
    /// <summary>
    /// Request body for creating a user.
    /// </summary>
    public class AotCreateUserRequest
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Role.
        /// </summary>
        public AotUserRole Role { get; set; } = AotUserRole.Member;
    }
}
