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
    /// The manager owns the matchers assigned to <see cref="DenyList"/> and <see cref="PermitList"/>: a matcher
    /// that is replaced, or held when the manager is disposed, is disposed to release its match cache.
    /// </summary>
    public class AccessControlManager : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Matcher to match denied addresses.
        /// Assigning a different matcher disposes the previous one.
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
                Matcher previous = _DenyList;
                _DenyList = value;
                DisposeReplaced(previous, value, _PermitList);
            }
        }

        /// <summary>
        /// Matcher to match permitted addresses.
        /// Assigning a different matcher disposes the previous one.
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
                Matcher previous = _PermitList;
                _PermitList = value;
                DisposeReplaced(previous, value, _DenyList);
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
        private bool _Disposed = false;

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
                    return TryMatch(PermitList, ip);

                case AccessControlMode.DefaultPermit:
                    if (TryMatch(DenyList, ip)) return false;
                    return true;

                default:
                    throw new ArgumentException("Unknown access control mode: " + Mode.ToString());
            }
        }

        /// <summary>
        /// Dispose the deny-list and permit-list matchers, releasing their match caches.
        /// The matchers continue to evaluate addresses after disposal, without caching.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;
            _Disposed = true;

            if (disposing)
            {
                _DenyList?.Dispose();
                if (!ReferenceEquals(_PermitList, _DenyList)) _PermitList?.Dispose();
            }
        }

        private static void DisposeReplaced(Matcher previous, Matcher current, Matcher sibling)
        {
            if (previous == null) return;
            if (ReferenceEquals(previous, current)) return;
            if (ReferenceEquals(previous, sibling)) return;
            previous.Dispose();
        }

        private static bool TryMatch(Matcher matcher, string ip)
        {
            if (matcher == null) return false;

            try
            {
                return matcher.MatchExists(ip);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}
