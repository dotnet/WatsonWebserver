namespace WatsonWebserver.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Webserver statistics.
    /// </summary>
    public class WebserverStatistics
    {
        #region Public-Members

        /// <summary>
        /// The time at which the client or server was started.
        /// </summary>
        public DateTime StartTime
        {
            get
            {
                return _StartTime;
            }
        }

        /// <summary>
        /// The amount of time which the client or server has been up.
        /// </summary>
        public TimeSpan UpTime
        {
            get
            {
                return DateTime.Now.ToUniversalTime() - _StartTime;
            }
        }

        /// <summary>
        /// The number of payload bytes received (incoming request body).
        /// </summary>
        public long ReceivedPayloadBytes
        {
            get
            {
                return _ReceivedPayloadBytes;
            }
            internal set
            {
                _ReceivedPayloadBytes = value;
            }
        }

        /// <summary>
        /// The number of payload bytes sent (outgoing request body).
        /// </summary>
        public long SentPayloadBytes
        {
            get
            {
                return _SentPayloadBytes;
            }
            internal set
            {
                _SentPayloadBytes = value;
            }
        }

        #endregion

        #region Private-Members

        private DateTime _StartTime = DateTime.Now.ToUniversalTime();
        private long _ReceivedPayloadBytes = 0;
        private long _SentPayloadBytes = 0;
        private long[] _RequestsByMethod; // _RequestsByMethod[(int)HttpMethod.Xyz] = Count

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the statistics object.
        /// </summary>
        public WebserverStatistics()
        {
            // Calculating the length for _RequestsByMethod array
            int max = 0;
            foreach (var value in Enum.GetValues(typeof(HttpMethod)))
            {
                if ((int)value > max)
                    max = (int)value;
            }

            _RequestsByMethod = new long[max + 1];
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human-readable version of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret = "";

            ret +=
                Environment.NewLine + 
                "--- Statistics ---" + Environment.NewLine +
                "    Start Time     : " + StartTime.ToString() + Environment.NewLine +
                "    Up Time        : " + UpTime.ToString("h'h 'm'm 's's'") + Environment.NewLine +
                "    Received Bytes : " + ReceivedPayloadBytes.ToString("N0") + " bytes" + Environment.NewLine +
                "    Sent Bytes     : " + SentPayloadBytes.ToString("N0") + " bytes" + Environment.NewLine;

            return ret;
        }

        /// <summary>
        /// Reset statistics other than StartTime and UpTime.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _ReceivedPayloadBytes, 0);
            Interlocked.Exchange(ref _SentPayloadBytes, 0);

            for (int i = 0; i < _RequestsByMethod.Length; i++)
                Interlocked.Exchange(ref _RequestsByMethod[i], 0);
        }

        /// <summary>
        /// Increment request counter.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        public void IncrementRequestCounter(HttpMethod method)
        {
            Interlocked.Increment(ref _RequestsByMethod[(int)method]);
        }

        /// <summary>
        /// Increment received payload bytes.
        /// </summary>
        /// <param name="len">Length.</param>
        public void IncrementReceivedPayloadBytes(long len)
        {
            Interlocked.Add(ref _ReceivedPayloadBytes, len);
        }

        /// <summary>
        /// Increment sent payload bytes.
        /// </summary>
        /// <param name="len">Length.</param>
        public void IncrementSentPayloadBytes(long len)
        {
            Interlocked.Add(ref _SentPayloadBytes, len);
        }

        #endregion

        #region Private-Members

        #endregion
    }
}
