using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace WatsonWebserver
{
    /// <summary>
    /// HTTP methods, i.e. GET, PUT, POST, DELETE, etc.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum HttpMethod
    {
        /// <summary>
        /// HTTP GET.
        /// </summary>
        [EnumMember(Value = "GET")]
        GET,
        /// <summary>
        /// HTTP HEAD.
        /// </summary>
        [EnumMember(Value = "HEAD")]
        HEAD,
        /// <summary>
        /// HTTP PUT.
        /// </summary>
        [EnumMember(Value = "PUT")]
        PUT,
        /// <summary>
        /// HTTP POST.
        /// </summary>
        [EnumMember(Value = "POST")]
        POST,
        /// <summary>
        /// HTTP DELETE.
        /// </summary>
        [EnumMember(Value = "DELETE")]
        DELETE,
        /// <summary>
        /// HTTP PATCH.
        /// </summary>
        [EnumMember(Value = "PATCH")]
        PATCH,
        /// <summary>
        /// HTTP CONNECT.
        /// </summary>
        [EnumMember(Value = "CONNECT")]
        CONNECT,
        /// <summary>
        /// HTTP OPTIONS.
        /// </summary>
        [EnumMember(Value = "OPTIONS")]
        OPTIONS,
        /// <summary>
        /// HTTP TRACE.
        /// </summary>
        [EnumMember(Value = "TRACE")]
        TRACE,
        /// <summary>
        /// Unknown method.
        /// </summary>
        [EnumMember(Value = "UNKNOWN")]
        UNKNOWN
    }
}
