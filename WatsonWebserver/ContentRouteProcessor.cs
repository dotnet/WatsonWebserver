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
         
        private ContentRouteManager _Routes;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="logging">Logging instance.</param>
        /// <param name="debug">Enable or disable debugging.</param>
        public ContentRouteProcessor(ContentRouteManager routes)
        { 
            if (routes == null) throw new ArgumentNullException(nameof(routes));
             
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
                return Send500Response(req);
            }

            string filePath = req.RawUrlWithoutQuery;
            if (!String.IsNullOrEmpty(filePath) && filePath.StartsWith("/")) filePath = filePath.Substring(1);
            filePath = AppDomain.CurrentDomain.BaseDirectory + filePath;
            filePath = filePath.Replace("+", " ").Replace("%20", " ");

            string contentType = GetContentType(filePath);

            if (!File.Exists(filePath))
            { 
                return Send404Response(req);
            }

            try
            {
                byte[] data = null;
                if (req.Method == HttpMethod.GET)
                {
                    data = File.ReadAllBytes(filePath);
                    return new HttpResponse(req, 200, null, contentType, data);
                }
                else if (req.Method == HttpMethod.HEAD)
                {
                    data = null;
                    return Send204Response(req);
                }
                else
                { 
                    return Send500Response(req);
                }
            }
            catch (Exception)
            { 
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
            return new HttpResponse(req, 204, null);
        }

        private HttpResponse Send404Response(HttpRequest req)
        {
            return new HttpResponse(req, 404, null, "text/plain", Encoding.UTF8.GetBytes("Not Found"));
        }

        private HttpResponse Send500Response(HttpRequest req)
        {
            return new HttpResponse(req, 500, null, "text/plain", Encoding.UTF8.GetBytes("Internal Server Error"));
        }

        #endregion
    }
}
