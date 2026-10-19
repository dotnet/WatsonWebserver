namespace WatsonWebserver.Core.Http2
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Serialized HTTP/2 connection writer. Concurrent writes are serialized as complete batches.
    /// Caller cancellation applies while waiting for the writer; committed writes use the connection
    /// token and finish whole. A transport failure faults the writer and prevents subsequent writes.
    /// </summary>
    public class Http2ConnectionWriter : IDisposable
    {
        private const int ContiguousWriteLimit = 16 * 1024;

        /// <summary>
        /// Instantiate the writer.
        /// </summary>
        /// <param name="stream">Output stream.</param>
        /// <param name="leaveOpen">True to leave the stream open when disposing.</param>
        public Http2ConnectionWriter(Stream stream, bool leaveOpen = false)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _Stream = stream;
            _LeaveOpen = leaveOpen;
        }

        /// <summary>
        /// Write a raw frame with serialized connection ordering.
        /// </summary>
        /// <param name="frame">Frame to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public async ValueTask WriteFrameAsync(Http2RawFrame frame, CancellationToken cancellationToken = default)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            ThrowIfDisposed();

            await AcquireAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                byte[] headerBytes = Http2FrameSerializer.SerializeFrameHeader(frame.Header);
                await WriteHeaderAndPayloadAsync(headerBytes, frame.Payload, 0, frame.Payload.Length, _ConnectionToken).ConfigureAwait(false);
                await _Stream.FlushAsync(_ConnectionToken).ConfigureAwait(false);
            }
            catch
            {
                Fault();
                throw;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Write a frame header and a payload slice with serialized connection ordering.
        /// </summary>
        /// <param name="header">Frame header.</param>
        /// <param name="payload">Payload buffer.</param>
        /// <param name="offset">Payload offset.</param>
        /// <param name="count">Payload length.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public async ValueTask WriteFrameAsync(Http2FrameHeader header, byte[] payload, int offset, int count, CancellationToken cancellationToken = default)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (payload == null && count > 0) throw new ArgumentNullException(nameof(payload));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (payload != null && (offset + count) > payload.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (header.Length != count) throw new ArgumentException("HTTP/2 frame header length must match the supplied payload length.", nameof(header));
            ThrowIfDisposed();

            await AcquireAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                byte[] headerBytes = Http2FrameSerializer.SerializeFrameHeader(header);
                await WriteHeaderAndPayloadAsync(headerBytes, payload, offset, count, _ConnectionToken).ConfigureAwait(false);
                await _Stream.FlushAsync(_ConnectionToken).ConfigureAwait(false);
            }
            catch
            {
                Fault();
                throw;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Write multiple raw frames with a single writer lock and final flush.
        /// </summary>
        /// <param name="frames">Frames to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public async ValueTask WriteFramesAsync(IReadOnlyList<Http2RawFrame> frames, CancellationToken cancellationToken = default)
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            ThrowIfDisposed();

            await AcquireAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    Http2RawFrame frame = frames[i];
                    if (frame == null) throw new ArgumentNullException(nameof(frames), "HTTP/2 frame collection contains a null entry.");

                    byte[] headerBytes = Http2FrameSerializer.SerializeFrameHeader(frame.Header);
                    await WriteHeaderAndPayloadAsync(headerBytes, frame.Payload, 0, frame.Payload.Length, _ConnectionToken).ConfigureAwait(false);
                }

                await _Stream.FlushAsync(_ConnectionToken).ConfigureAwait(false);
            }
            catch
            {
                Fault();
                throw;
            }
            finally
            {
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Write multiple frame header/payload slices with a single writer lock and final flush.
        /// </summary>
        /// <param name="segments">Frame segments.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public async ValueTask WriteFrameSegmentsAsync(IReadOnlyList<Http2FrameWriteSegment> segments, CancellationToken cancellationToken = default)
        {
            await WriteFrameSegmentsAsync(segments, null, null, cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask WriteFramesAsync(IReadOnlyList<Http2RawFrame> frames, Http2StreamWriteGate gate, Http2WriteReservation reservation, CancellationToken token)
        {
            List<Http2FrameWriteSegment> segments = new List<Http2FrameWriteSegment>(frames.Count);
            for (int i = 0; i < frames.Count; i++)
                segments.Add(new Http2FrameWriteSegment { Header = frames[i].Header, Payload = frames[i].Payload, Count = frames[i].Payload.Length });
            await WriteFrameSegmentsAsync(segments, gate, reservation, token).ConfigureAwait(false);
        }

        internal async ValueTask WriteFrameSegmentsAsync(IReadOnlyList<Http2FrameWriteSegment> segments, Http2StreamWriteGate gate, Http2WriteReservation reservation, CancellationToken cancellationToken)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            ThrowIfDisposed();

            await AcquireAsync(cancellationToken).ConfigureAwait(false);

            if (gate != null && !gate.TryCommit())
            {
                _WriteLock.Release();
                throw new Http2WriteNotCommittedException(cancellationToken);
            }
            reservation?.Claim();
            try
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    Http2FrameWriteSegment segment = segments[i];
                    if (segment == null) throw new ArgumentNullException(nameof(segments), "HTTP/2 frame segment collection contains a null entry.");
                    if (segment.Header == null) throw new ArgumentNullException(nameof(segments), "HTTP/2 frame segment contains a null header.");
                    if (segment.Payload == null && segment.Count > 0) throw new ArgumentNullException(nameof(segments), "HTTP/2 frame segment contains a null payload.");
                    if (segment.Offset < 0) throw new ArgumentOutOfRangeException(nameof(segments), "HTTP/2 frame segment offset cannot be negative.");
                    if (segment.Count < 0) throw new ArgumentOutOfRangeException(nameof(segments), "HTTP/2 frame segment count cannot be negative.");
                    if (segment.Payload != null && (segment.Offset + segment.Count) > segment.Payload.Length)
                    {
                        throw new ArgumentOutOfRangeException(nameof(segments), "HTTP/2 frame segment payload range exceeds the supplied buffer.");
                    }

                    if (segment.Header.Length != segment.Count)
                    {
                        throw new ArgumentException("HTTP/2 frame segment header length must match the supplied payload length.", nameof(segments));
                    }

                    byte[] headerBytes = Http2FrameSerializer.SerializeFrameHeader(segment.Header);
                    await WriteHeaderAndPayloadAsync(headerBytes, segment.Payload, segment.Offset, segment.Count, _ConnectionToken).ConfigureAwait(false);
                }

                await _Stream.FlushAsync(_ConnectionToken).ConfigureAwait(false);
            }
            catch
            {
                Fault();
                throw;
            }
            finally
            {
                gate?.Complete();
                _WriteLock.Release();
            }
        }

        /// <summary>
        /// Write a SETTINGS frame.
        /// </summary>
        /// <param name="settings">HTTP/2 settings.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public ValueTask WriteSettingsAsync(Http2Settings settings, CancellationToken cancellationToken = default)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return WriteFrameAsync(Http2FrameSerializer.CreateSettingsFrame(settings), cancellationToken);
        }

        /// <summary>
        /// Write a SETTINGS acknowledgement frame.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public ValueTask WriteSettingsAcknowledgementAsync(CancellationToken cancellationToken = default)
        {
            return WriteFrameAsync(Http2FrameSerializer.CreateSettingsAcknowledgementFrame(), cancellationToken);
        }

        /// <summary>
        /// Write a PING frame.
        /// </summary>
        /// <param name="frame">PING frame.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public ValueTask WritePingAsync(Http2PingFrame frame, CancellationToken cancellationToken = default)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return WriteFrameAsync(Http2FrameSerializer.CreatePingFrame(frame), cancellationToken);
        }

        /// <summary>
        /// Write a GOAWAY frame.
        /// </summary>
        /// <param name="frame">GOAWAY frame.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>ValueTask.</returns>
        public ValueTask WriteGoAwayAsync(Http2GoAwayFrame frame, CancellationToken cancellationToken = default)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            return WriteFrameAsync(Http2FrameSerializer.CreateGoAwayFrame(frame), cancellationToken);
        }

        /// <summary>
        /// Dispose the writer.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;

            _Disposed = true;
            // Queued or committed writes still own the semaphore until they unwind.

            if (!_LeaveOpen)
            {
                using (_Stream)
                {
                }
            }
        }

        private readonly Stream _Stream;
        private readonly bool _LeaveOpen;
        private readonly SemaphoreSlim _WriteLock = new SemaphoreSlim(1, 1);
        private bool _Disposed = false;
        private int _Faulted;
        private CancellationToken _ConnectionToken;

        internal Http2ConnectionWriter(Stream stream, bool leaveOpen, CancellationToken connectionToken)
            : this(stream, leaveOpen)
        {
            _ConnectionToken = connectionToken;
        }

        private async Task AcquireAsync(CancellationToken token)
        {
            ThrowIfDisposed();
            try { await _WriteLock.WaitAsync(token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw new Http2WriteNotCommittedException(token); }
            try { ThrowIfDisposed(); if (token.IsCancellationRequested) throw new Http2WriteNotCommittedException(token); }
            catch { _WriteLock.Release(); throw; }
        }

        private void Fault()
        {
            Interlocked.Exchange(ref _Faulted, 1);
            try { _Stream.Dispose(); } catch (Exception) { }
        }


        private async ValueTask WriteHeaderAndPayloadAsync(byte[] headerBytes, byte[] payload, int offset, int count, CancellationToken cancellationToken)
        {
            if (headerBytes == null) throw new ArgumentNullException(nameof(headerBytes));
            if (payload == null && count > 0) throw new ArgumentNullException(nameof(payload));

            int totalLength = headerBytes.Length + count;
            if (totalLength <= ContiguousWriteLimit)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLength);
                try
                {
                    Buffer.BlockCopy(headerBytes, 0, buffer, 0, headerBytes.Length);
                    if (count > 0)
                    {
                        Buffer.BlockCopy(payload, offset, buffer, headerBytes.Length, count);
                    }

                    await _Stream.WriteAsync(buffer, 0, totalLength, cancellationToken).ConfigureAwait(false);
                    return;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            await _Stream.WriteAsync(headerBytes, 0, headerBytes.Length, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                await _Stream.WriteAsync(payload, offset, count, _ConnectionToken).ConfigureAwait(false);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _Faulted) != 0) throw new IOException("HTTP/2 connection writer is faulted.");
            if (_Disposed) throw new ObjectDisposedException(nameof(Http2ConnectionWriter));
        }
    }
}
