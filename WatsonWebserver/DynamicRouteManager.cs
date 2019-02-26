using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RegexMatcher;

namespace WatsonWebserver
{
    internal class DynamicRouteManager
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager Logging;
        private bool Debug;
        private Matcher RegexMatch;

        #endregion

        #region Constructors-and-Factories

        public DynamicRouteManager(LoggingManager logging, bool debug)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            Logging = logging;
            Debug = debug;
            RegexMatch = new Matcher();
        }

        #endregion

        #region Public-Methods

        public void Add(HttpMethod method, Regex path, Func<HttpRequest, HttpResponse> handler)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            RegexMatch.Add(
                new Regex(BuildConsolidatedRegex(method, path)), 
                handler);
        }

        public void Remove(HttpMethod method, Regex path)
        { 
            if (path == null) throw new ArgumentNullException(nameof(path));
            RegexMatch.Remove(
                new Regex(BuildConsolidatedRegex(method, path)));
        }
         
        public bool Exists(HttpMethod method, Regex path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            return RegexMatch.Exists(
                new Regex(BuildConsolidatedRegex(method, path)));
        }

        public Func<HttpRequest, HttpResponse> Match(HttpMethod method, string rawUrl)
        {
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            object val;
            Func<HttpRequest, HttpResponse> handler;
            if (RegexMatch.Match(
                BuildConsolidatedRegex(method, rawUrl), 
                out val))
            { 
                if (val == null) return null;
                handler = (Func<HttpRequest, HttpResponse>)val;
                return handler;
            }

            return null;
        }

        #endregion

        #region Private-Methods

        private string BuildConsolidatedRegex(HttpMethod method, string rawUrl)
        {
            rawUrl = rawUrl.Replace("^", "");
            return method.ToString() + " " + rawUrl;
        }

        private string BuildConsolidatedRegex(HttpMethod method, Regex path)
        {
            string pathString = path.ToString().Replace("^", "");
            return method.ToString() + " " + pathString;
        }

        #endregion
    }
}
