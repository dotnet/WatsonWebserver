using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IpMatcher;

namespace WatsonWebserver
{
    /// <summary>
    /// Access control manager.  Dictates which connections are permitted or denied.
    /// </summary>
    public class AccessControlManager
    {
        #region Public-Members

        /// <summary>
        /// Matcher to match blacklisted addresses.
        /// </summary>
        public Matcher Blacklist;

        /// <summary>
        /// Matcher to match whitelisted addresses.
        /// </summary>
        public Matcher Whitelist;

        /// <summary>
        /// Access control mode, either DefaultPermit or DefaultDeny.
        /// DefaultPermit: allow everything, except for those explicitly blacklisted.
        /// DefaultDeny: deny everything, except for those explicitly whitelisted.
        /// </summary>
        public AccessControlMode Mode;

        #endregion

        #region Private-Members
          
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        /// <param name="mode">Access control mode.</param>
        public AccessControlManager(AccessControlMode mode)
        {
            Blacklist = new Matcher();
            Whitelist = new Matcher();
            Mode = mode;
        }

        #endregion

        #region Public-Methods
        
        /// <summary>
        /// Permit or deny a request based on IP address.  
        /// When operating in 'default deny', only white listed entries are permitted. 
        /// When operating in 'default permit', everything is allowed unless explicitly blacklisted.
        /// </summary>
        /// <param name="ip">The IP address to evaluate.</param>
        /// <returns>True if permitted.</returns>
        public bool Permit(string ip)
        {
            if (String.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));

            switch (Mode)
            {
                case AccessControlMode.DefaultDeny:
                    return Whitelist.MatchExists(ip);

                case AccessControlMode.DefaultPermit:
                    if (Blacklist.MatchExists(ip)) return false;
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
