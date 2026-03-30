namespace Test.RestApi
{
    using System;

    /// <summary>
    /// Request body for login.
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// Username.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                _Username = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Password.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                _Password = value ?? String.Empty;
            }
        }

        private string _Username = String.Empty;
        private string _Password = String.Empty;
    }
}
