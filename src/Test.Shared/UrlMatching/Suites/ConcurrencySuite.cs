#nullable enable
namespace Test.Shared.UrlMatching.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using UrlMatcher;
    using static Test.Shared.UrlMatching.CaseFactory;

    /// <summary>
    /// Thread safety of shared Matcher and UrlPattern instances.
    /// </summary>
    public static class ConcurrencySuite
    {
        private const string Id = "UrlMatcher.Concurrency";
        private const int Workers = 8;
        private const int Iterations = 5000;

        /// <summary>
        /// Create the suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(
                suiteId: Id,
                displayName: "UrlMatcher: Concurrency",
                cases: new List<TestCaseDescriptor>
                {
                    CaseAsync(Id, "SharedMatcherInstance", "One Matcher shared across threads", async ct =>
                    {
                        Matcher matcher = new Matcher("/api/users/42/orders");
                        await RunParallelAsync(i =>
                        {
                            if (!matcher.Match("/api/users/{id}/orders", out NameValueCollection a) || a["id"] != "42")
                                throw new AssertionFailedException("parameter match failed on iteration " + i);
                            if (!matcher.Match("/api/{*rest}", out NameValueCollection b) || b["rest"] != "users/42/orders")
                                throw new AssertionFailedException("catch-all match failed on iteration " + i);
                            if (matcher.Match("/other/{*rest}", out NameValueCollection c) || c.Count != 0)
                                throw new AssertionFailedException("negative match failed on iteration " + i);
                        }, ct).ConfigureAwait(false);
                    }),
                    CaseAsync(Id, "SharedUrlPatternInstance", "One UrlPattern shared across threads", async ct =>
                    {
                        UrlPattern pattern = UrlPattern.Parse("/{v}/files/{*path}");
                        await RunParallelAsync(i =>
                        {
                            string url = "/v" + i + "/files/a/" + i;
                            if (!Matcher.Match(url, pattern, out NameValueCollection vals) || vals["v"] != "v" + i || vals["path"] != "a/" + i)
                                throw new AssertionFailedException("match failed for " + url + ": " + MatchVerifier.Describe(vals));
                            if (Matcher.Match("/v" + i + "/docs/a", pattern, out NameValueCollection none) || none.Count != 0)
                                throw new AssertionFailedException("negative match failed on iteration " + i);
                        }, ct).ConfigureAwait(false);
                    }),
                    CaseAsync(Id, "StaticMethods", "Static methods called from many threads", async ct =>
                    {
                        await RunParallelAsync(i =>
                        {
                            string url = "/users/" + i;
                            if (!Matcher.Match(url, "/users/{id}", out NameValueCollection vals) || vals["id"] != i.ToString())
                                throw new AssertionFailedException("match failed for " + url);
                        }, ct).ConfigureAwait(false);
                    })
                });
        }

        private static async Task RunParallelAsync(Action<int> body, CancellationToken token)
        {
            Task[] tasks = new Task[Workers];
            for (int w = 0; w < Workers; w++)
            {
                int worker = w;
                tasks[w] = Task.Run(() =>
                {
                    for (int i = 0; i < Iterations; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        body(worker * Iterations + i);
                    }
                }, token);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}
