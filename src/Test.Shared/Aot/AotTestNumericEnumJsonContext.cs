namespace Test.Shared.Aot
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Source-generated JSON metadata declared without <c>UseStringEnumConverter</c>, so application
    /// enums are written as numbers.
    /// </summary>
    [JsonSerializable(typeof(AotTestItem))]
    internal partial class AotTestNumericEnumJsonContext : JsonSerializerContext
    {
    }
}
