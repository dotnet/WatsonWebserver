namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Routing group.
    /// </summary>
    public class RoutingGroup
    {
        #region Public-Members

        /// <summary>
        /// Static routes.
        /// </summary>
        public StaticRouteManager Static
        {
            get
            {
                return _Static;
            }
            set
            {
                if (value == null) _Static = new StaticRouteManager();
                else _Static = value;
            }
        }

        /// <summary>
        /// Content routes.
        /// </summary>
        public ContentRouteManager Content
        {
            get
            {
                return _Content;
            }
            set
            {
                if (value == null) _Content = new ContentRouteManager();
                else _Content = value;
            }
        }

        /// <summary>
        /// Parameter routes.
        /// </summary>
        public ParameterRouteManager Parameter
        {
            get
            {
                return _Parameter;
            }
            set
            {
                if (value == null) _Parameter = new ParameterRouteManager();
                else _Parameter = value;
            }
        }

        /// <summary>
        /// Dynamic routes.
        /// </summary>
        public DynamicRouteManager Dynamic
        {
            get
            {
                return _Dynamic;
            }
            set
            {
                if (value == null) _Dynamic = new DynamicRouteManager();
                else _Dynamic = value;
            }
        }

        #endregion

        #region Private-Members

        private StaticRouteManager _Static = new StaticRouteManager();
        private ContentRouteManager _Content = new ContentRouteManager();
        private ParameterRouteManager _Parameter = new ParameterRouteManager();
        private DynamicRouteManager _Dynamic = new DynamicRouteManager();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RoutingGroup()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
