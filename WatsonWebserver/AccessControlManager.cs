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

        public Matcher Blacklist;
        public Matcher Whitelist;
        public AccessControlMode Mode;

        #endregion

        #region Private-Members

        private LoggingManager _Logging;
        private bool _Debug;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="logging">Logging instance.</param>
        /// <param name="debug">Enable or disable debugging.</param>
        public AccessControlManager(LoggingManager logging, bool debug, AccessControlMode mode)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            _Logging = logging;
            _Debug = debug;

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
