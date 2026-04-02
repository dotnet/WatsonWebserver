namespace WatsonWebserver.Core.Routing
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core.Middleware;

    /// <summary>
    /// Internal factory that wraps Func&lt;ApiRequest, Task&lt;object&gt;&gt; handlers into Func&lt;HttpContextBase, Task&gt; handlers
    /// compatible with the Watson routing system. Handles body deserialization, response processing,
    /// middleware execution, timeout management, and exception mapping.
    /// </summary>
    internal static class ApiRouteHandler
    {
        /// <summary>
        /// Create a wrapped handler for a route without automatic body deserialization.
        /// </summary>
        internal static Func<HttpContextBase, Task> WrapNoBody(
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings)
        {
            return async (HttpContextBase ctx) =>
            {
                CancellationTokenSource timeoutCts = null;
                CancellationToken requestToken = ctx.Token;

                try
                {
                    TimeSpan timeout = settings.Timeout.DefaultTimeout;
                    if (timeout > TimeSpan.Zero)
                    {
                        timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.Token);
                        timeoutCts.CancelAfter(timeout);
                        requestToken = timeoutCts.Token;
                    }

                    ApiRequest apiReq = new ApiRequest(ctx, serializer, null, requestToken);
                    apiReq.Metadata = ctx.Metadata;

                    Func<Task> terminalHandler = async () =>
                    {
                        object result = await handler(apiReq).ConfigureAwait(false);
                        await ApiResponseProcessor.ProcessResultAsync(ctx, result, serializer).ConfigureAwait(false);
                    };

                    if (middleware != null && middleware.HasMiddleware)
                    {
                        await middleware.Execute(ctx, terminalHandler, requestToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await terminalHandler().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested && !ctx.Token.IsCancellationRequested)
                {
                    await SendTimeoutResponseAsync(ctx, serializer).ConfigureAwait(false);
                }
                catch (WebserverException wex)
                {
                    await SendWebserverExceptionResponseAsync(ctx, wex, serializer).ConfigureAwait(false);
                }
                catch (JsonException jex)
                {
                    await SendJsonExceptionResponseAsync(ctx, jex, serializer).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await SendGenericExceptionResponseAsync(ctx, ex, serializer).ConfigureAwait(false);
                }
                finally
                {
                    timeoutCts?.Dispose();
                }
            };
        }

        /// <summary>
        /// Create a wrapped handler for a route with automatic body deserialization.
        /// </summary>
        internal static Func<HttpContextBase, Task> WrapWithBody<T>(
            Func<ApiRequest, Task<object>> handler,
            ISerializationHelper serializer,
            MiddlewarePipeline middleware,
            WebserverSettings settings) where T : class
        {
            return async (HttpContextBase ctx) =>
            {
                CancellationTokenSource timeoutCts = null;
                CancellationToken requestToken = ctx.Token;

                try
                {
                    TimeSpan timeout = settings.Timeout.DefaultTimeout;
                    if (timeout > TimeSpan.Zero)
                    {
                        timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ctx.Token);
                        timeoutCts.CancelAfter(timeout);
                        requestToken = timeoutCts.Token;
                    }

                    T requestData = null;
                    if (!String.IsNullOrEmpty(ctx.Request.DataAsString))
                    {
                        if (typeof(T) == typeof(string))
                        {
                            requestData = (T)(object)ctx.Request.DataAsString;
                        }
                        else if (typeof(T).IsPrimitive)
                        {
                            requestData = (T)Convert.ChangeType(ctx.Request.DataAsString, typeof(T));
                        }
                        else
                        {
                            requestData = serializer.DeserializeJson<T>(ctx.Request.DataAsString);
                        }
                    }

                    ApiRequest apiReq = new ApiRequest(ctx, serializer, requestData, requestToken);
                    apiReq.Metadata = ctx.Metadata;

                    Func<Task> terminalHandler = async () =>
                    {
                        object result = await handler(apiReq).ConfigureAwait(false);
                        await ApiResponseProcessor.ProcessResultAsync(ctx, result, serializer).ConfigureAwait(false);
                    };

                    if (middleware != null && middleware.HasMiddleware)
                    {
                        await middleware.Execute(ctx, terminalHandler, requestToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await terminalHandler().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (timeoutCts != null && timeoutCts.IsCancellationRequested && !ctx.Token.IsCancellationRequested)
                {
                    await SendTimeoutResponseAsync(ctx, serializer).ConfigureAwait(false);
                }
                catch (WebserverException wex)
                {
                    await SendWebserverExceptionResponseAsync(ctx, wex, serializer).ConfigureAwait(false);
                }
                catch (JsonException jex)
                {
                    await SendJsonExceptionResponseAsync(ctx, jex, serializer).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await SendGenericExceptionResponseAsync(ctx, ex, serializer).ConfigureAwait(false);
                }
                finally
                {
                    timeoutCts?.Dispose();
                }
            };
        }

        private static async Task SendTimeoutResponseAsync(HttpContextBase ctx, ISerializationHelper serializer)
        {
            ctx.Response.StatusCode = 408;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(new ApiErrorResponse
            {
                Error = ApiResultEnum.RequestTimeout,
                Message = "The request timed out."
            }, false)).ConfigureAwait(false);
        }

        private static async Task SendWebserverExceptionResponseAsync(HttpContextBase ctx, WebserverException wex, ISerializationHelper serializer)
        {
            ctx.Response.StatusCode = wex.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(new ApiErrorResponse
            {
                Error = wex.Result,
                Message = wex.Message,
                Data = wex.Data
            }, false)).ConfigureAwait(false);
        }

        private static async Task SendJsonExceptionResponseAsync(HttpContextBase ctx, JsonException jex, ISerializationHelper serializer)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(new ApiErrorResponse
            {
                Error = ApiResultEnum.DeserializationError,
                Message = jex.Message
            }, false)).ConfigureAwait(false);
        }

        private static async Task SendGenericExceptionResponseAsync(HttpContextBase ctx, Exception ex, ISerializationHelper serializer)
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(new ApiErrorResponse
            {
                Error = ApiResultEnum.InternalError,
                Message = ex.Message
            }, false)).ConfigureAwait(false);
        }
    }
}
