using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    internal class ContentRoute
    {
        #region Public-Members
        
        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        public string Path;

        /// <summary>
        /// Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.
        /// </summary>
        public bool IsDirectory;
         
        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a new route object.
        /// </summary> 
        /// <param name="path">The pattern against which the raw URL should be matched.</param>
        /// <param name="isDirectory">Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.</param> 
        public ContentRoute(string path, bool isDirectory)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path)); 
            
            Path = path.ToLower();
            IsDirectory = isDirectory; 
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
