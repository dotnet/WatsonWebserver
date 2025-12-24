namespace Test.Serialization
{
    using Newtonsoft.Json;
    using WatsonWebserver.Core;

    internal class NewtonsoftSerializer : ISerializationHelper
    {
        public T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

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
