namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of a route, allowing for varying signatures, with and without cancellation tokens.
    /// </summary>
    public class RouteImplementation
    {
        private readonly Func<HttpContextBase, CancellationToken, Task> _Implementation;

        /// <summary>
        /// Creates a RouteImplementation from a function with CancellationToken.
        /// </summary>
        public RouteImplementation(Func<HttpContextBase, CancellationToken, Task> handler)
        {
            _Implementation = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Creates a RouteImplementation from a function without CancellationToken.
        /// </summary>
        public RouteImplementation(Func<HttpContextBase, Task> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _Implementation = (ctx, token) => handler(ctx);
        }

        /// <summary>
        /// Implicit conversion from Func without CancellationToken.
        /// </summary>
        public static implicit operator RouteImplementation(Func<HttpContextBase, Task> handler)
            => handler == null ? null : new RouteImplementation(handler);

        /// <summary>
        /// Implicit conversion from Func with CancellationToken.
        /// </summary>
        public static implicit operator RouteImplementation(Func<HttpContextBase, CancellationToken, Task> handler)
            => handler == null ? null : new RouteImplementation(handler);

        /// <summary>
        /// Invokes the implementation.
        /// </summary>
        public Task InvokeAsync(HttpContextBase context, CancellationToken cancellationToken = default)
            => _Implementation?.Invoke(context, cancellationToken) ?? Task.CompletedTask;

        /// <summary>
        /// Checks if the implementation is null.
        /// </summary>
        public bool IsNull => _Implementation == null;

        /// <summary>
        /// Implicit conversion to bool for null checking.
        /// </summary>
        public static implicit operator bool(RouteImplementation handler)
            => handler?._Implementation != null;
    }
}