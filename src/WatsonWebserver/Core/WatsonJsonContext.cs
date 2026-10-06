namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using WatsonWebserver.Core.Health;

    /// <summary>
    /// Source-generated JSON metadata for the types Watson serializes itself: API error responses,
    /// health-check results, the OpenAPI document graph, and the primitive and collection types that
    /// commonly appear in their object-typed members. This metadata needs no reflection, so Watson's own
    /// responses serialize under native AOT and trimming. Enums are written as strings, matching the
    /// reflection-based serializer.
    /// </summary>
    [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
    [JsonSerializable(typeof(ApiErrorResponse))]
    [JsonSerializable(typeof(HealthCheckResult))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(byte))]
    [JsonSerializable(typeof(sbyte))]
    [JsonSerializable(typeof(short))]
    [JsonSerializable(typeof(ushort))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(uint))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(decimal))]
    [JsonSerializable(typeof(char))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTimeOffset))]
    [JsonSerializable(typeof(TimeSpan))]
    [JsonSerializable(typeof(object[]))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(int[]))]
    [JsonSerializable(typeof(List<object>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<int>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, List<string>>))]
    [JsonSerializable(typeof(Dictionary<string, Dictionary<string, object>>))]
    [JsonSerializable(typeof(List<Dictionary<string, object>>))]
    [JsonSerializable(typeof(List<Dictionary<string, List<string>>>))]
    internal partial class WatsonJsonContext : JsonSerializerContext
    {
    }
}
