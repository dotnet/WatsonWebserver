namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Authorization details.
    /// </summary>
    public class AuthorizationDetails
    {
        #region Public-Members

        /// <summary>
        /// Value.
        /// </summary>
        public string Value
        {
            get
            {
                return _Value;
            }
        }

        /// <summary>
        /// Username.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
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
        }

        /// <summary>
        /// Bearer token.
        /// </summary>
        public string BearerToken
        {
            get
            {
                return _BearerToken;
            }
        }

        #endregion

        #region Private-Members

        private string _Value = null;
        private string _Username = null;
        private string _Password = null;
        private string _BearerToken = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Authorization details.
        /// </summary>
        public AuthorizationDetails()
        {
            // do nothing
        }

        /// <summary>
        /// Authorization details.
        /// </summary>
        /// <param name="value">Value from the Authorization header.</param>
        public AuthorizationDetails(string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                _Value = value;

                if (value.StartsWith("Basic ") && value.Length > 6)
                {
                    string encoded = value.Substring(6);

                    try
                    {
                        string cred = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                        string[] credParts = cred.Split(new char[] { ':' }, 2);
                        if (credParts.Length == 1)
                        {
                            _Username = credParts[0];
                        }
                        else if (credParts.Length == 2)
                        {
                            _Username = credParts[0];
                            _Password = credParts[1];
                        }

                    }
                    catch (Exception)
                    {

                    }
                }
                else if (value.StartsWith("Bearer ") && value.Length > 7)
                {
                    _BearerToken = value.Substring(7);
                }
            }
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
