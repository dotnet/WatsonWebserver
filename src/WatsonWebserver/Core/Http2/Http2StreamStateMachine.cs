namespace WatsonWebserver.Core.Http2
{
    using System;

    /// <summary>
    /// HTTP/2 stream state machine.
    /// </summary>
    public class Http2StreamStateMachine
    {
        /// <summary>
        /// Stream identifier.
        /// </summary>
        public int StreamIdentifier
        {
            get
            {
                return _StreamIdentifier;
            }
            private set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamIdentifier));
                _StreamIdentifier = value;
            }
        }

        /// <summary>
        /// Current stream state.
        /// </summary>
        public Http2StreamState State { get; private set; } = Http2StreamState.Idle;

        /// <summary>
        /// Instantiate the state machine.
        /// </summary>
        /// <param name="streamIdentifier">Stream identifier.</param>
        public Http2StreamStateMachine(int streamIdentifier)
        {
            StreamIdentifier = streamIdentifier;
        }

        /// <summary>
        /// Apply inbound HEADERS.
        /// </summary>
        /// <param name="endStream">True if END_STREAM is present.</param>
        public void ReceiveHeaders(bool endStream)
        {
            if (State == Http2StreamState.Idle)
            {
                State = endStream ? Http2StreamState.HalfClosedRemote : Http2StreamState.Open;
                return;
            }

            if (State == Http2StreamState.Open)
            {
                if (endStream) State = Http2StreamState.HalfClosedRemote;
                return;
            }

            if (State == Http2StreamState.HalfClosedLocal)
            {
                if (endStream) State = Http2StreamState.Closed;
                return;
            }

            throw new Http2StreamStateException(State, "Inbound HEADERS are not valid in the current stream state.");
        }

        /// <summary>
        /// Apply outbound HEADERS.
        /// </summary>
        /// <param name="endStream">True if END_STREAM is present.</param>
        public void SendHeaders(bool endStream)
        {
            if (State == Http2StreamState.Idle)
            {
                State = endStream ? Http2StreamState.HalfClosedLocal : Http2StreamState.Open;
                return;
            }

            if (State == Http2StreamState.Open)
            {
                if (endStream) State = Http2StreamState.HalfClosedLocal;
                return;
            }

            if (State == Http2StreamState.HalfClosedRemote)
            {
                if (endStream) State = Http2StreamState.Closed;
                return;
            }

            throw new Http2StreamStateException(State, "Outbound HEADERS are not valid in the current stream state.");
        }

        /// <summary>
        /// Apply inbound DATA.
        /// </summary>
        /// <param name="endStream">True if END_STREAM is present.</param>
        public void ReceiveData(bool endStream)
        {
            if (State == Http2StreamState.Open)
            {
                if (endStream) State = Http2StreamState.HalfClosedRemote;
                return;
            }

            if (State == Http2StreamState.HalfClosedLocal)
            {
                if (endStream) State = Http2StreamState.Closed;
                return;
            }

            throw new Http2StreamStateException(State, "Inbound DATA is not valid in the current stream state.");
        }

        /// <summary>
        /// Apply outbound DATA.
        /// </summary>
        /// <param name="endStream">True if END_STREAM is present.</param>
        public void SendData(bool endStream)
        {
            if (State == Http2StreamState.Open)
            {
                if (endStream) State = Http2StreamState.HalfClosedLocal;
                return;
            }

            if (State == Http2StreamState.HalfClosedRemote)
            {
                if (endStream) State = Http2StreamState.Closed;
                return;
            }

            throw new Http2StreamStateException(State, "Outbound DATA is not valid in the current stream state.");
        }

        /// <summary>
        /// Apply RST_STREAM received from the peer.
        /// </summary>
        public void ReceiveReset()
        {
            State = Http2StreamState.Closed;
        }

        /// <summary>
        /// Apply RST_STREAM sent to the peer.
        /// </summary>
        public void SendReset()
        {
            State = Http2StreamState.Closed;
        }

        /// <summary>
        /// Transition to local reserved state.
        /// </summary>
        public void ReserveLocal()
        {
            if (State != Http2StreamState.Idle) throw new Http2StreamStateException(State, "Local reservation is only valid from the idle state.");
            State = Http2StreamState.ReservedLocal;
        }

        /// <summary>
        /// Transition to remote reserved state.
        /// </summary>
        public void ReserveRemote()
        {
            if (State != Http2StreamState.Idle) throw new Http2StreamStateException(State, "Remote reservation is only valid from the idle state.");
            State = Http2StreamState.ReservedRemote;
        }

        private int _StreamIdentifier = 1;
    }
}
