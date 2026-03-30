namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 SETTINGS parameter.
    /// </summary>
    public class Http2SettingsParameter
    {
        /// <summary>
        /// Setting identifier.
        /// </summary>
        public Http2SettingIdentifier Identifier { get; set; } = Http2SettingIdentifier.HeaderTableSize;

        /// <summary>
        /// Setting value.
        /// </summary>
        public uint Value { get; set; } = 0;
    }
}
