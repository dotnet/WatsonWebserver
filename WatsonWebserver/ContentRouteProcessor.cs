using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    internal class ContentRouteProcessor
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingManager Logging;
        private bool Debug;
        private ContentRouteManager Routes;

        #endregion

        #region Constructors-and-Factories

        public ContentRouteProcessor(LoggingManager logging, bool debug, ContentRouteManager routes)
        {
            if (logging == null) throw new ArgumentNullException(nameof(logging));
            if (routes == null) throw new ArgumentNullException(nameof(routes));

            Logging = logging;
            Debug = debug;
            Routes = routes; 
        }

        #endregion

        #region Public-Methods

        public HttpResponse Process(HttpRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));

            string filePath = req.RawUrlWithoutQuery;
            if (!String.IsNullOrEmpty(filePath) && filePath.StartsWith("/")) filePath = filePath.Substring(1);

            string contentType = GetContentType(filePath);

            if (!File.Exists(filePath))
            {
                Logging.Log("ContentRouteProcessor unable to find " + filePath);
                return Send404Response(req);
            }

            try
            {
                byte[] data = null;
                if (req.Method.ToLower().Equals("get")) data = File.ReadAllBytes(filePath);
                return new HttpResponse(req, true, 200, null, contentType, data, true);
            }
            catch (Exception e)
            {
                Logging.Log("ContentRouteProcessor error reading " + filePath + ": " + e.Message);
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
