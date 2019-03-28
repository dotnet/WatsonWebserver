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
    /// Access control mode of operation.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AccessControlMode
    {
        [EnumMember(Value = "DefaultPermit")]
        DefaultPermit,
        [EnumMember(Value = "DefaultDeny")]
        DefaultDeny
    }
}
