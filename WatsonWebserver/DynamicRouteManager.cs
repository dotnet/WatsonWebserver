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

        public void Add(string verb, Regex path, Func<HttpRequest, HttpResponse> handler)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            RegexMatch.Add(
                new Regex(BuildConsolidatedRegex(verb, path)), 
                handler);
        }

        public void Remove(string verb, Regex path)
        { 
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (path == null) throw new ArgumentNullException(nameof(path));
            RegexMatch.Remove(
                new Regex(BuildConsolidatedRegex(verb, path)));
        }
         
        public bool Exists(string verb, Regex path)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (path == null) throw new ArgumentNullException(nameof(path));
            return RegexMatch.Exists(
                new Regex(BuildConsolidatedRegex(verb, path)));
        }

        public Func<HttpRequest, HttpResponse> Match(string verb, string rawUrl)
        {
            if (String.IsNullOrEmpty(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrEmpty(rawUrl)) throw new ArgumentNullException(nameof(rawUrl));

            object val;
            Func<HttpRequest, HttpResponse> handler;
            if (RegexMatch.Match(
                BuildConsolidatedRegex(verb, rawUrl), 
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

        private string BuildConsolidatedRegex(string verb, string rawUrl)
        {
            rawUrl = rawUrl.Replace("^", "");
            return verb.ToLower() + " " + rawUrl;
        }

        private string BuildConsolidatedRegex(string verb, Regex path)
        {
            string pathString = path.ToString().Replace("^", "");
            return verb.ToLower() + " " + pathString;
        }

        #endregion
    }
}
