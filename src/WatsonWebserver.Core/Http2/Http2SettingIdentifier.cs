namespace WatsonWebserver.Core.Http2
{
    /// <summary>
    /// HTTP/2 SETTINGS identifiers.
    /// </summary>
    public enum Http2SettingIdentifier : ushort
    {
        /// <summary>
        /// HEADER_TABLE_SIZE.
        /// </summary>
        HeaderTableSize = 1,
        /// <summary>
        /// ENABLE_PUSH.
        /// </summary>
        EnablePush = 2,
        /// <summary>
        /// MAX_CONCURRENT_STREAMS.
        /// </summary>
        MaxConcurrentStreams = 3,
        /// <summary>
        /// INITIAL_WINDOW_SIZE.
        /// </summary>
        InitialWindowSize = 4,
        /// <summary>
        /// MAX_FRAME_SIZE.
        /// </summary>
        MaxFrameSize = 5,
        /// <summary>
        /// MAX_HEADER_LIST_SIZE.
        /// </summary>
        MaxHeaderListSize = 6
    }
}
