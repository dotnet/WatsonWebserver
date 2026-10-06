namespace Test.Aot
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated JSON metadata for this application's types, as a native AOT application declares it.
    /// </summary>
    [JsonSourceGenerationOptions(UseStringEnumConverter = true)]
    [JsonSerializable(typeof(AotUser))]
    [JsonSerializable(typeof(AotCreateUserRequest))]
    internal partial class AotJsonContext : JsonSerializerContext
    {
    }
}
