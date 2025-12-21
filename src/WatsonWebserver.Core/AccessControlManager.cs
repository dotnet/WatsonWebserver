namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using IpMatcher;

    /// <summary>
    /// Access control manager.  Dictates which connections are permitted or denied.
    /// </summary>
    public class AccessControlManager
    {
        #region Public-Members

        /// <summary>
        /// Matcher to match denied addresses.
        /// </summary>
        public Matcher DenyList
        {
            get
            {
                return _DenyList;
            }
            set
            {
                if (value == null) value = new Matcher();
                _DenyList = value;
            }
        }

        /// <summary>
        /// Matcher to match permitted addresses.
        /// </summary>
        public Matcher PermitList
        {
            get
            {
                return _PermitList;
            }
            set
            {
                if (value == null) value = new Matcher();
                _PermitList = value;
            }
        }

        /// <summary>
        /// Access control mode, either DefaultPermit or DefaultDeny.
        /// DefaultPermit: allow everything, except for those explicitly denied.
        /// DefaultDeny: deny everything, except for those explicitly permitted.
        /// </summary>
        public AccessControlMode Mode { get; set; } = AccessControlMode.DefaultPermit;

        #endregion

        #region Private-Members

        private Matcher _DenyList = new Matcher();
        private Matcher _PermitList = new Matcher();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary> 
        /// <param name="mode">Access control mode.</param>
        public AccessControlManager(AccessControlMode mode = AccessControlMode.DefaultPermit)
        {
            Mode = mode;
        }

        #endregion

        #region Public-Methods
        
        /// <summary>
        /// Permit or deny a request based on IP address.  
        /// When operating in 'default deny', only specified entries are permitted. 
        /// When operating in 'default permit', everything is allowed unless explicitly denied.
        /// </summary>
        /// <param name="ip">The IP address to evaluate.</param>
        /// <returns>True if permitted.</returns>
        public bool Permit(string ip)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));

            switch (Mode)
            {
                case AccessControlMode.DefaultDeny:
                    return PermitList.MatchExists(ip);

                case AccessControlMode.DefaultPermit:
                    if (DenyList.MatchExists(ip)) return false;
                    return true;

                default:
                    throw new ArgumentException("Unknown access control mode: " + Mode.ToString());
            }
        }

        #endregion

        #region Private-Methods
        
        #endregion
    }
}
