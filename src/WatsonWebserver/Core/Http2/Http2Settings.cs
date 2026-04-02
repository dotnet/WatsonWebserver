namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// Typed HTTP/2 settings payload and local configuration.
    /// </summary>
    public class Http2Settings
    {
        /// <summary>
        /// HPACK dynamic table size.
        /// </summary>
        public uint HeaderTableSize { get; set; } = Http2Constants.DefaultHeaderTableSize;

        /// <summary>
        /// Enable server push.
        /// </summary>
        public bool EnablePush { get; set; } = false;

        /// <summary>
        /// Maximum concurrent streams.
        /// </summary>
        public uint MaxConcurrentStreams { get; set; } = 100;

        /// <summary>
        /// Initial stream flow-control window size.
        /// </summary>
        public int InitialWindowSize
        {
            get
            {
                return _InitialWindowSize;
            }
            set
            {
                if (value < 0 || value > Http2Constants.MaxInitialWindowSize) throw new ArgumentOutOfRangeException(nameof(InitialWindowSize));
                _InitialWindowSize = value;
            }
        }

        /// <summary>
        /// Maximum frame size accepted from peers.
        /// </summary>
        public int MaxFrameSize
        {
            get
            {
                return _MaxFrameSize;
            }
            set
            {
                if (value < Http2Constants.MinMaxFrameSize || value > Http2Constants.MaxMaxFrameSize) throw new ArgumentOutOfRangeException(nameof(MaxFrameSize));
                _MaxFrameSize = value;
            }
        }

        /// <summary>
        /// Maximum header list size accepted from peers.
        /// </summary>
        public uint MaxHeaderListSize { get; set; } = Http2Constants.DefaultMaxHeaderListSize;

        private int _InitialWindowSize = Http2Constants.DefaultInitialWindowSize;
        private int _MaxFrameSize = Http2Constants.DefaultMaxFrameSize;
    }
}
