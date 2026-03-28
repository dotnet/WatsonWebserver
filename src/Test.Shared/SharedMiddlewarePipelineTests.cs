namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Middleware;

    /// <summary>
    /// Shared MiddlewarePipeline tests that can execute in both runners.
    /// </summary>
    public static class SharedMiddlewarePipelineTests
    {
        /// <summary>
        /// Get the shared MiddlewarePipeline test cases.
        /// </summary>
        /// <returns>Ordered shared test cases.</returns>
        public static IReadOnlyList<SharedNamedTestCase> GetTests()
        {
            List<SharedNamedTestCase> tests = new List<SharedNamedTestCase>();

            tests.Add(CreateAsync("MiddlewarePipeline :: No middleware calls terminal", TestNoMiddlewareCallsTerminalAsync));
            tests.Add(CreateAsync("MiddlewarePipeline :: Single middleware wraps terminal", TestSingleMiddlewareWrapsTerminalAsync));
            tests.Add(CreateAsync("MiddlewarePipeline :: Multiple middleware execute in order", TestMultipleMiddlewareExecuteInOrderAsync));
            tests.Add(CreateAsync("MiddlewarePipeline :: Short-circuit skips terminal", TestShortCircuitSkipsTerminalAsync));
            tests.Add(CreateSync("MiddlewarePipeline :: HasMiddleware false when empty", TestHasMiddlewareFalseWhenEmpty));
            tests.Add(CreateSync("MiddlewarePipeline :: HasMiddleware true when added", TestHasMiddlewareTrueWhenAdded));
            tests.Add(CreateSync("MiddlewarePipeline :: Add throws on null", TestAddThrowsOnNull));
            tests.Add(CreateAsync("MiddlewarePipeline :: Simplified add works", TestSimplifiedAddWorksAsync));

            return tests.ToArray();
        }

        private static SharedNamedTestCase CreateSync(string name, Action action)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SharedNamedTestCase(name, delegate
            {
                action();
                return Task.CompletedTask;
            });
        }

        private static SharedNamedTestCase CreateAsync(string name, Func<Task> action)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (action == null) throw new ArgumentNullException(nameof(action));

            return new SharedNamedTestCase(name, action);
        }

        private static async Task TestNoMiddlewareCallsTerminalAsync()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool terminalCalled = false;

            await pipeline.Execute(null, delegate
            {
                terminalCalled = true;
                return Task.CompletedTask;
            }, CancellationToken.None).ConfigureAwait(false);

            AssertTrue(terminalCalled, "Terminal delegate should be called.");
        }

        private static async Task TestSingleMiddlewareWrapsTerminalAsync()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            string order = String.Empty;

            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next, CancellationToken token)
            {
                order += "before;";
                await next().ConfigureAwait(false);
                order += "after;";
            });

            await pipeline.Execute(null, delegate
            {
                order += "terminal;";
                return Task.CompletedTask;
            }, CancellationToken.None).ConfigureAwait(false);

            AssertEquals("before;terminal;after;", order, "Unexpected middleware order.");
        }

        private static async Task TestMultipleMiddlewareExecuteInOrderAsync()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            string order = String.Empty;

            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next, CancellationToken token)
            {
                order += "1;";
                await next().ConfigureAwait(false);
            });

            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next, CancellationToken token)
            {
                order += "2;";
                await next().ConfigureAwait(false);
            });

            await pipeline.Execute(null, delegate
            {
                order += "terminal;";
                return Task.CompletedTask;
            }, CancellationToken.None).ConfigureAwait(false);

            AssertEquals("1;2;terminal;", order, "Unexpected middleware execution order.");
        }

        private static async Task TestShortCircuitSkipsTerminalAsync()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool terminalCalled = false;

            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next, CancellationToken token)
            {
                await Task.CompletedTask.ConfigureAwait(false);
            });

            await pipeline.Execute(null, delegate
            {
                terminalCalled = true;
                return Task.CompletedTask;
            }, CancellationToken.None).ConfigureAwait(false);

            AssertTrue(!terminalCalled, "Terminal should not be called after short-circuit.");
        }

        private static void TestHasMiddlewareFalseWhenEmpty()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            AssertTrue(!pipeline.HasMiddleware, "Pipeline should report empty middleware.");
        }

        private static void TestHasMiddlewareTrueWhenAdded()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next, CancellationToken token)
            {
                await next().ConfigureAwait(false);
            });

            AssertTrue(pipeline.HasMiddleware, "Pipeline should report middleware after add.");
        }

        private static void TestAddThrowsOnNull()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();

            try
            {
                pipeline.Add((MiddlewareDelegate)null);
                throw new InvalidOperationException("Expected Add to throw for a null delegate.");
            }
            catch (ArgumentNullException)
            {
            }
        }

        private static async Task TestSimplifiedAddWorksAsync()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool called = false;

            pipeline.Add(async delegate (HttpContextBase context, Func<Task> next)
            {
                called = true;
                await next().ConfigureAwait(false);
            });

            await pipeline.Execute(null, delegate { return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
            AssertTrue(called, "Simplified middleware delegate should execute.");
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void AssertEquals<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + " Actual: " + actual);
            }
        }
    }
}
