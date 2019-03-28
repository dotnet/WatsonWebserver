using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Content route processor.  Handles GET and HEAD requests to content routes for files and directories. 
    /// </summary>
    internal class ContentRouteProcessor
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager _Logging;
        private bool _Debug;
        private ContentRouteManager _Routes;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="logging">Logging instance.</param>
        /// <param name="debug">Enable or disable debugging.</param>
        public ContentRouteProcessor(LoggingManager logging, bool debug, ContentRouteManager routes)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (routes == null) throw new ArgumentNullException(nameof(routes));

            _Logging = logging;
            _Debug = debug;
            _Routes = routes; 
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Process an incoming request for a content route.
        /// </summary>
        /// <param name="req">The HttpRequest.</param>
        /// <returns>HttpResponse.</returns>
        public HttpResponse Process(HttpRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            if (req.Method != HttpMethod.GET 
                && req.Method != HttpMethod.HEAD)
            {
                _Logging.Log("ContentRouteProcessor request using method other than GET and HEAD: " + req.Method.ToString());
                return Send500Response(req);
            }

            string filePath = req.RawUrlWithoutQuery;
            if (!String.IsNullOrEmpty(filePath) && filePath.StartsWith("/")) filePath = filePath.Substring(1);
            filePath = AppDomain.CurrentDomain.BaseDirectory + filePath;
            filePath = filePath.Replace("+", " ").Replace("%20", " ");

            string contentType = GetContentType(filePath);

            if (!File.Exists(filePath))
            {
                _Logging.Log("ContentRouteProcessor unable to find " + filePath);
                return Send404Response(req);
            }

            try
            {
                byte[] data = null;
                if (req.Method == HttpMethod.GET)
                {
                    data = File.ReadAllBytes(filePath);
                    return new HttpResponse(req, true, 200, null, contentType, data, true);
                }
                else if (req.Method == HttpMethod.HEAD)
                {
                    data = null;
                    return Send204Response(req);
                }
                else
                {
                    _Logging.Log("ContentRouteProcessor request using method other than GET and HEAD: " + req.Method.ToString());
                    return Send500Response(req);
                }
            }
            catch (Exception e)
            {
                _Logging.Log("ContentRouteProcessor error reading " + filePath + ": " + e.Message);
                return Send500Response(req);
            }
        }

        #endregion

        #region Private-Methods
         
        private string GetContentType(string path)
        {
            if (String.IsNullOrEmpty(path)) return "application/octet-stream";

            int idx = path.LastIndexOf(".");
            if (idx >= 0)
            {
                return MimeTypes.GetFromExtension(path.Substring(idx));
            }

            return "application/octet-stream";
        }

        private HttpResponse Send204Response(HttpRequest req)
        {
            return new HttpResponse(req, true, 204, null, null, null, true);
        }

        private HttpResponse Send404Response(HttpRequest req)
        {
            return new HttpResponse(req, false, 404, null, "text/plain", "Not Found", true);
        }

        private HttpResponse Send500Response(HttpRequest req)
        {
            return new HttpResponse(req, false, 500, null, "text/plain", "Internal Server Error", true);
        }

        #endregion
    }
}
