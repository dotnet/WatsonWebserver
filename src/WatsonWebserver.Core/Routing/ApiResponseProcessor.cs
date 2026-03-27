namespace WatsonWebserver.Core.Routing
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// Processes the return value from an API route handler and sends the appropriate HTTP response.
    /// Handles null, string, primitive, tuple, and object return types.
    /// </summary>
    internal static class ApiResponseProcessor
    {
        /// <summary>
        /// Process the return value from an API route handler and send the response.
        /// </summary>
        /// <param name="ctx">The HTTP context.</param>
        /// <param name="result">The return value from the route handler.</param>
        /// <param name="serializer">The serialization helper for JSON serialization.</param>
        /// <returns>Task.</returns>
        internal static async Task ProcessResultAsync(HttpContextBase ctx, object result, ISerializationHelper serializer)
        {
            if (ctx.Response.ServerSentEvents || ctx.Response.ChunkedTransfer)
            {
                return;
            }

            if (result == null)
            {
                await ctx.Response.Send().ConfigureAwait(false);
                return;
            }

            string stringResult = result as string;
            if (stringResult != null)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send(stringResult).ConfigureAwait(false);
                return;
            }

            if (result.GetType().IsPrimitive)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send(result.ToString()).ConfigureAwait(false);
                return;
            }

            Type resultType = result.GetType();

            if (resultType.Name.StartsWith("ValueTuple`"))
            {
                if (TryProcessTuple(resultType, result, out object tupleBody, out int tupleStatus))
                {
                    ctx.Response.StatusCode = tupleStatus;
                    await SendBodyAsync(ctx, tupleBody, serializer).ConfigureAwait(false);
                    return;
                }
            }

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Tuple<,>))
            {
                PropertyInfo item1Prop = resultType.GetProperty("Item1");
                PropertyInfo item2Prop = resultType.GetProperty("Item2");

                if (item1Prop != null && item2Prop != null)
                {
                    object item1 = item1Prop.GetValue(result);
                    int statusCode = Convert.ToInt32(item2Prop.GetValue(result));
                    ctx.Response.StatusCode = statusCode;
                    await SendBodyAsync(ctx, item1, serializer).ConfigureAwait(false);
                    return;
                }
            }

            if (String.IsNullOrEmpty(ctx.Response.ContentType))
                ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(result, false)).ConfigureAwait(false);
        }

        private static bool TryProcessTuple(Type resultType, object result, out object body, out int statusCode)
        {
            body = null;
            statusCode = 200;

            // ValueTuple uses fields, not properties
            FieldInfo item1Field = resultType.GetField("Item1");
            FieldInfo item2Field = resultType.GetField("Item2");

            if (item1Field != null && item2Field != null)
            {
                body = item1Field.GetValue(result);
                statusCode = Convert.ToInt32(item2Field.GetValue(result));
                return true;
            }

            return false;
        }

        private static async Task SendBodyAsync(HttpContextBase ctx, object body, ISerializationHelper serializer)
        {
            if (body == null)
            {
                await ctx.Response.Send().ConfigureAwait(false);
                return;
            }

            string bodyString = body as string;
            if (bodyString != null)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send(bodyString).ConfigureAwait(false);
                return;
            }

            if (body.GetType().IsPrimitive)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";
                await ctx.Response.Send(body.ToString()).ConfigureAwait(false);
                return;
            }

            if (String.IsNullOrEmpty(ctx.Response.ContentType))
                ctx.Response.ContentType = "application/json";
            await ctx.Response.Send(serializer.SerializeJson(body, false)).ConfigureAwait(false);
        }
    }
}
