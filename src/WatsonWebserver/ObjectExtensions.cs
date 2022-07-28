using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace WatsonWebserver
{
    /// <summary>
    /// Object extensions.
    /// </summary>
    public static class ObjectExtensions
    {
        /// <summary>
        /// Return a JSON string of the object.
        /// </summary>
        /// <param name="obj">Object.</param>
        /// <param name="pretty">Enable or disable pretty print.</param>
        /// <returns>JSON string.</returns>
        public static string ToJson(this object obj, bool pretty = false)
        {
            string json;

            if (pretty)
            {
                json = JsonConvert.SerializeObject(
                    obj,
                    Newtonsoft.Json.Formatting.Indented,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DateTimeZoneHandling = DateTimeZoneHandling.Local,
                    });
            }
            else
            {
                json = JsonConvert.SerializeObject(obj,
                    new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        DateTimeZoneHandling = DateTimeZoneHandling.Local
                    });
            }

            return json;
        }
    }
}
