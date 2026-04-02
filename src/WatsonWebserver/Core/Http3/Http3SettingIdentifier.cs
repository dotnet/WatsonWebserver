namespace WatsonWebserver.Core.Http3
{
    /// <summary>
    /// HTTP/3 setting identifiers.
    /// </summary>
    public enum Http3SettingIdentifier : long
    {
        /// <summary>
        /// QPACK dynamic table capacity.
        /// </summary>
        QpackMaxTableCapacity = 0x1,
        /// <summary>
        /// Maximum field section size.
        /// </summary>
        MaxFieldSectionSize = 0x6,
        /// <summary>
        /// QPACK blocked streams.
        /// </summary>
        QpackBlockedStreams = 0x7,
        /// <summary>
        /// HTTP datagram support.
        /// </summary>
        H3Datagram = 0x33
    }
}
