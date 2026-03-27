namespace Test.XUnit
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core.Middleware;
    using Xunit;

    public class MiddlewarePipelineTests
    {
        [Fact]
        public async Task NoMiddleware_CallsTerminal()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool terminalCalled = false;

            await pipeline.Execute(null, () => { terminalCalled = true; return Task.CompletedTask; }, CancellationToken.None);

            Assert.True(terminalCalled);
        }

        [Fact]
        public async Task SingleMiddleware_WrapsTerminal()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            string order = "";

            pipeline.Add(async (ctx, next, token) =>
            {
                order += "before;";
                await next();
                order += "after;";
            });

            await pipeline.Execute(null, () => { order += "terminal;"; return Task.CompletedTask; }, CancellationToken.None);

            Assert.Equal("before;terminal;after;", order);
        }

        [Fact]
        public async Task MultipleMiddleware_ExecuteInOrder()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            string order = "";

            pipeline.Add(async (ctx, next, token) =>
            {
                order += "1;";
                await next();
            });

            pipeline.Add(async (ctx, next, token) =>
            {
                order += "2;";
                await next();
            });

            await pipeline.Execute(null, () => { order += "terminal;"; return Task.CompletedTask; }, CancellationToken.None);

            Assert.Equal("1;2;terminal;", order);
        }

        [Fact]
        public async Task ShortCircuit_SkipsTerminal()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool terminalCalled = false;

            pipeline.Add(async (ctx, next, token) =>
            {
                // Don't call next() - short-circuit
            });

            await pipeline.Execute(null, () => { terminalCalled = true; return Task.CompletedTask; }, CancellationToken.None);

            Assert.False(terminalCalled);
        }

        [Fact]
        public void HasMiddleware_FalseWhenEmpty()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            Assert.False(pipeline.HasMiddleware);
        }

        [Fact]
        public void HasMiddleware_TrueWhenAdded()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            pipeline.Add(async (ctx, next, token) => await next());
            Assert.True(pipeline.HasMiddleware);
        }

        [Fact]
        public void Add_ThrowsOnNull()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            Assert.Throws<ArgumentNullException>(() => pipeline.Add((MiddlewareDelegate)null));
        }

        [Fact]
        public async Task SimplifiedAdd_Works()
        {
            MiddlewarePipeline pipeline = new MiddlewarePipeline();
            bool called = false;

            pipeline.Add(async (ctx, next) =>
            {
                called = true;
                await next();
            });

            await pipeline.Execute(null, () => Task.CompletedTask, CancellationToken.None);
            Assert.True(called);
        }
    }
}
