namespace Test.XUnit
{
    using System;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.Middleware;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ApiErrorResponseTests
    {
        [Fact]
        public void StatusCode_DerivedFromError()
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = ApiResultEnum.NotFound };
            Assert.Equal(404, resp.StatusCode);
        }

        [Fact]
        public void Description_AutoPopulated()
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = ApiResultEnum.NotAuthorized };
            Assert.False(String.IsNullOrEmpty(resp.Description));
        }

        [Theory]
        [InlineData(ApiResultEnum.Success, 200)]
        [InlineData(ApiResultEnum.Created, 201)]
        [InlineData(ApiResultEnum.BadRequest, 400)]
        [InlineData(ApiResultEnum.NotAuthorized, 401)]
        [InlineData(ApiResultEnum.NotFound, 404)]
        [InlineData(ApiResultEnum.RequestTimeout, 408)]
        [InlineData(ApiResultEnum.Conflict, 409)]
        [InlineData(ApiResultEnum.InternalError, 500)]
        public void StatusCode_MapsCorrectly(ApiResultEnum error, int expectedCode)
        {
            ApiErrorResponse resp = new ApiErrorResponse { Error = error };
            Assert.Equal(expectedCode, resp.StatusCode);
        }
    }

    public class WebserverExceptionTests
    {
        [Fact]
        public void StatusCode_MapsFromResult()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.NotFound);
            Assert.Equal(404, ex.StatusCode);
        }

        [Fact]
        public void Message_CustomMessage()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.BadRequest, "Invalid input");
            Assert.Equal("Invalid input", ex.Message);
        }

        [Fact]
        public void Message_DefaultMessage()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.NotFound);
            Assert.Equal("Not found.", ex.Message);
        }

        [Fact]
        public void Data_CanBeSet()
        {
            WebserverException ex = new WebserverException(ApiResultEnum.Conflict);
            ex.Data = new { Field = "name" };
            Assert.NotNull(ex.Data);
        }

        [Fact]
        public void InnerException_Preserved()
        {
            Exception inner = new InvalidOperationException("inner");
            WebserverException ex = new WebserverException(ApiResultEnum.InternalError, "outer", inner);
            Assert.Same(inner, ex.InnerException);
        }
    }

    public class AuthResultTests
    {
        [Fact]
        public void IsPermitted_TrueWhenSuccessAndPermitted()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.Permitted
            };
            Assert.True(result.IsPermitted());
        }

        [Fact]
        public void IsPermitted_FalseWhenNotFound()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.NotFound,
                AuthorizationResult = AuthorizationResultEnum.DeniedImplicit
            };
            Assert.False(result.IsPermitted());
        }

        [Fact]
        public void IsPermitted_FalseWhenDeniedExplicit()
        {
            AuthResult result = new AuthResult
            {
                AuthenticationResult = AuthenticationResultEnum.Success,
                AuthorizationResult = AuthorizationResultEnum.DeniedExplicit
            };
            Assert.False(result.IsPermitted());
        }

        [Fact]
        public void Metadata_Propagated()
        {
            AuthResult result = new AuthResult { Metadata = new { UserId = 42 } };
            Assert.NotNull(result.Metadata);
        }
    }

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

    public class TimeoutSettingsTests
    {
        [Fact]
        public void Default_IsZero()
        {
            TimeoutSettings settings = new TimeoutSettings();
            Assert.Equal(TimeSpan.Zero, settings.DefaultTimeout);
        }

        [Fact]
        public void Constructor_SetsTimeout()
        {
            TimeoutSettings settings = new TimeoutSettings(TimeSpan.FromSeconds(30));
            Assert.Equal(TimeSpan.FromSeconds(30), settings.DefaultTimeout);
        }

        [Fact]
        public void NegativeTimeout_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                TimeoutSettings settings = new TimeoutSettings();
                settings.DefaultTimeout = TimeSpan.FromSeconds(-1);
            });
        }
    }
}
