namespace Test.RestApi
{
    using System;

    /// <summary>
    /// Login success response payload.
    /// </summary>
    internal class LoginSuccessResponse
    {
        /// <summary>
        /// Bearer token.
        /// </summary>
        public string Token
        {
            get
            {
                return _Token;
            }
            set
            {
                _Token = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Token lifetime in seconds.
        /// </summary>
        public int ExpiresIn
        {
            get
            {
                return _ExpiresIn;
            }
            set
            {
                if (value < 0) _ExpiresIn = 0;
                else _ExpiresIn = value;
            }
        }

        private string _Token = String.Empty;
        private int _ExpiresIn = 0;
    }
}
