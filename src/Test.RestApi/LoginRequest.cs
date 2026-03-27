namespace Test.RestApi
{
    /// <summary>
    /// Request body for login.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Password.
        /// </summary>
        public string Password { get; set; }
    }
}
