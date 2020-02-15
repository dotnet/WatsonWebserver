using System;
using System.Collections.Generic;
using System.Text;

namespace WatsonWebserver
{
    /// <summary>
    /// Watson webserver statistics.
    /// </summary>
    public class Statistics
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
        private readonly object _DictionaryLock = new object();
        private Dictionary<HttpMethod, long> _IncomingRequests = new Dictionary<HttpMethod, long>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the statistics object.
        /// </summary>
        public Statistics()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Human-readable version of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret =
                "--- Statistics ---" + Environment.NewLine +
                "    Start Time     : " + StartTime.ToString() + Environment.NewLine +
                "    Up Time        : " + UpTime.ToString() + Environment.NewLine +
                "    Received Payload Bytes : " + ReceivedPayloadBytes + " bytes" + Environment.NewLine +
                "    Sent Payload Bytes     : " + SentPayloadBytes + " bytes" + Environment.NewLine +
                "    Requests By Method     : " + Environment.NewLine;

            lock (_DictionaryLock)
            {
                if (_IncomingRequests.Count > 0)
                {
                    foreach (KeyValuePair<HttpMethod, long> curr in _IncomingRequests)
                    {
                        ret +=
                            "        " + curr.Key.ToString().PadRight(18) + " : " + curr.Value + Environment.NewLine;
                    }
                }
                else
                {
                    ret += "        (none)" + Environment.NewLine; 
                }
            }

            return ret;
        }

        /// <summary>
        /// Reset statistics other than StartTime and UpTime.
        /// </summary>
        public void Reset()
        {
            lock (_DictionaryLock)
            {
                _ReceivedPayloadBytes = 0;
                _SentPayloadBytes = 0;
                _IncomingRequests = new Dictionary<HttpMethod, long>();
            }
        }

        /// <summary>
        /// Retrieve the number of requests received using a specific HTTP method.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <returns>Number of requests received using this method.</returns>
        public long RequestCountByMethod(HttpMethod method)
        {
            lock (_DictionaryLock)
            {
                if (_IncomingRequests.ContainsKey(method)) return _IncomingRequests[method];
                else return 0;
            }
        }

        #endregion

        #region Private-and-Internal-Methods

        internal void IncrementRequestCounter(HttpMethod method)
        {
            lock (_DictionaryLock)
            {
                if (_IncomingRequests.ContainsKey(method))
                {
                    long val = _IncomingRequests[method];
                    val = val + 1;
                    _IncomingRequests.Remove(method);
                    _IncomingRequests.Add(method, val);
                }
                else
                {
                    _IncomingRequests.Add(method, 1);
                }
            }
        }

        #endregion
    }
}
