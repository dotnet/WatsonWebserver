namespace WatsonWebserver.Core.Http3
{
    /// <summary>
    /// HTTP/3 unidirectional stream types.
    /// </summary>
    public enum Http3StreamType : long
    {
        /// <summary>
        /// Control stream.
        /// </summary>
        Control = 0x00,
        /// <summary>
        /// QPACK encoder stream.
        /// </summary>
        QpackEncoder = 0x02,
        /// <summary>
        /// QPACK decoder stream.
        /// </summary>
        QpackDecoder = 0x03,
        /// <summary>
        /// Push stream.
        /// </summary>
        Push = 0x01
    }
}
