namespace Test.Serialization
{
    using Newtonsoft.Json;
    using WatsonWebserver.Core;

    /// <summary>
    /// Newtonsoft.Json-based serializer implementation for sample use.
    /// </summary>
    internal class NewtonsoftSerializer : ISerializationHelper
    {
        /// <inheritdoc />
        public T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <inheritdoc />
        public string SerializeJson(object obj, bool pretty = true)
        {
            if (!pretty)
            {
                return JsonConvert.SerializeObject(obj);
            }
            else
            {
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
        }
    }
}
