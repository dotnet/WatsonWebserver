namespace Test.Shared.Aot
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated JSON metadata for the application types used by the AOT serialization tests,
    /// declared the way a native AOT application declares its own types.
    /// </summary>
    [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
    [JsonSerializable(typeof(AotTestDto))]
    [JsonSerializable(typeof(AotTestItem))]
    [JsonSerializable(typeof(AotTestExample))]
    [JsonSerializable(typeof(InvalidOperationException))]
    internal partial class AotTestJsonContext : JsonSerializerContext
    {
    }
}
