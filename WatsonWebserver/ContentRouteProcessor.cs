using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatsonWebserver
{
    /// <summary>
    /// Content route handler.  Handles GET and HEAD requests to content routes for files and directories. 
    /// </summary>
    public class ContentRouteHandler
    {
        #region Public-Members

        /// <summary>
        /// The FileMode value to use when accessing files within a content route via a FileStream.  Default is FileMode.Open.
        /// </summary>
        public FileMode ContentFileMode = FileMode.Open;

        /// <summary>
        /// The FileAccess value to use when accessing files within a content route via a FileStream.  Default is FileAccess.Read.
        /// </summary>
        public FileAccess ContentFileAccess = FileAccess.Read;

        /// <summary>
        /// The FileShare value to use when accessing files within a content route via a FileStream.  Default is FileShare.Read.
        /// </summary>
        public FileShare ContentFileShare = FileShare.Read;

        #endregion

        #region Private-Members
         
        private ContentRouteManager _Routes;

        #endregion

        #region Constructors-and-Factories
         
        internal ContentRouteHandler(ContentRouteManager routes)
        { 
            if (routes == null) throw new ArgumentNullException(nameof(routes));
             
            _Routes = routes; 
        }

        #endregion

        #region Internal-Methods

        internal async Task Process(HttpContext ctx, CancellationToken token)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (ctx.Request == null) throw new ArgumentNullException(nameof(ctx.Request));
            if (ctx.Response == null) throw new ArgumentNullException(nameof(ctx.Response));

            if (ctx.Request.Method != HttpMethod.GET 
                && ctx.Request.Method != HttpMethod.HEAD)
            {
                Set500Response(ctx);
                await ctx.Response.Send(token).ConfigureAwait(false);
                return;
            }
             
            string filePath = ctx.Request.Url.RawWithoutQuery;
            if (!String.IsNullOrEmpty(filePath))
            {
                while (filePath.StartsWith("/")) filePath = filePath.Substring(1); 
            }

            string baseDirectory = _Routes.BaseDirectory;
            baseDirectory = baseDirectory.Replace("\\", "/");
            if (!baseDirectory.EndsWith("/")) baseDirectory += "/";

            filePath = baseDirectory + filePath;
            filePath = filePath.Replace("+", " ").Replace("%20", " ");
             
            string contentType = GetContentType(filePath);

            if (!File.Exists(filePath))
            {
                Set404Response(ctx);
                await ctx.Response.Send(token).ConfigureAwait(false);
                return;
            }
             
            FileInfo fi = new FileInfo(filePath);
            long contentLength = fi.Length;
                  
            if (ctx.Request.Method == HttpMethod.GET)
            {
                FileStream fs = new FileStream(filePath, ContentFileMode, ContentFileAccess, ContentFileShare);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = contentLength;
                ctx.Response.ContentType = GetContentType(filePath);
                await ctx.Response.Send(contentLength, fs, token).ConfigureAwait(false);
                return;
            }
            else if (ctx.Request.Method == HttpMethod.HEAD)
            {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentLength = contentLength;
                ctx.Response.ContentType = GetContentType(filePath);
                await ctx.Response.Send(contentLength, token).ConfigureAwait(false);
                return;
            }
            else
            {
                Set500Response(ctx);
                await ctx.Response.Send(token).ConfigureAwait(false);
                return;
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

        private void Set204Response(HttpContext ctx)
        {
            ctx.Response.StatusCode = 204;
            ctx.Response.ContentLength = 0;
        }

        private void Set404Response(HttpContext ctx)
        {
            ctx.Response.StatusCode = 404;
            ctx.Response.ContentLength = 0;
        }

        private void Set500Response(HttpContext ctx)
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentLength = 0;
        }

        #endregion
    }
}
