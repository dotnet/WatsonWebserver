namespace WatsonWebserver.Core.Middleware
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Delegate for middleware that participates in the request pipeline.
    /// Call <paramref name="next"/> to continue to the next middleware or the route handler.
    /// Do not call <paramref name="next"/> to short-circuit the pipeline.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="next">Invokes the next middleware or the terminal route handler.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Task.</returns>
    public delegate Task MiddlewareDelegate(HttpContextBase context, Func<Task> next, CancellationToken token);
}
