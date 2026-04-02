namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// Typed HTTP/3 settings payload and local configuration.
    /// </summary>
    public class Http3Settings
    {
        /// <summary>
        /// Maximum field section size.
        /// </summary>
        public long MaxFieldSectionSize
        {
            get
            {
                return _MaxFieldSectionSize;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxFieldSectionSize));
                _MaxFieldSectionSize = value;
            }
        }

        /// <summary>
        /// Maximum QPACK dynamic table capacity.
        /// </summary>
        public long QpackMaxTableCapacity
        {
            get
            {
                return _QpackMaxTableCapacity;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(QpackMaxTableCapacity));
                _QpackMaxTableCapacity = value;
            }
        }

        /// <summary>
        /// Maximum QPACK blocked streams.
        /// </summary>
        public long QpackBlockedStreams
        {
            get
            {
                return _QpackBlockedStreams;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(QpackBlockedStreams));
                _QpackBlockedStreams = value;
            }
        }

        /// <summary>
        /// Whether RFC 9220 datagrams are advertised.
        /// </summary>
        public bool EnableDatagram { get; set; } = false;

        private long _MaxFieldSectionSize = 0;
        private long _QpackMaxTableCapacity = 0;
        private long _QpackBlockedStreams = 0;
    }
}
