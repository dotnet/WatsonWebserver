namespace WatsonWebserver.Core.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages an ordered list of middleware delegates and executes them as a pipeline.
    /// Middleware is registered before the server starts and executed per-request.
    /// </summary>
    public class MiddlewarePipeline
    {
        #region Public-Members

        /// <summary>
        /// True if the pipeline has one or more middleware registered.
        /// </summary>
        public bool HasMiddleware
        {
            get { return _Middlewares.Count > 0; }
        }

        #endregion

        #region Private-Members

        private readonly List<MiddlewareDelegate> _Middlewares = new List<MiddlewareDelegate>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public MiddlewarePipeline()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a middleware delegate to the end of the pipeline.
        /// Must be called before the server starts.
        /// </summary>
        /// <param name="middleware">The middleware delegate to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when middleware is null.</exception>
        public void Add(MiddlewareDelegate middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _Middlewares.Add(middleware);
        }

        /// <summary>
        /// Add a middleware delegate without a cancellation token parameter.
        /// </summary>
        /// <param name="middleware">The middleware delegate to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when middleware is null.</exception>
        public void Add(Func<HttpContextBase, Func<Task>, Task> middleware)
        {
            if (middleware == null) throw new ArgumentNullException(nameof(middleware));
            _Middlewares.Add((ctx, next, token) => middleware(ctx, next));
        }

        /// <summary>
        /// Execute the middleware pipeline, terminating with the provided handler.
        /// When no middleware is registered, the terminal handler is called directly.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="terminalHandler">The route handler to invoke at the end of the pipeline.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when context or terminalHandler is null.</exception>
        public async Task Execute(HttpContextBase context, Func<Task> terminalHandler, CancellationToken token)
        {
            if (terminalHandler == null) throw new ArgumentNullException(nameof(terminalHandler));

            if (_Middlewares.Count == 0)
            {
                await terminalHandler().ConfigureAwait(false);
                return;
            }

            int index = -1;

            Func<Task> next = null;
            next = () =>
            {
                index++;
                if (index < _Middlewares.Count)
                {
                    return _Middlewares[index](context, next, token);
                }
                else
                {
                    return terminalHandler();
                }
            };

            await next().ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
