namespace WatsonWebserver.Core.Http3
{
    using System;

    /// <summary>
    /// HTTP/3 GOAWAY payload.
    /// </summary>
    public class Http3GoAwayFrame
    {
        /// <summary>
        /// Stream identifier carried by GOAWAY.
        /// </summary>
        public long Identifier
        {
            get
            {
                return _Identifier;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Identifier));
                _Identifier = value;
            }
        }

        private long _Identifier = 0;
    }
}
