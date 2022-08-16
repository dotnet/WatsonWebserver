using System.Runtime.Serialization;

namespace WatsonWebserver
{
    /// <summary>
    /// Route type.
    /// </summary>
    public enum RouteTypeEnum
    {
        /// <summary>
        /// Default route.
        /// </summary>
        [EnumMember(Value = "Default")]
        Default,
        /// <summary>
        /// Content route.
        /// </summary>
        [EnumMember(Value = "Content")]
        Content,
        /// <summary>
        /// Static route.
        /// </summary>
        [EnumMember(Value = "Static")]
        Static,
        /// <summary>
        /// Parameter route.
        /// </summary>
        [EnumMember(Value = "Parameter")]
        Parameter,
        /// <summary>
        /// Dynamic route.
        /// </summary>
        [EnumMember(Value = "Dynamic")]
        Dynamic
    }
}
