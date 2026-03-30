namespace Test.BrowserInterop
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Waits for a matching browser navigation observation.
    /// </summary>
    internal sealed class BrowserNavigationWaiter
    {
        private readonly string _Url;
        private readonly TaskCompletionSource<BrowserNavigationObservation> _TaskCompletionSource = new TaskCompletionSource<BrowserNavigationObservation>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Instantiate the waiter.
        /// </summary>
        /// <param name="url">Expected navigation URL.</param>
        public BrowserNavigationWaiter(string url)
        {
            if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
            _Url = url;
        }

        /// <summary>
        /// Attempt to satisfy the waiter with an observation.
        /// </summary>
        /// <param name="observation">Observed navigation result.</param>
        public void TrySetObservation(BrowserNavigationObservation observation)
        {
            if (observation == null) return;
            if (!String.Equals(observation.Url, _Url, StringComparison.OrdinalIgnoreCase)
                && !observation.Url.StartsWith(_Url, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _TaskCompletionSource.TrySetResult(observation);
        }

        /// <summary>
        /// Wait for the observation.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Observed navigation result.</returns>
        public Task<BrowserNavigationObservation> WaitAsync(CancellationToken token)
        {
            if (!token.CanBeCanceled) return _TaskCompletionSource.Task;
            return _TaskCompletionSource.Task.WaitAsync(token);
        }
    }
}
